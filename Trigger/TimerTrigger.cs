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
        static async Task CheckNewProduct(
            HttpClient httpClient,
            string categoryId,
            string key,
            CloudTable table,
            CloudQueue queue,
            ILogger log,
            CancellationToken token
            )
        {
            ItemListResult productList = await AladinUtils.FetchItemListAsync(httpClient, categoryId);
            if (productList != null)
            {
                QueueItem queueItem = new QueueItem(key);
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

                            queueItem.ItemList.Add(item.itemId.ToString());
                        }
                    }
                    catch (Exception e)
                    {
                        log.LogError(e.Message);
                    }
                }

                if (!token.IsCancellationRequested && queueItem.ItemList.Count > 0)
                {
                    string json = JsonConvert.SerializeObject(queueItem);
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

            Task comicsTask = CheckNewProduct(httpClient, Const.CategoryID_Comics, "COMICS", table, queue, log, token);
            Task lnovelTask = CheckNewProduct(httpClient, Const.CategoryID_LNovel, "LNOVEL", table, queue, log, token);
            Task itbookTask = CheckNewProduct(httpClient, Const.CategoryID_ITBook, "ITBOOK", table, queue, log, token);

            await comicsTask;
            await lnovelTask;
            await itbookTask;
        }
    }
}
