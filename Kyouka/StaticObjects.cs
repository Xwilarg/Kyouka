using Kyouka.Database;

namespace Kyouka
{
    public static class StaticObjects
    {
        public static Db Db { get; }

        static StaticObjects()
        {
            Db = new Db();
            Db.InitAsync("Kyouka").GetAwaiter().GetResult();
        }
    }
}
