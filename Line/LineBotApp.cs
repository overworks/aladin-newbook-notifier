using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Line.Messaging;
using Line.Messaging.Webhooks;

namespace Mh.Functions.AladinNewBookNotifier
{
    public class LineBotApp : WebhookApplication
    {
        private string channelId;
        private LineMessagingClient messagingClient;
        private CloudTable accountTable;
        private ILogger log;
        
        public LineBotApp(string channelId, LineMessagingClient messagingClient, CloudTable accountTable, ILogger log)
        {
            this.channelId = channelId;
            this.messagingClient = messagingClient;
            this.accountTable = accountTable;
            this.log = log;
        }

        public async Task MulticastItemMessages(List<ItemLookUpResult.Item> itemList)
        {
            // 한번에 보낼 수 있는 건 5개까지다.
            int count = 0;
            List<ISendMessage> messageList = new List<ISendMessage>();
            while (count < itemList.Count)
            {
                messageList.Clear();
                for (int i = 0; i < 5; ++i)
                {
                    if (count >= itemList.Count)
                    {
                        break;
                    }

                    ISendMessage message = LineUtils.MakeBookMessage(itemList[count]);
                    messageList.Add(message);

                    count++;
                }
                await MulticastMessages(messageList);
            }
        }

        private async Task MulticastMessages(IList<ISendMessage> messages)
        {
            TableContinuationToken continuationToken = null;
            do
            {
                TableQuery<LineAccountEntity> query = new TableQuery<LineAccountEntity>()
                    .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, channelId));
                query.TakeCount = 150;
            
                TableQuerySegment<LineAccountEntity> querySegment = await accountTable.ExecuteQuerySegmentedAsync(query, continuationToken);
                continuationToken = querySegment.ContinuationToken;
                
                if (querySegment.Results != null && querySegment.Results.Count > 0)
                {
                    List<string> to = new List<string>(querySegment.Results.Count);
                    foreach (LineAccountEntity account in querySegment)
                    {
                        to.Add(account.Id);
                    }

                    await messagingClient.MultiCastMessageAsync(to, messages);
                }
            }
            while (continuationToken != null);
        }

        protected override async Task OnMessageAsync(MessageEvent ev)
        {
            log.LogInformation("OnMessage");
            switch (ev.Message.Type)
            {
                case EventMessageType.Text:
                    break;
                case EventMessageType.Image:
                    break;
                case EventMessageType.Video:
                    break;
                case EventMessageType.Audio:
                    break;
                case EventMessageType.Location:
                    break;
                case EventMessageType.Sticker:
                    break;
                case EventMessageType.File:
                    break;
            }
            await Task.CompletedTask;
        }

        protected override async Task OnFollowAsync(FollowEvent ev)
        {
            log.LogInformation("OnFollow");
            await InsertOrReplaceAccount(ev);
        }

        protected override async Task OnUnfollowAsync(UnfollowEvent ev)
        {
            log.LogInformation("OnUnfollow");
            await DeleteAccount(ev);
        }

        protected override async Task OnJoinAsync(JoinEvent ev)
        {
            log.LogInformation("OnJoin");
            await InsertOrReplaceAccount(ev);
        }

        protected override async Task OnLeaveAsync(LeaveEvent ev)
        {
            log.LogInformation("OnLeave");
            await DeleteAccount(ev);
        }

        protected virtual async Task InsertOrReplaceAccount(WebhookEvent ev)
        {
            LineAccountEntity entity = new LineAccountEntity();
            entity.PartitionKey = channelId;
            entity.RowKey = ev.Source.Id;
            entity.Type = ev.Source.Type.ToString();

            TableOperation operation = TableOperation.InsertOrReplace(entity);
            await accountTable.ExecuteAsync(operation);
        }

        protected virtual async Task DeleteAccount(WebhookEvent ev)
        {
            LineAccountEntity entity = new LineAccountEntity();
            entity.PartitionKey = channelId;
            entity.RowKey = ev.Source.Id;
            entity.Type = ev.Source.Type.ToString();

            TableOperation operation = TableOperation.Delete(entity);
            await accountTable.ExecuteAsync(operation);
        }
    }
}