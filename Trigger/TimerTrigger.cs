using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Mh.Functions.AladinNewBookNotifier
{
    public static class TimerTrigger
    {
        static async Task<ItemListResult> FetchItemListAsync(HttpClient httpClient, bool eBook, string categoryId, ILogger log)
        {
            string ttbKey = Environment.GetEnvironmentVariable("TTB_KEY");
            string partnerId = Environment.GetEnvironmentVariable("PARTNER_ID");

            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("querytype", "itemnewall");
            queryDict.Add("version", "20131101");
            queryDict.Add("cover", "big");
            queryDict.Add("output", "js");
            queryDict.Add("maxresults", "30");
            //queryDict.Add("searchtarget", eBook ? "ebook" : "book");
            //queryDict.Add("optresult", "ebooklist,fileformatlist");
            queryDict.Add("categoryid", categoryId);
            queryDict.Add("ttbkey", ttbKey);
            queryDict.Add("partner", partnerId);

            StringBuilder sb = new StringBuilder(256);
            sb.Append(Const.EndPoint_List + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }
            
            Uri uri = new Uri(new Uri(Const.Domain), sb.ToString());

            log.LogInformation("target uri: " + uri);

            string response = await httpClient.GetStringAsync(uri);
            ItemListResult result = JsonConvert.DeserializeObject<ItemListResult>(response);

            return result;
        }

        static async Task CheckNewProduct(
            HttpClient httpClient,
            bool eBook,
            string categoryId,
            string key,
            CloudTable table,
            CloudQueue queue,
            ILogger log,
            CancellationToken token
            )
        {
            ItemListResult productList = await FetchItemListAsync(httpClient, eBook, categoryId, log);
            if (productList != null)
            {
                List<TableEntity> entityList = new List<TableEntity>();
                foreach (ItemListResult.Item item in productList.item)
                {
                    if (token.IsCancellationRequested)
                    {
                        log.LogInformation("function was cancelled.");
                        break;
                    }

                    try
                    {
                        TableOperation retrieveOperation = TableOperation.Retrieve<BookEntity>(key, item.itemId.ToString());
                        TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);
                        if (retrievedResult.Result == null)
                        {
                            log.LogInformation("enqueue " + item.title);

                            entityList.Add(new TableEntity(key, item.itemId.ToString()));
                        }
                    }
                    catch (Exception e)
                    {
                        log.LogError(e.Message);
                    }
                }

                if (!token.IsCancellationRequested && entityList.Count > 0)
                {
                    string json = JsonConvert.SerializeObject(entityList);
                    CloudQueueMessage message = new CloudQueueMessage(json);
                    await queue.AddMessageAsync(message);
                }
            }
        }

        [FunctionName("TimerTrigger")]
        public static async Task Run(
            [TimerTrigger("0 0 0,3,9,13 * * *")] TimerInfo myTimer,
            [Table("BookEntity")] CloudTable table,
            [Queue("aladin-newbooks")] CloudQueue queue,
            ILogger log,
            CancellationToken token)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            HttpClient httpClient = new HttpClient();

            Task comicsTask = CheckNewProduct(httpClient, false, Const.CategoryID_Comics, "COMICS", table, queue, log, token);
            Task lnovelTask = CheckNewProduct(httpClient, false, Const.CategoryID_LNovel, "LNOVEL", table, queue, log, token);
            Task itbookTask = CheckNewProduct(httpClient, false, Const.CategoryID_ITBook, "ITBOOK", table, queue, log, token);

            await comicsTask;
            await lnovelTask;
            await itbookTask;
        }
    }
}
