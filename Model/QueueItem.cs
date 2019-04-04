using System.Collections.Generic;

namespace Mh.Functions.AladinNewBookNotifier
{
    public class QueueItem
    {
        public string CategoryId { get; set; }
        public IList<int> ItemList { get; set; }

        public QueueItem(string categoryId)
        {
            CategoryId = categoryId;
            ItemList = new List<int>();
        }
    }
}