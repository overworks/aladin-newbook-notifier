using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CoreTweet;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace Mh.Functions.AladinNewBookNotifier
{
    public static class WeeklyTimerTrigger
    {
        private static async Task<List<Status>> FetchLastTweetList(
            Tokens tokens,
            long userId,
            DateTimeOffset dateTimeOffset,
            TimeSpan timeRange,
            CancellationToken cancellationToken)
        {
            var list = new List<Status>();
            long? max_id = null;
            bool broken = false;
            
            do
            {
                if (cancellationToken.IsCancellationRequested) return null;
                var statuses = await tokens.Statuses.UserTimelineAsync(userId, max_id: max_id, trim_user: true, cancellationToken: cancellationToken);

                foreach (var status in statuses)
                {
                    var timeSpan = dateTimeOffset - status.CreatedAt;
                    if (timeSpan > timeRange)
                    {
                        broken = true;
                        break;
                    }
                    list.Add(status);
                }

                var lastElem = statuses[statuses.Count - 1];
                max_id = lastElem.Id - 1;
            }
            while (!broken);

            return list;
        }

        private static async Task TweetWeeklyReportAsync(
            DateTimeOffset dateTimeOffset,
            CloudTable credentialsTable,
            string categoryId,
            CancellationToken cancellationToken)
        {
            var tokens = await Twitter.Utils.CreateTokenAsync(credentialsTable, categoryId);
            if (cancellationToken.IsCancellationRequested) return;
            var user = await tokens.Account.VerifyCredentialsAsync(false, true, false, cancellationToken: cancellationToken);
            if (cancellationToken.IsCancellationRequested) return;

            var list = await FetchLastTweetList(tokens, user.Id.Value, dateTimeOffset, TimeSpan.FromDays(7), cancellationToken);
            if (list == null || list.Count == 0) return;
            if (cancellationToken.IsCancellationRequested) return;
            
            // 같은 수가 여러개 있을수도 있는데... 근데 인용은 하나만 되니까 그냥 가자.
            list.Sort((l, r) => {
                if (l.RetweetCount > r.RetweetCount) return -1;
                if (l.RetweetCount < r.RetweetCount) return 1;
                if (l.FavoriteCount > r.FavoriteCount) return -1;
                if (l.FavoriteCount < r.FavoriteCount) return 1;
                return 0;
            });
            var most = new List<Status>();
            for (int i = 0; i < 3; ++i)
            {
                if (i < list.Count && list[i].RetweetCount > 0)
                {
                    most.Add(list[i]);
                }
            }
            long? replyId = null;
            for (int i = 0; i < most.Count; ++i)
            {
                var status = most[i];
                string text = $"지난 한 주간 가장 많이 리트윗된 도서 {i + 1}위 ({status.RetweetCount}회)";
                string permalink = $"https://twitter.com/{user.ScreenName}/status/{status.Id.ToString()}";
                var r = await tokens.Statuses.UpdateAsync(text, replyId, attachment_url: permalink, cancellationToken: cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
                replyId = r.Id;
            }
            
            list.Sort((l, r) => {
                if (l.FavoriteCount > r.FavoriteCount) return -1;
                if (l.FavoriteCount < r.FavoriteCount) return 1;
                if (l.RetweetCount > r.RetweetCount) return -1;
                if (l.RetweetCount < r.RetweetCount) return 1;
                return 0;
            });
            most.Clear();
            for (int i = 0; i < 3; ++i)
            {
                if (i < list.Count && list[i].FavoriteCount > 0)
                {
                    most.Add(list[i]);
                }
            }
            replyId = null;
            for (int i = 0; i < most.Count; ++i)
            {
                var status = most[i];
                string text = $"지난 한 주간 가장 많이 좋아요 표시된 도서 {i + 1}위 ({status.FavoriteCount}회)";
                string permalink = $"https://twitter.com/{user.ScreenName}/status/{status.Id.ToString()}";
                var r = await tokens.Statuses.UpdateAsync(text, replyId, attachment_url: permalink, cancellationToken: cancellationToken);
                if (cancellationToken.IsCancellationRequested) return;
                replyId = r.Id;
            }
        }

        [FunctionName("WeeklyTimerTrigger")]
        public static async Task Run(
            [TimerTrigger("0 0 0 * * Sun")] TimerInfo myTimer,
            [Table("Credentials", "Twitter")] CloudTable credentialsTable,
            [Table("LineAccount")] CloudTable lineAccountTable,
            ILogger log,
            CancellationToken cancellationToken)
        {
            var now = DateTimeOffset.Now;

            log.LogInformation($"C# Weekly timer trigger function executed at: {now.ToString()}");

            var comicsTask = TweetWeeklyReportAsync(now, credentialsTable, Aladin.Const.CategoryID.Comics, cancellationToken);
            await comicsTask;
            //var lnovelTask = TweetWeeklyReportAsync(now, credentialsTable, Aladin.Const.CategoryID.LNovel, cancellationToken);
            //var itbookTask = TweetWeeklyReportAsync(now, credentialsTable, Aladin.Const.CategoryID.ITBook, cancellationToken);

            //await Task.WhenAll(comicsTask, lnovelTask, itbookTask);
        }
    }
}