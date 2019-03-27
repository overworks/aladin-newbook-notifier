using System;
using System.Net.Http;
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

        [FunctionName("QueueTrigger")]
        public static async Task Run(
            [QueueTrigger("aladin-newbooks", Connection = "AzureWebJobsStorage")] CloudQueueMessage message,
            [Table("BookEntity")] CloudTable table,
            ILogger log)
        {
            BookEntity entity = JsonConvert.DeserializeObject<BookEntity>(message.AsString);
            if (entity != null)
            {
                // TableOperation operation = TableOperation.InsertOrReplace(entity);
                // Task tableTask = table.ExecuteAsync(operation);

                // await tableTask;
            }
            //log.LogInformation($"C# Queue trigger function processed: {myQueueItem}");
        }
    }
}
