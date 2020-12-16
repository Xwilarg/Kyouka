using Kyouka.Database;
using System;
using System.Net.Http;

namespace Kyouka
{
    public static class StaticObjects
    {
        public static Db Db { get; }
        public static HttpClient Client { get; } = new HttpClient();
        public static Random Rand { get; } = new Random();

        static StaticObjects()
        {
            Client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 Kyouka");

            Db = new Db();
            Db.InitAsync("Kyouka").GetAwaiter().GetResult();
        }
    }
}
