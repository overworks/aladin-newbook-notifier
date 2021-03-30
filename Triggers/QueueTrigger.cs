using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreTweet;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.Storage.Queue;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Line.Messaging;
using Mh.Functions.AladinNewBookNotifier.Aladin.Models;
using Mh.Functions.AladinNewBookNotifier.Models;

namespace Mh.Functions.AladinNewBookNotifier.Triggers
{
    public static class QueueTrigger
    {
        private static HttpClient httpClient;
        private static LineMessagingClient lineMessagingClient;

        static QueueTrigger()
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Aladin.Const.Domain);

            string accessToken = Environment.GetEnvironmentVariable("LINE_COMICS_ACCESS_TOKEN");
            lineMessagingClient = new LineMessagingClient(accessToken);
            var sp = ServicePointManager.FindServicePoint(new Uri("https://api.line.me"));
            sp.ConnectionLeaseTimeout = 60 * 1000;
        }

        /// <summary>알라딘 상품 조회 API를 사용하여 상품 정보를 가져옴</summary>
        /// <param name="itemId">상품 ID</param>
        public static async Task<ItemLookUpResult> LookUpItemAsync(int itemId)
        {
            string ttbKey = Environment.GetEnvironmentVariable("ALADIN_TTB_KEY");
            string partnerId = Environment.GetEnvironmentVariable("ALADIN_PARTNER_ID");

            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("ttbkey", ttbKey);
            queryDict.Add("partner", partnerId);
            queryDict.Add("version", "20131101");
            queryDict.Add("cover", "big");
            queryDict.Add("output", "js");
            queryDict.Add("itemidtype", "itemid");
            queryDict.Add("itemid", itemId.ToString());
            
            StringBuilder sb = new StringBuilder(256);
            sb.Append(Aladin.Const.Endpoint.LookUp + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }

            Uri uri = new Uri(new Uri(Aladin.Const.Domain), sb.ToString());
            string response = await httpClient.GetStringAsync(uri);
            var result = JsonConvert.DeserializeObject<ItemLookUpResult>(response);

            return result;
        }

        /// <summary>상품정보 트윗(비동기 처리)</summary>
        private static async Task TweetItemAsync(Tokens tokens, ItemLookUpResult.Item item, CancellationToken cancellationToken)
        {
            var stream = await httpClient.GetStreamAsync(item.hqCover);
            if (cancellationToken.IsCancellationRequested)
                return;

            var mediaUploadResult = await tokens.Media.UploadAsync(stream, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            long[] mediaIds = { mediaUploadResult.MediaId };
            string status = Twitter.Utils.ToTwitterStatus(item);
            var statusResponse = await tokens.Statuses.UpdateAsync(status, media_ids: mediaIds, cancellationToken: cancellationToken);
        }

        /// <summary>상품정보 트윗 일괄처리</summary>
        private static async Task TweetItemsAsync(Tokens tokens, List<ItemLookUpResult.Item> itemList, CancellationToken cancellationToken)
        {
            // 호출하는 쪽에서 하고 있어서 null 체크 빼버림.
            var taskList = new List<Task>(itemList.Count);
            foreach (var item in itemList)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                taskList.Add(TweetItemAsync(tokens, item, cancellationToken));
            }

            await Task.WhenAll(taskList);
        }

        private static async Task SendLineMessageAsync(CloudTable accountTable, IList<ItemLookUpResult.Item> itemList, ILogger log)
        {
            string channelId = Environment.GetEnvironmentVariable("LINE_COMICS_CHANNEL_ID");
            var lineBot = new Line.LineBotApp(channelId, lineMessagingClient, accountTable, log);
            await lineBot.MulticastItemMessagesAsync(itemList);
        }

        [FunctionName("QueueTrigger")]
        public static async Task Run(
            [QueueTrigger("aladin-newbooks")] CloudQueueMessage message,
            [Table("BookEntity")] CloudTable bookTable,
            [Table("LineAccount")] CloudTable lineAccountTable,
            [Table("Credentials", "Twitter")] CloudTable credentialsTable,
            ILogger log,
            CancellationToken cancellationToken)
        {
            try
            {
                var queueItem = JsonConvert.DeserializeObject<QueueItem>(message.AsString);
                string categoryId = queueItem.CategoryId;

                var itemList = new List<ItemLookUpResult.Item>();
                var batchOperation = new TableBatchOperation();

                foreach (int itemId in queueItem.ItemList)
                {
                    var lookUpResult = await LookUpItemAsync(itemId);
                    foreach (var item in lookUpResult.item)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var bookEntity = new BookEntity();
                        bookEntity.PartitionKey = categoryId;
                        bookEntity.RowKey = itemId.ToString();
                        bookEntity.Name = item.title;
                        
                        batchOperation.InsertOrReplace(bookEntity);
                        itemList.Add(item);
                    }
                }

                if (itemList.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var tokens = await Twitter.Utils.CreateTokenAsync(credentialsTable, queueItem.CategoryId);
                    var tweetTask = tokens != null ? TweetItemsAsync(tokens, itemList, cancellationToken) : Task.CompletedTask;

                    // 배치처리는 파티션 키가 동일해야하고, 100개까지 가능하다는데...
                    // 일단 파티션 키는 전부 동일하게 넘어올테고, 100개 넘을일은 없겠...지?
                    var tableTask = bookTable.ExecuteBatchAsync(batchOperation);

                    // 트위터와 테이블 쓰기 작업을 먼저한다.
                    await Task.WhenAll(tweetTask, tableTask);

                    // 라인 메시지를 보낼때 리미트가 걸려 예외가 발생하므로 라인만 따로 한다.
                    if (queueItem.CategoryId == Aladin.Const.CategoryID.Comics)
                    {
                        await SendLineMessageAsync(lineAccountTable, itemList, log);
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(e.Message);

                if (e is LineResponseException lineEx)
                {
                    if (lineEx.StatusCode != HttpStatusCode.TooManyRequests)
                    {
                        // 에러 발생시 내 계정으로 예외 정보를 보냄.
                        string adminLineId = Environment.GetEnvironmentVariable("LINE_ADMIN_USER_ID");
                        var error = new
                        {
                            Type = e.GetType().ToString(),
                            Message = e.Message,
                            StackTrace = e.StackTrace
                        };
                        string json = JsonConvert.SerializeObject(error, Formatting.Indented);
                        await lineMessagingClient.PushMessageAsync(adminLineId, json);
                    }
                }
            }
        }
    }
}
