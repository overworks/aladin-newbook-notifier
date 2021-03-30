using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.Logging;
using Line.Messaging;
using Line.Messaging.Webhooks;
using Mh.Functions.AladinNewBookNotifier.Models;
using Mh.Functions.AladinNewBookNotifier.Aladin.Models;

namespace Mh.Functions.AladinNewBookNotifier.Line
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

        public async Task MulticastItemMessagesAsync(IList<ItemLookUpResult.Item> itemList)
        {
            
            int count = 0;
            List<ISendMessage> messageList = new List<ISendMessage>();
            while (count < itemList.Count)
            {
                messageList.Clear();

                // 한번에 보낼 수 있는 건 5개.
                for (int i = 0; i < 5; ++i)
                {
                    if (count >= itemList.Count)
                    {
                        break;
                    }

                    // 캐러셀 하나에는 10개까지...인데 그럼 한번에 50개 보낼 수 있는건가?
                    string altText = itemList[count].title;
                    if (count < itemList.Count - 1)
                    {
                        altText += " 외";
                    }
                    CarouselContainerFlexMessage carouselMessage = FlexMessage.CreateCarouselMessage(altText);

                    for (int j = 0; j < 10; ++j)
                    {
                        if (count >= itemList.Count)
                        {
                            break;
                        }

                        BubbleContainer container = itemList[count].ToBubbleContainer();
                        carouselMessage.AddBubbleContainer(container);
                        
                        count++;
                    }

                    messageList.Add(carouselMessage);
                }

                if (messageList.Count > 0)
                {
                    await MulticastMessagesAsync(messageList);
                }
            }
        }

        private async Task MulticastMessagesAsync(IList<ISendMessage> messages)
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

            // C# 7부터 추가된 형식 패턴.
            switch (ev.Message)
            {
                // 일반적인 텍스트 메시지
                case TextEventMessage textMessage:
                    break;

                // 이미지, 오디오, 비디오
                case MediaEventMessage mediaMessage:
                    break;

                // 장소
                case LocationEventMessage locationMessage:
                    break;

                // 스티커
                case StickerEventMessage stickerMessage:
                    break;

                // 파일
                case FileEventMessage fileMessage:
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

            log.LogInformation($"Channel ID: {channelId}, ID: {ev.Source.Id}, Type: {ev.Source.Type.ToString()}");

            TableOperation operation = TableOperation.InsertOrReplace(entity);
            await accountTable.ExecuteAsync(operation);
        }

        protected virtual async Task DeleteAccount(WebhookEvent ev)
        {
            LineAccountEntity entity = new LineAccountEntity();
            entity.PartitionKey = channelId;
            entity.RowKey = ev.Source.Id;
            entity.Type = ev.Source.Type.ToString();
            entity.ETag = "*";

            log.LogInformation($"Channel ID: {channelId}, ID: {ev.Source.Id}, Type: {ev.Source.Type.ToString()}");

            TableOperation operation = TableOperation.Delete(entity);
            await accountTable.ExecuteAsync(operation);
        }
    }
}