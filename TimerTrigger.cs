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
    public static class TimerTrigger
    {
        const string DOMAIN = "http://www.aladin.co.kr/";

        const string CATEGORY_ID_COMICS = "2551";
        const string CATEGORY_ID_LNOVEL = "50927";
        const string CATEGORY_ID_ITBOOK = "351";

        static async Task<ProductList> FetchProductListAsync(HttpClient httpClient, bool eBook, string categoryId, ILogger log)
        {
            string ttbKey = Environment.GetEnvironmentVariable("TTB_KEY");
            string partnerId = Environment.GetEnvironmentVariable("PARTNER_ID");

            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("querytype", "itemnewall");
            queryDict.Add("version", "20131101");
            queryDict.Add("cover", "big");
            queryDict.Add("output", "js");
            queryDict.Add("maxresults", "30");
            queryDict.Add("searchtarget", eBook ? "ebook" : "book");
            queryDict.Add("optresult", "ebooklist,fileformatlist");
            queryDict.Add("categoryid", categoryId);
            queryDict.Add("ttbkey", ttbKey);
            queryDict.Add("partner", partnerId);

            StringBuilder sb = new StringBuilder(256);
            sb.Append("ttb/api/itemlist.aspx?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }
            
            Uri uri = new Uri(new Uri(DOMAIN), sb.ToString());

            log.LogInformation("target uri: " + uri);

            string res = await httpClient.GetStringAsync(uri);
            return JsonConvert.DeserializeObject<ProductList>(res);
        }

        static async Task CheckNewProduct(
            HttpClient httpClient,
            bool eBook,
            string categoryId,
            string key,
            CloudTable table,
            CloudQueue queue,
            ILogger log
            )
        {
            ProductList productList = await FetchProductListAsync(httpClient, eBook, categoryId, log);
            if (productList != null)
            {
                foreach (Product product in productList.item)
                {
                    TableOperation retrieveOperation = TableOperation.Retrieve<BookEntity>(key, product.itemId.ToString());
                    TableResult retrievedResult = await table.ExecuteAsync(retrieveOperation);
                    if (retrievedResult.Result == null)
                    {
                        log.LogInformation("enqueue " + product.title);
                        CloudQueueMessage message = new CloudQueueMessage(JsonConvert.SerializeObject(product));
                        await queue.AddMessageAsync(message);
                    }
                }
            }
        }

        [FunctionName("TimerTrigger")]
        public static async Task Run(
            [TimerTrigger("0 0 0,3,9,13 * * *")] TimerInfo myTimer,
            [Table("BookEntity")] CloudTable table,
            [Queue("aladin-newbooks")] CloudQueue queue,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");

            HttpClient httpClient = new HttpClient();

            Task comicsTask = CheckNewProduct(httpClient, false, CATEGORY_ID_COMICS, "COMICS", table, queue, log);
            Task lnovelTask = CheckNewProduct(httpClient, false, CATEGORY_ID_LNOVEL, "LNOVEL", table, queue, log);
            Task itbookTask = CheckNewProduct(httpClient, false, CATEGORY_ID_ITBOOK, "ITBOOK", table, queue, log);

            await comicsTask;
            await lnovelTask;
            await itbookTask;
        }
    }
}
