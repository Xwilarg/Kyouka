using Kyouka.Impl;
using RethinkDb.Driver;
using RethinkDb.Driver.Net;
using System;
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
            if (!await _r.Db(_dbName).TableList().Contains("Users").RunAsync<bool>(_conn))
                _r.Db(_dbName).TableCreate("Users").Run(_conn);
            if (!await _r.Db(_dbName).TableList().Contains("Reddit").RunAsync<bool>(_conn))
                _r.Db(_dbName).TableCreate("Reddit").Run(_conn);

            _scores = new Dictionary<string, Score>();
            var tmp = (Cursor<Score>)await _r.Db(_dbName).Table("Scores").RunAsync<Score>(_conn);
            while (tmp.MoveNext())
                _scores.Add(tmp.Current.id, tmp.Current);

            _subreddits = new Dictionary<string, Subreddit>();
            var tmp2 = (Cursor<Subreddit>)await _r.Db(_dbName).Table("Reddit").RunAsync<Subreddit>(_conn);
            while (tmp2.MoveNext())
                _subreddits.Add(tmp2.Current.id, tmp2.Current);
        }

        public async Task<string> GetSubredditAsync(string name)
        {
            if (_subreddits.ContainsKey(name))
                return _subreddits[name].Last;
            return null;
        }

        public async Task SaveSubredditAsync(string name, string last)
        {
            if (_subreddits.ContainsKey(name))
            {
                _subreddits[name].Last = last;
                await _r.Db(_dbName).Table("Reddit").Update(_r.HashMap("id", name)
                    .With("Last", last)
                ).RunAsync(_conn);
            }
            else
            {
                Subreddit sub = new Subreddit
                {
                    id = name,
                    Last = last
                };
                _subreddits.Add(name, sub);
                await _r.Db(_dbName).Table("Reddit").Insert(sub).RunAsync(_conn);
            }
        }

        public async Task AddMessageAsync(string user)
        {
            if (await _r.Db(_dbName).Table("Users").GetAll(user).Count().Eq(0).RunAsync<bool>(_conn))
                await _r.Db(_dbName).Table("Users").Insert(_r.HashMap("id", user)
                    .With("LastMessage", DateTime.Now)
                ).RunAsync(_conn);
            else
                await _r.Db(_dbName).Table("Users").Update(_r.HashMap("id", user)
                    .With("LastMessage", DateTime.Now)
                ).RunAsync(_conn);
        }

        public async Task<User[]> GetUsersAsync()
        {
            var tmp = (Cursor<User>)await _r.Db(_dbName).Table("Users").RunAsync<User>(_conn);
            var users = new List<User>();
            while (tmp.MoveNext())
                users.Add(tmp.Current);
            return users.ToArray();
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

        public (string, Score)[] GetScores()
        {
            return _scores.Select(x => (x.Key, x.Value)).ToArray();
        }

        public Dictionary<string, Score> _scores;
        public Dictionary<string, Subreddit> _subreddits;

        private RethinkDB _r;
        private Connection _conn;
        private string _dbName;
    }
}
