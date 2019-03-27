using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
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
        static HttpClient httpClient = new HttpClient();

        private static async Task<ItemLookUpResult> LookUpItem(string itemId, ILogger log)
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

            log.LogInformation("target uri: " + uri);

            string res = await httpClient.GetStringAsync(uri);
            log.LogInformation(res);
            return JsonConvert.DeserializeObject<ItemLookUpResult>(res);
        }

        [FunctionName("QueueTrigger")]
        public static async Task Run(
            [QueueTrigger("aladin-newbooks", Connection = "AzureWebJobsStorage")] CloudQueueMessage message,
            [Table("BookEntity")] CloudTable table,
            ILogger log)
        {
            BookEntity entity = JsonConvert.DeserializeObject<BookEntity>(message.AsString);
            if (entity != null)
            {
                ItemLookUpResult lookUpResult = await LookUpItem(entity.RowKey, log);
                if (lookUpResult != null)
                {
                
                    // TableOperation operation = TableOperation.InsertOrReplace(entity);
                    // Task tableTask = table.ExecuteAsync(operation);

                    // await tableTask;
                }
            }
            //log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
        }
    }
}
