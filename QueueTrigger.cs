using System;
using System.Collections.Generic;
using System.IO;
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

namespace Mh.Functions.AladinNewBookNotifier
{
    public static class QueueTrigger
    {
        private static HttpClient httpClient = new HttpClient();

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
            string res = await httpClient.GetStringAsync(uri);
            return JsonConvert.DeserializeObject<ItemLookUpResult>(res);
        }

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
            return status + Utils.UnescapeUrl(item.link);
        }

        private static async Task TweetItem(string key, ItemLookUpResult.Item item)
        {
            string consumerKey = Environment.GetEnvironmentVariable(key + "_CONSUMER_KEY");
            string consumerSecret = Environment.GetEnvironmentVariable(key + "_CONSUMER_SECRET");
            string accessToken = Environment.GetEnvironmentVariable(key + "_ACCESS_TOKEN");
            string accessTokenSecret = Environment.GetEnvironmentVariable(key + "_ACCESS_TOKEN_SECRET");

            Tokens tokens = Tokens.Create(consumerKey, consumerSecret, accessToken, accessTokenSecret);

            // 커버 주소를 고해상도로 강제변경
            string coverUrl = Utils.UnescapeUrl(item.cover).Replace(@"/cover/", @"/cover500/");
            Stream stream = await httpClient.GetStreamAsync(coverUrl);
            MediaUploadResult mediaUploadResult = await tokens.Media.UploadAsync(stream);

            long[] mediaIds = { mediaUploadResult.MediaId };
            string status = MakeTweetStatus(item);
            StatusResponse updateResponse = await tokens.Statuses.UpdateAsync(status, media_ids: mediaIds);
        }

        [FunctionName("QueueTrigger")]
        public static async Task Run(
            [QueueTrigger("aladin-newbooks", Connection = "AzureWebJobsStorage")] CloudQueueMessage message,
            [Table("BookEntity")] CloudTable table,
            ILogger log,
            CancellationToken token)
        {
            try
            {
                BookEntity entity = JsonConvert.DeserializeObject<BookEntity>(message.AsString);
                ItemLookUpResult lookUpResult = await LookUpItem(entity.RowKey);

                foreach (ItemLookUpResult.Item item in lookUpResult.item)
                {
                    if (token.IsCancellationRequested)
                    {
                        log.LogInformation("trigger was cancelled.");
                        break;
                    }
                    Task tweetTask = TweetItem(entity.PartitionKey, item);

                    TableOperation operation = TableOperation.InsertOrReplace(entity);
                    Task tableTask = table.ExecuteAsync(operation);

                    await tweetTask;
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
