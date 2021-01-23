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
using System.Web;
using System.Text;
using System.Text.RegularExpressions;

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

        private Dictionary<int, (string, string)[]> _jlpt;

        public string[] JapaneseLines;
        public (string, string)[] JapaneseSentences;

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

            InitDicts();
            _jlpt = new Dictionary<int, (string, string)[]>();
            for (int i = 1; i <= 5; i++)
            {
                var full = File.ReadAllLines("Data/Jlpt" + i + "Vocabulary.txt");
                _jlpt.Add(i, full.Select((x) =>
                {
                    var split = x.Split('$').ToArray();
                    return (split[0], split[1]);
                }).ToArray());
            }
            P = this;

            JapaneseLines = File.ReadAllLines("Data/Japanese.txt");
            var lines = File.ReadAllLines("Data/Sentences.txt");
            JapaneseSentences = new (string, string)[lines.Length / 2];
            for (int i = 0; i < lines.Length; i += 2)
            {
                var curr = lines[i].Split('\t');
                JapaneseSentences[i / 2] = (curr[0].Replace("A: ", ""), curr[1].Split('#')[0]);
            }

            await _commands.AddModuleAsync<Communication>(null);
            await _commands.AddModuleAsync<Score>(null);
            await _commands.AddModuleAsync<Debug>(null);
            await _commands.AddModuleAsync<Module.Game>(null);

            Client.MessageReceived += HandleCommandAsync;
            Client.GuildAvailable += GuildAvailable;
            Client.Connected += Connected;

            StartTime = DateTime.Now;
            await Client.LoginAsync(TokenType.Bot, json["botToken"].Value<string>());
            await Client.StartAsync();

            await Task.Delay(-1);
        }

        Timer checkTimer, redditTimer;
        Timer japaneseTimer;

        private async Task Connected()
        {
            checkTimer = new Timer(new TimerCallback(CheckRole), null, 0, 60 * 60 * 1000); // Called every hour
            redditTimer = new Timer(new TimerCallback(CheckSubreddit), null, 0, 600000); // Called every 10 minutes
            japaneseTimer = new Timer(new TimerCallback(PostJapanese), null, 0, 60000); // Called every minute
        }

        private ulong regularRoleId = 692377699402121277;
        private ulong memberRoleId = 599014750306828318;
        private ulong japaneseChannel = 788851808382353488;


        public static Dictionary<string, string> HiraganaToRomaji { set; get; } = new Dictionary<string, string>();
        public static Dictionary<string, string> KatakanaToRomaji { set; get; } = new Dictionary<string, string>();

        private void InitDicts()
        {
            var RomajiToHiragana = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("LanguageResource/Hiragana.json"));
            foreach (var elem in RomajiToHiragana)
            {
                HiraganaToRomaji.Add(elem.Value, elem.Key);
            }
            var RomajiToKatakana = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText("LanguageResource/Katakana.json"));
            foreach (var elem in RomajiToKatakana)
            {
                KatakanaToRomaji.Add(elem.Value, elem.Key);
            }
        }

        // From: https://github.com/Xwilarg/Sanara/blob/master/SanaraV3/Module/Tool/LanguageModule.cs#L297
        public static string ConvertLanguage(string entry, Dictionary<string, string> dictionary, char doubleChar)
        {
            StringBuilder result = new StringBuilder();
            var biggest = dictionary.Keys.OrderByDescending(x => x.Length).First().Length;
            bool isEntryRomaji = IsLatinLetter(dictionary.Keys.First()[0]);
            bool doubleNext; // If we find a doubleChar, the next character need to be doubled (っこ -> kko)
            while (entry.Length > 0)
            {
                doubleNext = false;

                // SPECIAL CASES FOR KATAKANA
                if (entry[0] == 'ー') // We can't really convert this katakana so we just ignore it
                {
                    entry = entry.Substring(1);
                    continue;
                }
                if (entry[0] == 'ァ' || entry[0] == 'ィ' || entry[0] == 'ゥ' || entry[0] == 'ェ' || entry[0] == 'ォ')
                {
                    result.Remove(result.Length - 1, 1);
                    char tmp;
                    switch (entry[0])
                    {
                        case 'ァ': tmp = 'a'; break;
                        case 'ィ': tmp = 'i'; break;
                        case 'ゥ': tmp = 'u'; break;
                        case 'ェ': tmp = 'e'; break;
                        case 'ォ': tmp = 'o'; break;
                        default: throw new ArgumentException("Invalid katakana " + entry[0]);
                    }
                    result.Append(tmp);
                    entry = entry.Substring(1);
                    continue;
                }

                if (entry.Length >= 2 && entry[0] == entry[1] && isEntryRomaji) // kko -> っこ
                {
                    result.Append(doubleChar);
                    entry = entry.Substring(1);
                    continue;
                }
                if (entry[0] == doubleChar)
                {
                    doubleNext = true;
                    entry = entry.Substring(1);
                    if (entry.Length == 0)
                        continue;
                }
                // Iterate on biggest to 1 (We assume that 3 is the max number of character)
                // We then test for each entry if we can convert
                // We begin with the biggest, if we don't do so, we would find ん (n) before な (na)
                for (int i = biggest; i > 0; i--)
                {
                    if (entry.Length >= i)
                    {
                        var value = entry[0..i];
                        if (dictionary.ContainsKey(value))
                        {
                            if (doubleNext)
                                result.Append(dictionary[value][0]);
                            result.Append(dictionary[value]);
                            entry = entry.Substring(i);
                            goto found;
                        }
                    }
                }
                result.Append(entry[0]);
                entry = entry.Substring(1);
            found:;
            }
            return result.ToString();
        }

        private static bool IsLatinLetter(char c)
            => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        public static string ToRomaji(string entry)
           => ConvertLanguage(ConvertLanguage(entry, KatakanaToRomaji, 'ッ'), HiraganaToRomaji, 'っ');

        private void PostJapanese(object? _)
        {
            if (StaticObjects.Db.CanPostJapanese())
            {
                var nb = StaticObjects.Rand.Next(100);
                int jlpt;
                if (nb > 50) jlpt = 5;
                else if (nb > 25) jlpt = 4;
                else if (nb > 15) jlpt = 3;
                else if (nb > 10) jlpt = 2;
                else jlpt = 1;

                var content = _jlpt[jlpt];
                var randomLine = content[StaticObjects.Rand.Next(content.Length)];

                JObject json = JsonConvert.DeserializeObject<JObject>(StaticObjects.Client.GetStringAsync("http://jisho.org/api/v1/search/words?keyword="
                + HttpUtility.UrlEncode(randomLine.Item1)).GetAwaiter().GetResult());
                var data = ((JArray)json["data"]).Select(x => x).ToArray()[0];

                var g = Client.Guilds.ElementAt(0);
                var chan = g.GetTextChannel(japaneseChannel);

                var slug = data["slug"].Value<string>();
                slug = Regex.Replace(slug, "-[0-9]+", "");

                List<(string, string)> examples = new List<(string, string)>();
                var valids = JapaneseSentences.Where(x => x.Item1.Contains(slug)).ToList();
                while (valids.Count > 0 && examples.Count < 5)
                {
                    var random = StaticObjects.Rand.Next(valids.Count);
                    examples.Add(valids[random]);
                    valids.RemoveAt(random);
                }

                var list = new List<EmbedFieldBuilder>();
                if (randomLine.Item1 != slug)
                    list.Add(new EmbedFieldBuilder
                    {
                        Name = "Kanji",
                        Value = slug,
                        IsInline = true
                    });
                list.Add(new EmbedFieldBuilder
                {
                    Name = "JLPT Level",
                    Value = jlpt,
                    IsInline = true
                });
                var romaji = ToRomaji(randomLine.Item1);
                list.Add(new EmbedFieldBuilder
                {
                    Name = "Romaji",
                    Value = "||" + romaji + "||",
                    IsInline = true
                });
                int index = 1;
                foreach (var e in examples)
                {
                    list.Add(new EmbedFieldBuilder
                    {
                        Name = "Example n°" + index,
                        Value = e.Item1 + "\n||" + e.Item2 + "||"
                    });
                    index++;
                }
                var lines = File.ReadAllLines("Data/Japanese.txt").ToList();
                if (!lines.Any(x => x.Split('$')[1] == randomLine.Item1))
                    lines.Add(DateTime.Now.ToString("yyyy/MM/dd") + "$" + randomLine.Item1 + "$" + randomLine.Item2 + "$" + (randomLine.Item1 != slug ? slug : ""));
                JapaneseLines = lines.ToArray();
                File.WriteAllLines("Data/Japanese.txt", lines.ToArray());
                chan.SendMessageAsync(embed: new EmbedBuilder
                {
                    Color = Color.Blue,
                    Title = randomLine.Item1,
                    Url = "https://jisho.org/word/" + slug,
                    Description = randomLine.Item2,
                    Fields = list
                }.Build());

                StaticObjects.Db.UpdatePostJapaneseAsync().GetAwaiter().GetResult();
            }
        }

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
            { "arknuts", 788491069616029716 },
            { "badtiming", 788485890816868379 },
            { "blursedimages", 782542694580224030 },
            { "brandnewsentence", 784432098878029824 },
            { "coolguides", 783754079570231337 },
            { "dndmemes", 783754150055641128 },
            { "hololive", 784953090750677012 },
            { "hololiveyuri", 787083981740048394 },
            { "hololewd", 788491424978829392 },
            { "jacksepticeye", 788494503832584203 },
            { "kdrama", 782542808354521129 },
            { "kpop", 782413900367790091 },
            { "markiplier", 788487021193855006 },
            { "programmerhumor", 784433512362016860 },
            { "rainbow6", 783754266644840538 },
            { "showerthoughts", 784432835880026113 },
            { "technicallythetruth", 784433010048630804 },
            { "tf2", 791816386640740353 },
            { "upliftingnews", 788486214873120860 },
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
