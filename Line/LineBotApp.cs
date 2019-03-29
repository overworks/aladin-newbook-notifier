using Line.Messaging;
using Line.Messaging.Webhooks;

namespace Mh.Functions.AladinNewBookNotifier
{
    public class LineBotApp : WebhookApplication
    {
        private LineMessagingClient messagingClient;

        public LineBotApp(LineMessagingClient messagingClient)
        {
            this.messagingClient = messagingClient;
        }
    }
}