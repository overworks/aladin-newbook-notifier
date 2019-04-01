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

        /// <summary>상품정보 트윗 일괄처리</summary>
        private static async Task TweetItems(CredentialsEntity credentials, List<Aladin.ItemLookUpResult.Item> itemList, CancellationToken cancellationToken)
        {
            if (itemList != null && itemList.Count > 0)
            {
                string consumerKey = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_KEY");
                string consumerSecret = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_SECRET");
                string accessToken = credentials.AccessToken;
                string accessTokenSecret = credentials.AccessTokenSecret;

                Tokens tokens = Tokens.Create(consumerKey, consumerSecret, accessToken, accessTokenSecret);

                foreach (Aladin.ItemLookUpResult.Item item in itemList)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    Stream stream = await httpClient.GetStreamAsync(Aladin.Utils.GetHQCoverUrl(item));
                    MediaUploadResult mediaUploadResult = await tokens.Media.UploadAsync(stream);

                    long[] mediaIds = { mediaUploadResult.MediaId };
                    string status = Aladin.Utils.ToTwitterStatus(item);
                    StatusResponse updateResponse = await tokens.Statuses.UpdateAsync(status, media_ids: mediaIds, cancellationToken: cancellationToken);
                }
            }
        }

        private static async Task SendLineMessage(CloudTable accountTable, List<Aladin.ItemLookUpResult.Item> itemList, ILogger log)
        {
            if (lineMessagingClient == null)
            {
                string accessToken = Environment.GetEnvironmentVariable("LINE_COMICS_ACCESS_TOKEN");
                lineMessagingClient = new LineMessagingClient(accessToken);
                ServicePoint sp = ServicePointManager.FindServicePoint(new Uri("https://api.line.me"));
                sp.ConnectionLeaseTimeout = 60 * 1000;
            }

            string channelId = Environment.GetEnvironmentVariable("LINE_COMICS_CHANNEL_ID");
            Line.LineBotApp lineBot = new Line.LineBotApp(channelId, lineMessagingClient, accountTable, log);
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
                QueueItem queueItem = JsonConvert.DeserializeObject<QueueItem>(message.AsString);

                // 스토리지에 저장되어 있는 트위터의 액세스 토큰을 가져옴
                TableOperation retrieveOperation = TableOperation.Retrieve<CredentialsEntity>("Twitter", queueItem.Category);
                TableResult retrievedResult = await credentialsTable.ExecuteAsync(retrieveOperation);
                CredentialsEntity credentialsEntity = retrievedResult.Result as CredentialsEntity;
                if (credentialsEntity == null)
                {
                    throw new Exception($"credentials {queueItem.Category} did not exist.");
                }

                List<Aladin.ItemLookUpResult.Item> itemList = new List<Aladin.ItemLookUpResult.Item>();
                TableBatchOperation batchOperation = new TableBatchOperation();

                foreach (int itemId in queueItem.ItemList)
                {
                    Aladin.ItemLookUpResult lookUpResult = await Aladin.Utils.LookUpItemAsync(httpClient, itemId);
                    foreach (Aladin.ItemLookUpResult.Item item in lookUpResult.item)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new Exception("trigger was cancelled.");
                        }

                        BookEntity bookEntity = new BookEntity();
                        bookEntity.PartitionKey = queueItem.Category;
                        bookEntity.RowKey = itemId.ToString();
                        bookEntity.Name = item.title;
                        
                        batchOperation.InsertOrReplace(bookEntity);
                        itemList.Add(item);
                    }
                }

                if (itemList.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    Task tweetTask = TweetItems(credentialsEntity, itemList, cancellationToken);

                    // 지금은 테스트중이라 만화쪽만 처리한다.
                    Task lineTask = queueItem.Category == "COMICS" ? SendLineMessage(lineAccountTable, itemList, log) : Task.CompletedTask;

                    // 배치처리는 파티션 키가 동일해야하고, 100개까지 가능하다는데...
                    // 일단 파티션 키는 전부 동일하게 넘어올테고, 100개 넘을일은 없겠...지?
                    var tableTask = bookTable.ExecuteBatchAsync(batchOperation);

                    await tweetTask;
                    await lineTask;
                    await tableTask;
                }
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
            }
        }
    }
}
