using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using Mh.Functions.AladinNewBookNotifier.Models;
using System.Text;
using System.Collections.Generic;
using Mh.Functions.AladinNewBookNotifier.Aladin.Models;

namespace Mh.Functions.AladinNewBookNotifier.Triggers
{
    public static class TimerTrigger
    {
        static HttpClient httpClient;

        static TimerTrigger()
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(Aladin.Const.Domain);
        }

        public static async Task<ItemListResult> FetchItemListAsync(string categoryId, int page = 1, int limit = 50)
        {
            string ttbKey = Environment.GetEnvironmentVariable("ALADIN_TTB_KEY");
            string partnerId = Environment.GetEnvironmentVariable("ALADIN_PARTNER_ID");

            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("querytype", "itemnewall");
            queryDict.Add("version", "20131101");
            queryDict.Add("cover", "big");
            queryDict.Add("output", "js");
            queryDict.Add("start", page.ToString());
            queryDict.Add("maxresults", limit.ToString());
            queryDict.Add("categoryid", categoryId);
            queryDict.Add("ttbkey", ttbKey);
            queryDict.Add("partner", partnerId);

            StringBuilder sb = new StringBuilder(256);
            sb.Append(Aladin.Const.Endpoint.List + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }
            
            string response = await httpClient.GetStringAsync(sb.ToString());
            ItemListResult result = JsonConvert.DeserializeObject<ItemListResult>(response);

            return result;
        }

        static async Task CheckNewProduct(string categoryId, CloudTable table, CloudQueue queue, ILogger log, CancellationToken token)
        {
            try
            {
                var productList = await FetchItemListAsync(categoryId);
                var queueItem = new QueueItem(categoryId);

                foreach (var item in productList.item)
                {
                    if (token.IsCancellationRequested)
                    {
                        log.LogInformation("function was cancelled.");
                        break;
                    }

                    // 가끔씩 잘못된 것들이 끼어들어오기도 하더라...
                    if (item.itemId == 0 || string.IsNullOrEmpty(item.title) || string.IsNullOrEmpty(item.isbn))
                    {
                        continue;
                    }

                    TableOperation retrieveOperation = TableOperation.Retrieve<BookEntity>(categoryId, item.itemId.ToString());
                    TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);
                    if (retrievedResult.Result == null)
                    {
                        log.LogInformation($"enqueue {item.title} to Category {categoryId}");

                        queueItem.ItemList.Add(item.itemId);
                    }
                }

                if (!token.IsCancellationRequested && queueItem.ItemList.Count > 0)
                {
                    string json = JsonConvert.SerializeObject(queueItem);
                    CloudQueueMessage message = new CloudQueueMessage(json);
                    await queue.AddMessageAsync(message);
                }
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
            }
        }

        [FunctionName("TimerTrigger")]
        public static async Task Run(
            [TimerTrigger("0 0 9,12,18,22 * * *")] TimerInfo myTimer,
            [Table("BookEntity")] CloudTable table,
            [Queue("aladin-newbooks")] CloudQueue queue,
            ILogger log,
            CancellationToken token)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            var tasks = new Task[]
            {
                CheckNewProduct(Aladin.Const.CategoryID.Comics, table, queue, log, token),
                CheckNewProduct(Aladin.Const.CategoryID.LNovel, table, queue, log, token),
                CheckNewProduct(Aladin.Const.CategoryID.ITBook, table, queue, log, token)
            };
            await Task.WhenAll(tasks);
        }
    }
}
