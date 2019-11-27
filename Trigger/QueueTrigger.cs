using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CoreTweet;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Line.Messaging;

namespace Mh.Functions.AladinNewBookNotifier
{
    public static class QueueTrigger
    {
        private static HttpClient httpClient = new HttpClient();
        private static LineMessagingClient lineMessagingClient;

        static QueueTrigger()
        {
            string accessToken = Environment.GetEnvironmentVariable("LINE_COMICS_ACCESS_TOKEN");
            lineMessagingClient = new LineMessagingClient(accessToken);
            var sp = ServicePointManager.FindServicePoint(new Uri("https://api.line.me"));
            sp.ConnectionLeaseTimeout = 60 * 1000;
        }

        /// <summary>상품정보 트윗(비동기 처리)</summary>
        private static async Task TweetItemAsync(Tokens tokens, Aladin.ItemLookUpResult.Item item, CancellationToken cancellationToken)
        {
            var stream = await httpClient.GetStreamAsync(Aladin.Utils.GetHQCoverUrl(item));
            if (cancellationToken.IsCancellationRequested)
                return;

            var mediaUploadResult = await tokens.Media.UploadAsync(stream, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            long[] mediaIds = { mediaUploadResult.MediaId };
            string status = Aladin.Utils.ToTwitterStatus(item);
            var statusResponse = await tokens.Statuses.UpdateAsync(status, media_ids: mediaIds, cancellationToken: cancellationToken);
        }

        /// <summary>상품정보 트윗 일괄처리</summary>
        private static async Task TweetItemsAsync(Tokens tokens, List<Aladin.ItemLookUpResult.Item> itemList, CancellationToken cancellationToken)
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

        private static async Task SendLineMessage(CloudTable accountTable, List<Aladin.ItemLookUpResult.Item> itemList, ILogger log)
        {
            string channelId = Environment.GetEnvironmentVariable("LINE_COMICS_CHANNEL_ID");
            var lineBot = new Line.LineBotApp(channelId, lineMessagingClient, accountTable, log);
            await lineBot.MulticastItemMessages(itemList);
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

                var itemList = new List<Aladin.ItemLookUpResult.Item>();
                var batchOperation = new TableBatchOperation();

                foreach (int itemId in queueItem.ItemList)
                {
                    var lookUpResult = await Aladin.Utils.LookUpItemAsync(httpClient, itemId);
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

                    // 지금은 만화쪽만 처리한다.
                    var lineTask = queueItem.CategoryId == Aladin.Const.CategoryID.Comics ? SendLineMessage(lineAccountTable, itemList, log) : Task.CompletedTask;

                    // 배치처리는 파티션 키가 동일해야하고, 100개까지 가능하다는데...
                    // 일단 파티션 키는 전부 동일하게 넘어올테고, 100개 넘을일은 없겠...지?
                    var tableTask = bookTable.ExecuteBatchAsync(batchOperation);

                    await Task.WhenAll(tweetTask, lineTask, tableTask);
                }
            }
            catch (Exception e)
            {
                log.LogError(e.StackTrace);

                // 에러 발생시 내 계정으로 예외 정보를 보냄.
                string adminLineId = Environment.GetEnvironmentVariable("LINE_ADMIN_USER_ID");
                var error = new { Type = e.GetType().ToString(), Message = e.Message, StackTrace = e.StackTrace };
                string json = JsonConvert.SerializeObject(error, Formatting.Indented);
                await lineMessagingClient.PushMessageAsync(adminLineId, json);
            }
        }
    }
}
