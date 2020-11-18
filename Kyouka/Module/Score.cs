using Discord;
using Discord.Commands;
using Kyouka.Impl;
using System.Linq;
using System.Threading.Tasks;

namespace Kyouka.Module
{
    public class Score : ModuleBase
    {
        [Command("Add game"), RequireOwner]
        public async Task AddGame(string name, int type)
        {
            if (StaticObjects.Db.DoesScoreExists(name))
                await ReplyAsync("This game already exists.");
            else
            {
                await StaticObjects.Db.AddGameAsync(name, (ScoreType)type);
                await ReplyAsync("This game was added.");
            }
        }

        [Command("Add score"), RequireOwner]
        public async Task AddScore(string name, IGuildUser user, int value)
        {
            if (!StaticObjects.Db.DoesScoreExists(name))
                await ReplyAsync("This game doesn't exists.");
            else
            {
                await StaticObjects.Db.AddPlayerAsync(name, user.Id.ToString(), value);
                await ReplyAsync("This score was added.");
            }
        }

        [Command("Score")]
        public async Task GetScore()
        {
            var scores = StaticObjects.Db.GetScores();
            await ReplyAsync(embed: new EmbedBuilder
            {
                Description = string.Join("\n\n", scores.Select(x =>
                {
                    return x.Item1 + "\n" + string.Join("\n", x.Item2.Scores.Select(y => Context.Guild.GetUserAsync(ulong.Parse(y.Item1)).GetAwaiter().GetResult().ToString() + ": " + y.Item2));
                }))
            }.Build());
        }
    }
}
