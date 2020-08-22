using System;
using System.Threading.Tasks;
using CoreTweet;
using Microsoft.WindowsAzure.Storage.Table;
using Mh.Functions.AladinNewBookNotifier.Models;
using Mh.Functions.AladinNewBookNotifier.Aladin.Models;

namespace Mh.Functions.AladinNewBookNotifier.Twitter
{
    public static class Utils
    {
        public static async Task<Tokens> CreateTokenAsync(
            CloudTable credentialsTable,
            string categoryId)
        {
            var retrieveOperation = TableOperation.Retrieve<CredentialsEntity>("Twitter", categoryId);
            var tableResult = await credentialsTable.ExecuteAsync(retrieveOperation);
            var credential = tableResult.Result as CredentialsEntity;
            if (credential == null)
            {
                return null;
            }

            string consumerKey = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_KEY");
            string consumerSecret = Environment.GetEnvironmentVariable("TWITTER_CONSUMER_SECRET");
            string accessToken = credential.AccessToken;
            string accessTokenSecret = credential.AccessTokenSecret;

            return Tokens.Create(consumerKey, consumerSecret, accessToken, accessTokenSecret);
        }

        public static string ToTwitterStatus(this ItemLookUpResult.Item item)
        {
            // 트위터는 140자까지 적을 수 있으나, 영문-숫자-특문은 2자당 한칸만을 차지한다.
            // 링크가 23자를 차지하므로 140 - 12 = 128자까지 가능하다.
            string additionalInfo = $" ({item.author} / {item.publisher} / {item.pubDate} / {item.priceStandard}원) ";
            string status = item.title + additionalInfo;
            if (status.Length > 128)
            {
                // 제목이 긴건가 추가 정보가 긴건가 
                int maxTitleLength = 128 - additionalInfo.Length;
                if (maxTitleLength >= 2)
                {
                    // 제목을 적당히 줄임
                    status = item.title.Substring(0, maxTitleLength - 1) + "…" + additionalInfo;
                }
                else
                {
                    // 제목만 표시
                    if (item.title.Length < 128)
                    {
                        status = item.title + " ";
                    }
                    else
                    {
                        // 제목도 길다!
                        status = item.title.Substring(0, 126) + "… ";
                    }
                }
            }
            return status + Aladin.Utils.UnescapeUrl(item.link);
        }
    }
}