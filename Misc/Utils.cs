namespace Mh.Functions.AladinNewBookNotifier
{
    public static class Utils
    {
        public static string UnescapeUrl(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return url.Replace(@"\\/", @"/").Replace(@"&amp;", @"&");
            }
            return url;
        }
    }
}