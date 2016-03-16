using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Newtonsoft.Json;
using TextMarkovChains;

namespace MarkovBot
{
    public class MarkovChainHelper
    {
        public static int Depth;
        private static bool _isInitialized;
        private static IMarkovChain _markovChain;
        private static DiscordClient _client;
        private static List<JsonMessage> JsonList = new List<JsonMessage>();

        public static string RootDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName;
        public static string JsonMessageFileLocation => RootDirectory + "\\messages.zip";


        public MarkovChainHelper(int depth) : this(Program.Client, depth)
        {
        }

        public MarkovChainHelper(DiscordClient client, int depth)
        {
            _client = client;
            Depth = depth;
            if (depth == 1)
            {
                _markovChain = new TextMarkovChain();
            }
            else if (depth == 2)
            {
                _markovChain = new DeepMarkovChain();
            }
            else
            {
                _markovChain = new MultiDeepMarkovChain(Depth);
            }

        }

        public async Task<bool> Initialize()
        {
            if (!_isInitialized)
            {
                if (File.Exists(path: JsonMessageFileLocation))
                {
                    JsonList =
                        JsonConvert.DeserializeObject<List<JsonMessage>>(Open());
                    ulong id = JsonList.Last(item => item.CI == 85842104034541568).MI;
                    foreach (JsonMessage message in JsonList)
                    {
                        _markovChain.feed(message.M);
                            //Any messages here have already been thru all the if checks, and hence, we dont need to run thru all of those again.
                    }
                    _isInitialized = true;

                    List<Message> list = await DownloadMessagesAfterId(id, _client.GetChannel(85842104034541568));
                    foreach (Message message in list)
                    {
                        FeedMarkovChain(message);
                    }
                }
                else
                {
                    List<Message> list = await GetMessagesFromChannel(_client.GetChannel(85842104034541568), 10000);
                    list.AddRange(await GetMessagesFromChannel(_client.GetChannel(96786127238725632), 2000));
                    list.AddRange(await GetMessagesFromChannel(_client.GetChannel(94122326802571264), 2000));
                    foreach (Message message in list)
                    {
                        if (message != null && !message.Text.Equals(""))
                        {
                            FeedMarkovChain(message);
                            var json = new JsonMessage
                            {
                                M = message.Text,
                                MI = message.Id,
                                CI = message.Channel.Id,
                            };
                            JsonList.Add(json);
                        }
                    }
                }
                return _isInitialized = true;
            }
            return _isInitialized;
        }

        private async Task<List<Message>> DownloadMessagesAfterId(ulong id, Channel channel)
        {
            List<Message> messages = new List<Message>();
            var latestMessage = await channel.DownloadMessages(100, id, Relative.After);
            messages.AddRange(latestMessage);

            ulong tmpMessageTracker = latestMessage.Last().Id;

            while (true)
            {
                messages.AddRange(await channel.DownloadMessages(100, tmpMessageTracker, Relative.After));

                var newMessageTracker = messages[messages.Count - 1].Id;
                if (tmpMessageTracker != newMessageTracker)
                    //Checks if there are any more messages in channel, and if not, returns the List
                {
                    tmpMessageTracker = newMessageTracker;
                        //grabs the last message added, and uses the new Id as the start of the next query
                }
                else
                {
                    messages.RemoveAt(messages.Count - 1); //removes the excessive object
                    return messages;
                }
            }
        }

        public void Feed(Message message)
        {
            FeedMarkovChain(message);
        }

        public string GetSequence()
        {
            if (_isInitialized)
            {
                return _markovChain.generateSentence();
            }
            return "I'm not ready yet Senpai!";
        }

        private void FeedMarkovChain(Message message)
        {
            if (!message.User.Name.ToLower().Contains(Program.Client.CurrentUser.Name.ToLower()))
            {
                if (!message.Text.Equals("") && !message.Text.Equals(" ") && !message.Text.Contains("http") &&
                    !message.Text.ToLower().Contains("testmarkov"))
                {
                    if (message.Text.Contains("."))
                    {
                        _markovChain.feed(message.Text);
                    }
                    _markovChain.feed(message.Text + ".");

                    var json = new JsonMessage
                    {
                        M = message.Text,
                        MI = message.Id,
                        CI = message.Channel.Id,
                    };
                    JsonList.Add(json);
                }
            }
        }

        private async Task<List<Message>> GetMessagesFromChannel(Channel channel, int i)
        {
            List<Message> messages = new List<Message>();
            var latestMessage = await channel.DownloadMessages(i);
            messages.AddRange(latestMessage);

            ulong tmpMessageTracker = latestMessage.Last().Id;

            while (messages.Count < i)
            {
                messages.AddRange(await channel.DownloadMessages(100, tmpMessageTracker));

                ulong newMessageTracker = messages[messages.Count - 1].Id;
                if (tmpMessageTracker != newMessageTracker)
                    //Checks if there are any more messages in channel, and if not, returns the List
                {
                    tmpMessageTracker = newMessageTracker;
                        //grabs the last message added, and uses the new Id as the start of the next query
                }
                else
                {
                    messages.RemoveAt(messages.Count - 1); //removes the last duplicate object
                    return messages;
                }
            }
            return messages;
        }

        public void Save()
        {
            var text = Encoding.Default.GetBytes(JsonConvert.SerializeObject(JsonList, Newtonsoft.Json.Formatting.None));
            using (var fileStream = File.Open(JsonMessageFileLocation, FileMode.OpenOrCreate))
            {
                using (var stream = new GZipStream(fileStream, CompressionMode.Compress))
                {
                    stream.Write(text, 0, text.Length); // Write to the `stream` here and the result will be compressed
                }
            }
        }

        private string Open()
        {
            byte[] file = File.ReadAllBytes(JsonMessageFileLocation);
            using (var stream = new GZipStream(new MemoryStream(file), CompressionMode.Decompress))
            {
                const int size = 4096;
                byte[] buffer = new byte[size];

                using (MemoryStream memory = new MemoryStream())
                {
                    int count = 0;
                    do
                    {
                        count = stream.Read(buffer, 0 , size);
                        if (count > 0)
                        {
                            memory.Write(buffer, 0, count);
                        }
                    } while (count > 0);
                    return Encoding.Default.GetString(memory.ToArray());
                }
            }
        }

        class JsonMessage
        {
            public string M { get; set; }
            public ulong MI { get; set; }
            public ulong CI { get; set; }
        }
    }
}
