using Microsoft.WindowsAzure.Storage.Table;

namespace Mh.Functions.AladinNewBookNotifier
{
    public class BookEntity : TableEntity
    {
        public string Name { get; set; }
    }

    public class CredentialsEntity : TableEntity
    {
        public string AccessToken { get; set; }
        public string AccessTokenSecret { get; set; }
    }
}