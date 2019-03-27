using Microsoft.WindowsAzure.Storage.Table;

namespace Mh.Functions.AladinNewBookNotifier
{
    public class BookEntity : TableEntity
    {
        public string Name { get; set; }
    }
}