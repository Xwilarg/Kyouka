using System.Collections.Generic;

namespace Kyouka.Impl
{
    public class Subreddit
    {
        public string id;
        public List<string> Lasts = new List<string>();

        public void AddLast(string value)
        {
            Lasts.Add(value);
            if (Lasts.Count > 100)
                Lasts.RemoveAt(0);
        }
    }
}
