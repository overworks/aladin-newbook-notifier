using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Mh.Functions.AladinNewBookNotifier
{
    /// <summary>알라딘 관련 유틸 모음</summary>
    public static class AladinUtils
    {
        public static string UnescapeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url.Replace(@"\\/", @"/").Replace(@"&amp;", @"&");
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
            sb.Append(Const.EndPoint_List + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }
            
            Uri uri = new Uri(new Uri(Const.Domain), sb.ToString());

            string response = await httpClient.GetStringAsync(uri);
            ItemListResult result = JsonConvert.DeserializeObject<ItemListResult>(response);

            return result;
        }

        /// <summary>알라딘 상품 조회 API를 사용하여 상품 정보를 가져옴</summary>
        /// <param name="itemId">상품 ID</param>
        public static async Task<ItemLookUpResult> LookUpItemAsync(HttpClient httpClient, string itemId)
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
            sb.Append(Const.EndPoint_LookUp + "?");
            foreach (var kvp in queryDict)
            {
                sb.Append(kvp.Key).Append("=").Append(kvp.Value).Append("&");
            }

            Uri uri = new Uri(new Uri(Const.Domain), sb.ToString());
            string response = await httpClient.GetStringAsync(uri);
            ItemLookUpResult result = JsonConvert.DeserializeObject<ItemLookUpResult>(response);

            return result;
        }

        public static string GetHQCoverUrl(this ItemLookUpResult.Item item)
        {
            return AladinUtils.UnescapeUrl(item.cover).Replace(@"/cover/", @"/cover500/");
        }
    }
}