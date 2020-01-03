using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Mh.Functions.AladinNewBookNotifier
{
    public static class TimerTrigger
    {
        static HttpClient httpClient = new HttpClient();

        static async Task CheckNewProduct(
            string categoryId,
            CloudTable table,
            CloudQueue queue,
            ILogger log,
            CancellationToken token
            )
        {
            try
            {
                Aladin.ItemListResult productList = await Aladin.Utils.FetchItemListAsync(httpClient, categoryId);
                QueueItem queueItem = new QueueItem(categoryId);
                foreach (Aladin.ItemListResult.Item item in productList.item)
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

            Task comicsTask = CheckNewProduct(Aladin.Const.CategoryID.Comics, table, queue, log, token);
            Task lnovelTask = CheckNewProduct(Aladin.Const.CategoryID.LNovel, table, queue, log, token);
            Task itbookTask = CheckNewProduct(Aladin.Const.CategoryID.ITBook, table, queue, log, token);

            await Task.WhenAll(comicsTask, lnovelTask, itbookTask);
        }
    }
}
