using Kyouka.Database;
using System.Net.Http;

namespace Kyouka
{
    public static class StaticObjects
    {
        public static Db Db { get; }
        public static HttpClient Client { get; } = new HttpClient();

        static StaticObjects()
        {
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 Kyouka");

            Db = new Db();
            Db.InitAsync("Kyouka").GetAwaiter().GetResult();
        }
    }
}
