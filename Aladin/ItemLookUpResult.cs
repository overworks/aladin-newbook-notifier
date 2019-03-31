using System;
using System.Collections;
using System.Collections.Generic;

namespace Mh.Functions.AladinNewBookNotifier.Aladin
{
    public class ItemLookUpResult
    {
        public class SubItem
        {
            public int itemId { get; set; }
            public string isbn { get; set; }
            public int priceSales { get; set; }
            public string link { get; set; }
        }

        public class FileFormat
        {
            public string fileType { get; set; }
            public int fileSize { get; set; }
        }

        public class SubInfo
        {
            public IList<SubItem> ebookList { get; set; }
            public IList<FileFormat> fileFormatList { get; set; }
            public string subTitle { get; set; }
            public string originalTitle { get; set; }
            public int itemPage { get; set; }
        }

        public class Item
        {
            public string title { get; set; }
            public string link { get; set; }
            public string author { get; set; }
            public string pubDate { get; set; }
            public string description { get; set; }
            public string creator { get; set; }
            public string isbn { get; set; }
            public string isbn13 { get; set; }
            public int itemId { get; set; }
            public int priceSales { get; set; }
            public int priceStandard { get; set; }
            public string mallType { get; set; }
            public string stockStatus { get; set; }
            public int mileage { get; set; }
            public string cover { get; set; }
            public int caegoryId { get; set; }
            public string categoryName { get; set; }
            public string publisher { get; set; }
            public int salesPoint { get; set; }
            public bool adult { get; set; }
            public int customerReviewRank { get; set; }
            public SubInfo subInfo { get; set; }
        }
        
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
        public IList<Item> item { get; set; }
    }
}