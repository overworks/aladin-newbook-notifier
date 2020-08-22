using System.Net;

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
    }
}
