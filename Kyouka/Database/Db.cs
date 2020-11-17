using Kyouka.Impl;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Kyouka.Database
{
    public class Db
    {
        public Db()
        {
            _r = RethinkDB.R;
        }

        public async Task InitAsync(string dbName)
        {
            _dbName = dbName;
            _conn = await _r.Connection().ConnectAsync();
            if (!await _r.DbList().Contains(_dbName).RunAsync<bool>(_conn))
                _r.DbCreate(_dbName).Run(_conn);
            if (!await _r.Db(_dbName).TableList().Contains("Scores").RunAsync<bool>(_conn))
                _r.Db(_dbName).TableCreate("Scores").Run(_conn);

            _scores = new Dictionary<string, Score>();
            var tmp = (Cursor<Score>)await _r.Db(_dbName).Table("Scores").RunAsync<Score>(_conn);
            while (tmp.MoveNext())
                _scores.Add(tmp.Current.id, tmp.Current);
        }

        public bool DoesScoreExists(string name)
        {
            return _scores.ContainsKey(name);
        }

        public async Task AddGameAsync(string name, ScoreType type)
        {
            var score = new Score(name, type);
            _scores.Add(name, score);

            await _r.Db(_dbName).Table("Scores").Insert(score).RunAsync(_conn);
        }

        public async Task AddPlayerAsync(string name, string player, int value)
        {
            var score = _scores[name];

            score.Scores.Add((player, value));
            await _r.Db(_dbName).Table("Scores").Update(_r.HashMap("id", name)
                .With("Scores", score.Scores)
            ).RunAsync(_conn);
        }

        public async Task<string> DumpAsync(string name)
        {
            return (await _r.Db(_dbName).Table("Scores").Get(name).RunAsync(_conn)).ToString();
        }

        public async Task<(string, Score)[]> GetScoresAsync()
        {
            return _scores.Select(x => (x.Key, x.Value)).ToArray();
        }

        public Dictionary<string, Score> _scores;

        private RethinkDB _r;
        private Connection _conn;
        private string _dbName;
    }
}
