using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DiscordUtils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Kyouka.Module;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;
using System.Collections.Generic;

namespace Kyouka
{
    public class Program
    {
        public static void Main(string[] args)
                  => new Program().MainAsync().GetAwaiter().GetResult();

        public DiscordSocketClient Client { private set; get; }
        private readonly CommandService _commands = new CommandService();

        public static Program P;
        public DateTime StartTime { private set; get; }

        private Program()
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Verbose,
            });
            Client.Log += Utils.Log;
            _commands.Log += Utils.LogErrorAsync;
        }

        private async Task MainAsync()
        {
            var json = JsonConvert.DeserializeObject<JObject>(File.ReadAllText("Keys/Credentials.json"));
            if (json["botToken"] == null)
                throw new NullReferenceException("Invalid Credentials file");

            P = this;

            await _commands.AddModuleAsync<Communication>(null);
            await _commands.AddModuleAsync<Score>(null);
            await _commands.AddModuleAsync<Debug>(null);

            Client.MessageReceived += HandleCommandAsync;
            Client.GuildAvailable += GuildAvailable;
            Client.Connected += Connected;

            StartTime = DateTime.Now;
            await Client.LoginAsync(TokenType.Bot, json["botToken"].Value<string>());
            await Client.StartAsync();

            await Task.Delay(-1);
        }

        Timer checkTimer, redditTimer;

        private async Task Connected()
        {
            checkTimer = new Timer(new TimerCallback(CheckRole), null, 0, 60 * 60 * 1000); // Called every hour
            redditTimer = new Timer(new TimerCallback(CheckSubreddit), null, 0, 600000); // Called every 10 minutes
        }

        private ulong regularRoleId = 692377699402121277;
        private ulong memberRoleId = 599014750306828318;
        private void CheckRole(object? _)
        {
            var users = StaticObjects.Db.GetUsersAsync().GetAwaiter().GetResult();
            var g = Client.Guilds.ElementAt(0); // Kyouka should only be in one guild anyway
            g.DownloadUsersAsync().GetAwaiter().GetResult();
            foreach (var user in g.Users)
            {
                if (!user.Roles.Any(x => x.Id == regularRoleId))
                    continue;
                var value = users.FirstOrDefault(x => x.id == user.Id.ToString());
                if (value == null || value.LastMessage.AddDays(3) < DateTime.Now)
                    user.RemoveRoleAsync(user.Guild.GetRole(regularRoleId)).GetAwaiter().GetResult();
            }
        }

        private Dictionary<string, ulong> _subreddits = new Dictionary<string, ulong>
        {
            { "0sanitymemes", 782542380670648320 },
            { "angryupvote", 782542455479205889 },
            { "arknights", 782413766146785280 },
            { "aww", 782542535322894336 },
            { "blackmagicfuckery", 784432035456221184 },
            { "blursedimages", 782542694580224030 },
            { "brandnewsentence", 784432098878029824 },
            { "coolguides", 783754079570231337 },
            { "dndmemes", 783754150055641128 },
            { "earthporn", 784432361017573426 },
            { "france", 782542756077764628 },
            { "gamephysics", 783754211795533876 },
            { "gamingdetails", 784432512495124511 },
            { "hmmm", 784433980249735168 },
            { "kdrama", 782542808354521129 },
            { "koreanvariety", 782542862234943528 },
            { "kpop", 782413900367790091 },
            { "music", 784432661225537556 },
            { "nextfuckinglevel", 784434309121310720 },
            { "programmerhumor", 784433512362016860 },
            { "rainbow6", 783754266644840538 },
            { "rance", 783754331554840616 },
            { "rareinsults", 782542929440931850 },
            { "showerthoughts", 784432835880026113 },
            { "suspiciouslyspecific", 782542972817637396 },
            { "technicallythetruth", 784433010048630804 },
            { "tumblr", 782543018598203442 },
            { "yuriknights", 782543062802497536 },
            { "wholesomehentai", 782543100462497793 },
            { "wholesomeyuri", 782543136853196801 }
        };

        private void CheckSubreddit(object? _)
        {
            foreach (var sub in _subreddits)
            {
                try
                {
                    var html = StaticObjects.Client.GetStringAsync("https://api.reddit.com/r/" + sub.Key + "/hot").GetAwaiter().GetResult();
                    var json = JsonConvert.DeserializeObject<JObject>(html)["data"]["children"].Value<JArray>();

                    var lasts = StaticObjects.Db.GetSubredditAsync(sub.Key).GetAwaiter().GetResult();

                    var g = Client.Guilds.ElementAt(0);
                    var chan = g.GetTextChannel(sub.Value);

                    foreach (var elem in json)
                    {
                        var data = elem["data"];

                        if (data["stickied"].Value<bool>())
                            continue;

                        if (lasts.Contains(data["name"].Value<string>()))
                            continue;

                        StaticObjects.Db.SaveSubredditAsync(sub.Key, data["name"].Value<string>()).GetAwaiter().GetResult();

                        string title = data["title"].Value<string>();
                        var embed = new EmbedBuilder()
                        {
                            Title = title.Length > 256 ? title.Substring(0, 256) : title,
                            Color = data["over_18"].Value<bool>() ? Color.Red : Color.Green,
                            Url = "https://reddit.com" + data["permalink"].Value<string>(),
                            Footer = new EmbedFooterBuilder
                            {
                                Text = data["link_flair_text"].Value<string>()
                            }
                        };
                        if (data["spoiler"].Value<bool>())
                        {
                            embed.Description = "Post is marked as spoiler";
                        }
                        else
                        {
                            string preview = data["url"].Value<string>();
                            if (!Utils.IsImage(preview.Split('.').Last()))
                                preview = data["thumbnail"].Value<string>();
                            embed.ImageUrl = !Uri.IsWellFormedUriString(preview, UriKind.Absolute) ? null : preview;
                            var selfText = data["selftext"];
                            if (selfText != null)
                                embed.Description = selfText.Value<string>().Length > 2048 ? selfText.Value<string>().Substring(0, 2048) : selfText.Value<string>();
                        }
                        chan.SendMessageAsync(embed: embed.Build()).GetAwaiter().GetResult();
                    }


                }
                catch (Exception e)
                {
                    Utils.LogErrorAsync(new LogMessage(LogSeverity.Error, e.Source, e.Message, e)).GetAwaiter().GetResult();
                }
            }
        }

        private async Task GuildAvailable(SocketGuild g)
        {
            _ = Task.Run(async () =>
            {
                Console.WriteLine("Downloading users");
                await g.DownloadUsersAsync();
                Console.WriteLine("Done downloading users");
            });
        }

        private async Task HandleCommandAsync(SocketMessage arg)
        {
            SocketUserMessage msg = arg as SocketUserMessage;
            if (msg == null) return;
            int pos = 0;
            if (!arg.Author.IsBot && (msg.HasMentionPrefix(Client.CurrentUser, ref pos) || msg.HasStringPrefix("k.", ref pos)))
            {
                SocketCommandContext context = new SocketCommandContext(Client, msg);
                var result = await _commands.ExecuteAsync(context, pos, null);
                if (!result.IsSuccess)
                {
                    Console.WriteLine(result.Error.ToString() + ": " + result.ErrorReason);
                }
            }
            if (msg.Author is IGuildUser guildUser)
            {
                await StaticObjects.Db.AddMessageAsync(msg.Author.Id.ToString());
                if (!guildUser.RoleIds.Contains(regularRoleId) && guildUser.RoleIds.Contains(memberRoleId))
                    await guildUser.AddRoleAsync(guildUser.Guild.GetRole(regularRoleId));
            }
        }
    }
}
