using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Mh.Functions.AladinNewBookNotifier
{
    public static class TimerTrigger
    {
        [FunctionName("TimerTrigger")]
        public static void Run(
            [TimerTrigger("0 */5 * * * *")] TimerInfo myTimer,
            [Table("BookEntity")] CloudTable cloudTable,
            ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
