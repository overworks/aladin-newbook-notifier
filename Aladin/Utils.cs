using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Mh.Functions.AladinNewBookNotifier.Aladin
{
    /// <summary>알라딘 관련 유틸 모음</summary>
    public static class Utils
    {
        /// <summary>링크 Url 수정</summary>
        public static string UnescapeUrl(string url)
        {
            if (!string.IsNullOrEmpty(url))
            {
                return WebUtility.HtmlDecode(url.Replace(@"\\/", @"/"));
            }
            return url;
        }

        public static async Task<ItemListResult> FetchItemListAsync(HttpClient httpClient, string categoryId)
        {
            string ttbKey = Environment.GetEnvironmentVariable("ALADIN_TTB_KEY");
            string partnerId = Environment.GetEnvironmentVariable("ALADIN_PARTNER_ID");

            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("querytype", "itemnewall");
            queryDict.Add("version", "20131101");
            queryDict.Add("cover", "big");
            queryDict.Add("output", "js");
            queryDict.Add("maxresults", "30");
            queryDict.Add("categoryid", categoryId);
            queryDict.Add("ttbkey", ttbKey);
            queryDict.Add("partner", partnerId);

            StringBuilder sb = new StringBuilder(256);
            sb.Append(Aladin.Const.EndPoint_List + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }
            
            Uri uri = new Uri(new Uri(Aladin.Const.Domain), sb.ToString());

            string response = await httpClient.GetStringAsync(uri);
            ItemListResult result = JsonConvert.DeserializeObject<ItemListResult>(response);

            return result;
        }

        /// <summary>알라딘 상품 조회 API를 사용하여 상품 정보를 가져옴</summary>
        /// <param name="itemId">상품 ID</param>
        public static async Task<ItemLookUpResult> LookUpItemAsync(HttpClient httpClient, int itemId)
        {
            string ttbKey = Environment.GetEnvironmentVariable("ALADIN_TTB_KEY");
            string partnerId = Environment.GetEnvironmentVariable("ALADIN_PARTNER_ID");

            Dictionary<string, string> queryDict = new Dictionary<string, string>();
            queryDict.Add("ttbkey", ttbKey);
            queryDict.Add("partner", partnerId);
            queryDict.Add("version", "20131101");
            queryDict.Add("cover", "big");
            queryDict.Add("output", "js");
            queryDict.Add("itemidtype", "itemid");
            queryDict.Add("itemid", itemId.ToString());
            
            StringBuilder sb = new StringBuilder(256);
            sb.Append(Aladin.Const.EndPoint_LookUp + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }

            Uri uri = new Uri(new Uri(Aladin.Const.Domain), sb.ToString());
            string response = await httpClient.GetStringAsync(uri);
            ItemLookUpResult result = JsonConvert.DeserializeObject<ItemLookUpResult>(response);

            return result;
        }

        public static string GetHQCoverUrl(this ItemLookUpResult.Item item)
        {
            return Utils.UnescapeUrl(item.cover).Replace(@"/cover/", @"/cover500/");
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
            return status + Utils.UnescapeUrl(item.link);
        }
    }
}