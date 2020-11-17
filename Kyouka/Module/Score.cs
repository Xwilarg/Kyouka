using Discord;
using Discord.Commands;
using Kyouka.Impl;
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
    }
}
