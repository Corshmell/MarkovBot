using System;
using System.IO;
using Discord;
using Discord.Audio;
using Newtonsoft.Json;

namespace MarkovBot
{
    class Program
    {
        static bool exitSystem;

        public static DiscordClient Client;
        public static MarkovChainHelper MarkovChainHelper;
        private static JsonSettings _settings;

        public static string RootDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName;
        public static string JsonSettingsPath => RootDirectory + "/settings.json";

        private static void Main()
        {
            Client = new DiscordClient();
            _settings = JsonConvert.DeserializeObject<JsonSettings>(File.ReadAllText(JsonSettingsPath));
            MarkovChainHelper = new MarkovChainHelper(_settings.MarkovChainDepth);

            Client.MessageReceived += async (s, e) =>
            {
                Console.WriteLine(e.Channel.Name + "/" + e.User.Name + ": " + e.Message.Text);
                if (!e.Message.IsAuthor)
                {
                    if (!e.Message.IsMentioningMe())
                    {
                        MarkovChainHelper.Feed(e.Message);
                    }
                    else if (e.Message.IsMentioningMe() && e.Message.Text.Contains("saveJSON"))
                    {
                        MarkovChainHelper.Save();
                    }
                    else
                    {
                        await e.Channel.SendMessage(MarkovChainHelper.GetSequence());
                    }
                }
            };

            Client.ServerAvailable += async (s, e) =>
            {
                Console.WriteLine(await MarkovChainHelper.Initialize());
            };

            //Convert our sync method to an async one and block the Main function until the bot disconnects
            Client.ExecuteAndWait(async () =>
            {
                while (!exitSystem)
                {
                    await Client.Connect(_settings.Email,_settings.Password);
                    break;
                }
            });
        }

        // ReSharper disable once ClassNeverInstantiated.Local
        class JsonSettings
        {
            public string Email { get; set; }
            public string Password { get; set; }
            public int MarkovChainDepth { get; set; }
        }
    }
}
