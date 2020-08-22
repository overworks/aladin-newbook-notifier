using System.Collections.Generic;

namespace Mh.Functions.AladinNewBookNotifier.Models
{
    public class QueueItem
    {
        public string CategoryId { get; set; }
        public List<int> ItemList { get; set; }

        public QueueItem(string categoryId)
        {
            CategoryId = categoryId;
            ItemList = new List<int>();
        }
    }
}