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
            if (json["botToken"] == null || json["regularRoleId"] == null)
                throw new NullReferenceException("Invalid Credentials file");

            P = this;

            regularRoleId = ulong.Parse(json["regularRoleId"].Value<string>());

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

        private ulong regularRoleId;
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
            { "angryupvote", 782358498473148478 },
            { "aww", 782358954415226920 },
            { "kdrama", 782358994592858132 },
            { "koreanvariety", 782359043373137960 },
            { "kpop", 782359132304572426 },
            { "rareinsults", 782359169487470672 },
            { "suspiciouslyspecific", 782359214415675402 },
            { "tumblr", 782359259391197214 }
        };

        private void CheckSubreddit(object? _)
        {
            foreach (var sub in _subreddits)
            {
                try
                {
                    var html = StaticObjects.Client.GetStringAsync("https://api.reddit.com/r/" + sub.Key + "/hot").GetAwaiter().GetResult();
                    var json = JsonConvert.DeserializeObject<JObject>(html)["data"]["children"].Value<JArray>();

                    var last = StaticObjects.Db.GetSubredditAsync(sub.Key).GetAwaiter().GetResult();
                    var first = json[0]["data"]["name"].Value<string>();
                    if (first == last)
                        continue;

                    var g = Client.Guilds.ElementAt(0);
                    var chan = g.GetTextChannel(sub.Value);
                    StaticObjects.Db.SaveSubredditAsync(sub.Key, first).GetAwaiter().GetResult();

                    foreach (var elem in json)
                    {
                        var data = elem["data"];

                        if (data["stickied"].Value<bool>())
                            continue;

                        if (data["name"].Value<string>() == last)
                            break;

                        var embed = new EmbedBuilder()
                        {
                            Title = data["title"].Value<string>(),
                            Color = data["over_18"].Value<bool>() ? Color.Red : Color.Green,
                            Url = "https://reddit.com" + data["permalink"].Value<string>()
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
                            embed.Footer = new EmbedFooterBuilder
                            {
                                Text = data["link_flair_text"].Value<string>()
                            };
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
                if (!guildUser.RoleIds.Contains(regularRoleId))
                    await guildUser.AddRoleAsync(guildUser.Guild.GetRole(regularRoleId));
            }
        }
    }
}
