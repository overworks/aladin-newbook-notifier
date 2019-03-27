using System;
using System.Collections.Generic;

namespace Mh.Functions.AladinNewBookNotifier
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
        public IList<SubItem> paperBookList { get; set; }
        public IList<SubItem> ebookList { get; set; }
        public IList<FileFormat> fileFormatList { get; set; }
    }

    public class Product
    {
        public string title { get; set; }
        public string link { get; set; }
        public string author { get; set; }
        public string pubDate { get; set; }
        public string description { get; set; }
        public string isbn { get; set; }
        public string isbn13 { get; set; }
        public int itemId { get; set; }
        public int priceSales { get; set; }
        public int priceStandard { get; set; }
        public string mallType { get; set; }
        public string stockStatus { get; set; }
        public int mileage { get; set;}
        public string cover { get; set; }
        public int categoryId { get; set; }
        public string categoryName { get; set; }
        public string publisher { get; set; }
        public int salesPoint { get; set; }
        public bool fixedPrice { get; set; }
        public int customerReviewRank { get; set; }
        public SubInfo subInfo { get; set; }

        public string additionalInfo
        {
            get
            {
                string val = $" ({author} / {publisher} / {pubDate} / {priceStandard}원) ";

                // 자꾸 에러가 나서 왜 이러지 싶었더니 추가정보가 140자가 넘어간다;;;
                if (val.Length > 115)
                {
                    val = $" ({author} / {publisher} / {priceStandard}원) ";
                    
                    if (val.Length > 115)
                    {
                        val = $" ({author} / {publisher}) ";

                        if (val.Length > 115)
                        {
                            val = $" ({author}) ";
                        }
                    }
                }
                return val;
            }
        }

        public override string ToString()
        {
            string additionalInfo = this.additionalInfo; // 여러번 계산할 수도 있으니...
            string status = title + additionalInfo;

            // 트위터는 140자까지이나, 링크가 23자를 차지하므로 117까지 계산해서 카운트한다.
            if (status.Length > 117)
            {
                int maxLength = 117 - additionalInfo.Length;
                if (maxLength > 2)
                {
                    status = title.Substring(0, maxLength - 1) + "…" + additionalInfo;
                }
                else
                {
                    status = title + " ";
                    
                    if (status.Length > 117)
                    {
                        status = title.Substring(0, 115) + "… ";
                    }
                }
            }
            status += link.Replace(@"\\/", @"/").Replace(@"&amp;", @"&");

            return status;
        }
    }

    public class ProductList
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
        public IList<Product> item { get; set; }
    }
}