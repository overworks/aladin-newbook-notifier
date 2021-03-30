using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Line.Messaging;
using Line.Messaging.Webhooks;

namespace Mh.Functions.AladinNewBookNotifier.Triggers
{
    /// <summary>라인의 신간 알림 채널의 웹훅 트리거</summary>
    public static class LineComicsWebHookTrigger
    {
        private static LineMessagingClient messagingClient;
        
        static LineComicsWebHookTrigger()
        {
            messagingClient = new LineMessagingClient(Environment.GetEnvironmentVariable("LINE_COMICS_ACCESS_TOKEN"));
            ServicePoint sp = ServicePointManager.FindServicePoint(new Uri("https://api.line.me"));
            sp.ConnectionLeaseTimeout = 60 * 1000;
        }

        [FunctionName("LineComicsWebHookTrigger")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequestMessage req,
            [Table("LineAccount")] CloudTable accountTable,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            IEnumerable<WebhookEvent> events;
            try
            {
                string channelSecret = Environment.GetEnvironmentVariable("LINE_COMICS_CHANNEL_SECRET");
                events = await req.GetWebhookEventsAsync(channelSecret);
            }
            catch (InvalidSignatureException e)
            {
                return req.CreateResponse(HttpStatusCode.Forbidden, new { Message = e.Message });
            }

            try
            {
                string channelId = Environment.GetEnvironmentVariable("LINE_COMICS_CHANNEL_ID");
                Line.LineBotApp bot = new Line.LineBotApp(channelId, messagingClient, accountTable, log);
                await bot.RunAsync(events);
            }
            catch (Exception e)
            {
                log.LogError(e.Message);
            }
            
            return req.CreateResponse(HttpStatusCode.OK);
        }
    }
}
