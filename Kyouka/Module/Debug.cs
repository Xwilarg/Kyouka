using Discord.Commands;
using System.Threading.Tasks;

namespace Kyouka.Module
{
    public class Debug : ModuleBase
    {
        [Command("Dump"), RequireOwner]
        public async Task Db(string name)
        {
            await ReplyAsync(await StaticObjects.Db.DumpAsync(name));
        }
    }
}