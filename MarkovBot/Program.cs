using System;
using System.IO;
using System.Runtime.InteropServices;
using Discord;
using Newtonsoft.Json;

namespace MarkovBot
{
    class Program
    {
        static bool exitSystem;

        #region Trap application termination
        [DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static bool Handler(CtrlType sig)
        {
            Console.WriteLine("Exiting system due to external CTRL-C, or process kill, or shutdown");

            //cleanup here
            MarkovChainHelper.Save();

            Console.WriteLine("Cleanup complete");

            //allow main to run off
            exitSystem = true;

            //shutdown right away so there are no lingering threads
            Environment.Exit(-1);

            return true;
        }
        #endregion

        public static DiscordClient Client;
        public static MarkovChainHelper MarkovChainHelper;
        private static JsonSettings _settings;

        public static string RootDirectory = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName;
        public static string JsonSettingsPath => RootDirectory + "\\settings.json";

        private static void Main()
        {
            _handler += Handler;
            SetConsoleCtrlHandler(_handler, true);

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

            Client.LoggedIn += async (s, e) =>
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
