using Discord.Commands;
using Kyouka.Game;
using Newtonsoft.Json;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kyouka.Module
{
    public class Game : ModuleBase
    {
        [Command("Play")]
        public async Task PlayAsync()
        {
            var data = File.ReadAllLines("Data/Japanese.txt");
            var game = new CustomGame
            {
                Name = "Japanese Learning Quizz",
                Rules = "I'll give you words in hiragana/katakana, you must guest the meaning",
                Questions = data.Select((x) =>
                {
                    var split = x.Split('$');
                    if (StaticObjects.Rand.Next(0, 2) == 0)
                    {
                        string question;
                        if (split[3] != "")
                            question = split[3] + " (" + split[1] + ")";
                        else
                            question = split[1];
                        return new CustomQuestion
                        {
                            Question = question,
                            Answers = split[2].Split(',').Select(x => x.Trim('"')).ToArray()
                        };
                    }
                    else
                    {
                        return new CustomQuestion
                        {
                            Question = split[2],
                            Answers = new[] { split[1], Program.ToRomaji(split[1]) }
                        };
                    }
                }).ToArray()
            };
            byte[] arr = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(game));
            await Context.Channel.SendFileAsync(new MemoryStream(arr), "quizz.json", "s.play custom");
        }
    }
}
