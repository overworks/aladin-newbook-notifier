using System;
using System.Threading.Tasks;
using CoreTweet;
using Microsoft.WindowsAzure.Storage.Table;

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
    }
}