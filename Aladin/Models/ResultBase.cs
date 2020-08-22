using System;

namespace Mh.Functions.AladinNewBookNotifier.Aladin.Models
{
    public class ResultBase
    {
        public string version { get; set; }
        public string logo { get; set; }
        public string title { get; set; }
        public string link { get; set; }
        public DateTime pubDate { get; set; }
        public int totalResults { get; set; }
        public int startIndex { get; set; }
        public int itemsPerPage { get; set; }
        public string query { get; set; }
        public int searchCategoryId { get; set; }
        public string searchCategoryName { get; set; }
    }
}