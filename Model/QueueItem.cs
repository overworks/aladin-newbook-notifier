using System.Collections.Generic;

namespace Mh.Functions.AladinNewBookNotifier
{
    public class QueueItem
    {
        public string Category { get; set; }
        public IList<int> ItemList { get; set; }

        public QueueItem(string category)
        {
            Category = category;
            ItemList = new List<int>();
        }
    }
}