using System.Collections.Generic;

namespace Kyouka.Impl
{
    public class Score
    {
        public Score(string id, ScoreType type)
        {
            this.id = id;
            Type = type;
        }

        public string id;

        public List<(string, int)> Scores;

        public ScoreType Type;
    }
}
