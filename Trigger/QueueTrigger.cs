using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
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
        
        /// <summary>알라딘 상품 조회 API를 사용하여 상품 정보를 가져옴</summary>
        /// <param name="itemId">상품 ID</param>
        private static async Task<ItemLookUpResult> LookUpItem(string itemId)
        {
            string ttbKey = Environment.GetEnvironmentVariable("TTB_KEY");
            string partnerId = Environment.GetEnvironmentVariable("PARTNER_ID");

            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("ttbkey", ttbKey);
            queryDict.Add("partner", partnerId);
            queryDict.Add("version", "20131101");
            queryDict.Add("cover", "big");
            queryDict.Add("output", "js");
            queryDict.Add("itemidtype", "itemid");
            queryDict.Add("itemid", itemId.ToString());
            
            StringBuilder sb = new StringBuilder(256);
            sb.Append(Const.EndPoint_LookUp + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }

            Uri uri = new Uri(new Uri(Const.Domain), sb.ToString());
            string response = await httpClient.GetStringAsync(uri);
            ItemLookUpResult result = JsonConvert.DeserializeObject<ItemLookUpResult>(response);

            // url 이스케이프나 커버 주소 변경등은 미리 여기서 처리해둠.
            result.link = Utils.UnescapeUrl(result.link);
            foreach (ItemLookUpResult.Item item in result.item)
            {
                item.link = Utils.UnescapeUrl(item.link);
                item.cover = Utils.UnescapeUrl(item.cover).Replace(@"/cover/", @"/cover500/");
            }

            return result;
        }

        /// <summary>트윗 본문 작성</summary>
        private static string MakeTweetStatus(ItemLookUpResult.Item item)
        {
            // 트위터는 140자까지 적을 수 있으나, 영문-숫자-특문은 2자당 한칸만을 차지한다.
            // 링크가 23자를 차지하므로 140 - 12 = 128자까지 가능하다.            
            string additionalInfo = $" ({item.author} / {item.publisher} / {item.pubDate} / {item.priceStandard}원) ";
            string status = item.title + additionalInfo;
            if (status.Length > 128)
            {
                // 제목이 긴건가 추가 정보가 긴건가 
                int maxTitleLength = 128 - additionalInfo.Length;
                if (maxTitleLength >= 2)
                {
                    // 제목을 적당히 줄임
                    status = item.title.Substring(0, maxTitleLength - 1) + "…" + additionalInfo;
                }
                else
                {
                    // 제목만 표시
                    if (item.title.Length < 128)
                    {
                        status = item.title + " ";
                    }
                    else
                    {
                        // 제목도 길다!
                        status = item.title.Substring(0, 126) + "… ";
                    }
                }
            }
            return status + item.link;
        }

        /// <summary>상품정보를 트윗</summary>
        private static async Task TweetItem(CredentialsEntity credentials, ItemLookUpResult.Item item)
        {
            string consumerKey = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_KEY");
            string consumerSecret = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_SECRET");
            string accessToken = credentials.AccessToken;
            string accessTokenSecret = credentials.AccessTokenSecret;

            Tokens tokens = Tokens.Create(consumerKey, consumerSecret, accessToken, accessTokenSecret);

            // 커버 주소를 고해상도로 강제변경
            Stream stream = await httpClient.GetStreamAsync(item.cover);
            MediaUploadResult mediaUploadResult = await tokens.Media.UploadAsync(stream);

            long[] mediaIds = { mediaUploadResult.MediaId };
            string status = MakeTweetStatus(item);
            StatusResponse updateResponse = await tokens.Statuses.UpdateAsync(status, media_ids: mediaIds);
        }

        /// <summary>상품정보 트윗 일괄처리</summary>
        private static async Task TweetItems(CredentialsEntity credentials, List<ItemLookUpResult.Item> itemList)
        {
            foreach (ItemLookUpResult.Item item in itemList)
            {
                await TweetItem(credentials, item);
            }
        }

        private static ISendMessage MakeBookMessage(ItemLookUpResult.Item item)
        {
            // 이미지 컴포넌트
            ImageComponent hero = new ImageComponent();
            hero.Url = item.cover;
            hero.Action = new UriTemplateAction("Link", item.link);

            // 바디
            TextComponent author = new TextComponent(item.author);
            BoxComponent body = new BoxComponent();
            body.Contents.Add(author);
            body.Contents.Add(new SeparatorComponent());

            // 푸터
            ButtonComponent linkButton = new ButtonComponent();
            linkButton.Action = new UriTemplateAction("Link", item.link);
            
            BoxComponent footer = new BoxComponent();
            footer.Contents.Add(linkButton);
            
            
            BubbleContainer container = new BubbleContainer();
            container.Hero = hero;
            container.Footer = footer;

            return FlexMessage.CreateBubbleMessage(item.title).SetBubbleContainer(container);
        }

        private static async Task SendLineMessage(CloudTable accountTable, List<ItemLookUpResult.Item> itemList, ILogger log)
        {
            if (lineMessagingClient == null)
            {
                string accessToken = Environment.GetEnvironmentVariable("LINE_COMICS_ACCESS_TOKEN");
                lineMessagingClient = new LineMessagingClient(accessToken);
                ServicePoint sp = ServicePointManager.FindServicePoint(new Uri("https://api.line.me"));
                sp.ConnectionLeaseTimeout = 60 * 1000;
            }

            LineBotApp lineBot = new LineBotApp(lineMessagingClient, accountTable, log);
            
            // 한번에 보낼 수 있는 건 5개까지다.
            int count = 0;
            List<ISendMessage> messageList = new List<ISendMessage>();
            while (count < itemList.Count)
            {
                messageList.Clear();
                for (int i = 0; i < 5; ++i)
                {
                    if (count >= itemList.Count)
                    {
                        break;
                    }

                    ISendMessage message = MakeBookMessage(itemList[count]);
                    messageList.Add(message);

                    count++;
                }
                await lineBot.MulticastMessages(messageList);
            }
        }

        [FunctionName("QueueTrigger")]
        public static async Task Run(
            [QueueTrigger("aladin-newbooks")] CloudQueueMessage message,
            [Table("BookEntity")] CloudTable bookTable,
            [Table("LineAccount")] CloudTable lineAccountTable,
            [Table("Credentials", "Twitter")] CloudTable credentialsTable,
            ILogger log,
            CancellationToken token)
        {
            try
            {
                List<TableEntity> entityList = JsonConvert.DeserializeObject<List<TableEntity>>(message.AsString);

                CredentialsEntity credentialsEntity = null;
                List<ItemLookUpResult.Item> itemList = new List<ItemLookUpResult.Item>();
                TableBatchOperation batchOperation = new TableBatchOperation();

                foreach (TableEntity entity in entityList)
                {
                    if (credentialsEntity == null)
                    {
                        // 스토리지에 저장되어 있는 트위터의 액세스 토큰을 가져옴
                        TableOperation retrieveOperation = TableOperation.Retrieve<CredentialsEntity>("Twitter", entity.PartitionKey);
                        TableResult retrievedResult = await credentialsTable.ExecuteAsync(retrieveOperation);
                        credentialsEntity = retrievedResult.Result as CredentialsEntity;
                        if (credentialsEntity == null)
                        {
                            throw new Exception($"credentials {entity.PartitionKey} did not exist.");
                        }
                    }

                    ItemLookUpResult lookUpResult = await LookUpItem(entity.RowKey);
                    foreach (ItemLookUpResult.Item item in lookUpResult.item)
                    {
                        if (token.IsCancellationRequested)
                        {
                            throw new Exception("trigger was cancelled.");
                        }

                        BookEntity bookEntity = new BookEntity();
                        bookEntity.PartitionKey = entity.PartitionKey;
                        bookEntity.RowKey = entity.RowKey;
                        bookEntity.Name = item.title;
                        
                        batchOperation.InsertOrReplace(bookEntity);
                        itemList.Add(item);
                    }
                }

                if (itemList.Count > 0)
                {
                    Task tweetTask = TweetItems(credentialsEntity, itemList);

                    // 지금은 테스트중이라 만화쪽만 처리한다.
                    //Task lineTask = credentialsEntity.RowKey == "COMICS" ? SendLineMessage(lineAccountTable, itemList, log) : Task.CompletedTask;

                    // 배치처리는 파티션 키가 동일해야하고, 100개까지 가능하다는데...
                    // 일단 파티션 키는 전부 동일하게 넘어올테고, 100개 넘을일은 없겠...지?
                    var tableTask = bookTable.ExecuteBatchAsync(batchOperation);

                    await tweetTask;
                    //await lineTask;
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
