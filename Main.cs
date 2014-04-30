using System;
using System.Collections.Concurrent;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Newtonsoft.Json;
using System.Web;
using System.Net;



namespace TSWVote
{
    [ApiVersion(1, 15)]
    public class TSWVote : TerrariaPlugin
    {

        private const int NumberOfWebClientsAvailable = 15;

        private ConcurrentQueue<WebClient> webClientQueue;

        public string path = Path.Combine(TShock.SavePath, "TSWVote.txt");
        public override string Name
        {
            get
            {
                return "TServerWebVote";
            }
        }

        public override string Author
        {
            get
            {
                return "Loganizer & XGhozt (Special thanks to CytoDev, Simon311, Ijwu, Khoatic)";
            }
        }

        public override string Description
        {
            get
            {
                return "A plugin to vote to TServerWeb in-game.";
            }
        }

        public override Version Version
        {
            get
            {
                return new Version("2.1");
            }
        }

        public TSWVote(Main game)
            : base(game)
        {
            Order = 1000;

            this.webClientQueue = new ConcurrentQueue<WebClient>();
            for (int i = 0; i < NumberOfWebClientsAvailable; i++)
            {
                WebClient webClient = new WebClient();
                webClient.Proxy = null;
                webClient.Headers.Add("user-agent", "TServerWeb Vote Plugin");
                webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;

                this.webClientQueue.Enqueue(webClient);
            }
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerChat.Register(this, OnChat);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerChat.Deregister(this, OnChat);

                if (this.webClientQueue != null)
                {
                    WebClient webClient;
                    while (this.webClientQueue.TryDequeue(out webClient))
                    {
                        webClient.DownloadStringCompleted -= WebClient_DownloadStringCompleted;
                        webClient.Dispose();
                    }
                }
            }
            base.Dispose(disposing);
        }

        private void OnChat(ServerChatEventArgs args)
        {
            //check if its chat packet otherwise ignore
            if (string.IsNullOrWhiteSpace(args.Text) || !args.Text.StartsWith("/"))
            {
                return;
            }
            //make sure player exists and isn't null
            var player = TShock.Players[args.Who];
            //make sure message passed isn't null
            if (player == null)
            {
                return;
            }
            //remove '/'
            string cmdText = args.Text.Remove(0, 1);
            //change message into list of params
            var cmdArgs = cmdText.Split(' ').ToList();
            //get command name
            string cmdName = cmdArgs[0].ToLower();
            //remove the command name
            cmdArgs.RemoveAt(0);
            //check if its vote
            if (cmdName != "vote")
            {
                return;
            }

            if (cmdArgs.Count > 0)
            {
                Vote(new CommandArgs(args.Text, player, new List<string> { string.Join(" ", cmdArgs) }));

            }
            else
            {
                Vote(new CommandArgs(args.Text, player, new List<string>()));
            }
            args.Handled = true;
        }

        public void OnInitialize(EventArgs args)
        {
            if (!File.Exists(Path.Combine(TShock.SavePath, "TSWVote.txt")))
            {
                string[] text = {"**This is the configuration file, please do not edit.**", "Help page: http://www.tserverweb.com/help/",
                                    "Server ID is on next line. Please DO NOT edit the following line, change it using \"/tserverweb [ID] in-game\"",
                                "0"};
                File.WriteAllLines(Path.Combine(TShock.SavePath, "TSWVote.txt"), text);
            }
            else
            {
                int id;
                string message;
                if (!GetServerID(out id, out message))
                    SendError("Configuration", message);
            }
            if (TShock.Config.RestApiEnabled == false)
            {
                SendError("REST API", "REST API Not Enabled! TSWVote plugin will not load!");
            }
            else
            {

                Commands.ChatCommands.Add(new Command("vote.changeid", ChangeID, "tserverweb"));
                // This is so it will tell you if a new version is availiable.
                Commands.ChatCommands.Add(new Command("vote.checkversion", CheckVersion, "tswversioncheck"));
            }
        }

        public void CheckVersion(CommandArgs e)
        {
            e.Player.SendInfoMessage(Version.ToString());
        }

        public bool tswQuery(string url, object userToken = null)
        {
            Uri uri = new Uri("http://www.tserverweb.com/vote.php?" + url);

            WebClient webClient;
            if (this.webClientQueue.TryDequeue(out webClient))
            {
                webClient.DownloadStringAsync(uri, userToken);
                return true;
            }

            return false;
        }

        public void validateCAPTCHA(CommandArgs e)
        {
            int id;
            string message;
            if (!GetServerID(out id, out message))
            {
                e.Player.SendErrorMessage("[TServerWeb] Vote failed, please contact an admin.");
                SendError("Configuration", message);
                return;
            }

            string answer = HttpUtility.UrlPathEncode(e.Parameters[0].ToString());
            string playerName = HttpUtility.UrlPathEncode(e.Player.Name);

            string url = "answer=" + answer + "&user=" + playerName + "&sid=" + id;
            if (!tswQuery(url, e))
            {
                // this happens when there are more than NumberOfWebClientsAvailable requests being executed at once
                e.Player.SendErrorMessage("[TServerWeb] Vote failed, please contact an admin.");
            }
        }

        private void doVote(CommandArgs e)
        {
            int id;
            string message;
            if (!GetServerID(out id, out message))
            {
                e.Player.SendErrorMessage("[TServerWeb] Vote failed, please contact an admin.");
                SendError("Configuration", message);
                return;
            }

            string url = "user=" + HttpUtility.UrlPathEncode(e.Player.Name) + "&sid=" + id;
            if (!tswQuery(url, e))
            {
                // this happens when there are more than NumberOfWebClientsAvailable requests being executed at once
                e.Player.SendErrorMessage("[TServerWeb] Vote failed, please contact an admin.");
            }
        }

        private void Vote(CommandArgs e)
        {
            try
            {
                if (e.Parameters.Count == 0)
                {
                    // Send the vote
                    doVote(e);
                }
                else
                {
                    // Answer was provided
                    validateCAPTCHA(e);
                }
            }
            catch (Exception ex)
            {
                e.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                SendError("Vote", "Connection failure: " + ex);
            }
        }

        private void ChangeID(CommandArgs e)
        {
            if (e.Parameters.Count == 0)
            {
                int id;
                string message;
                if (!GetServerID(out id, out message))
                {
                    e.Player.SendErrorMessage("[TServerWeb] Server ID is currently not specified! Please type /tserverweb [number] to set it. Reason:");
                    e.Player.SendErrorMessage(message);
                    return;
                }

                e.Player.SendInfoMessage("[TServerWeb] Server ID is currently set to " + id + ". Type /tserverweb [number] to change it.");
                return;
            }

            if (e.Parameters.Count >= 2)
            {
                e.Player.SendErrorMessage("[TServerWeb] Incorrect syntax! Correct syntax: /tserverweb [number]");
                return;
            }

            int newId;
            if (int.TryParse(e.Parameters[0], out newId))
            {
                string[] text =
                {
                    "**This is the configuration file, please do not edit.**", "Help page: http://www.tserverweb.com/help/",
                    "Server ID is on next line. Please DO NOT edit the following line, change it using \"/tserverweb [ID] in-game\"",
                    newId.ToString()
                };

                File.WriteAllLines(Path.Combine(TShock.SavePath, "TSWVote.txt"), text);
                e.Player.SendInfoMessage("[TServerWeb] Server ID successfully changed to " + newId + "!");
                return;
            }

            e.Player.SendErrorMessage("[TServerWeb] Number not specified! Please type /tserverweb [number]");
        }

        public void SendError(string typeoffailure, string message)
        {
            Log.Error("[TServerWeb] TSWVote Error: " + typeoffailure + "failure. Reason: " + message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[TServerWeb] TSWVote Error: " + typeoffailure + " failure. Reason: " + message);
            Console.ResetColor();
        }

        public bool GetServerID(out int id, out string message)
        {
            string[] stringid = File.ReadAllLines(Path.Combine(TShock.SavePath, "TSWVote.txt"));
            foreach (string str in stringid)
            {
                if (int.TryParse(str, out id))
                {
                    if (id == 0)
                    {
                        message = "Server ID not specified. Type /tserverweb [ID] to specify it.";
                        return false;
                    }

                    message = string.Empty;
                    return true;
                }
            }

            id = 0;
            message = "Server ID is not a number. Please type /tserverweb [ID] to set it.";
            return false;
        }

        private void WebClient_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            WebClient webClient = sender as WebClient;
            CommandArgs args = e.UserState as CommandArgs;
            if (args == null)
            {
                // put this WebClient back into the pool of available WebClients
                ReuseWebClient(webClient);
                return;
            }

            if (e.Error != null)
            {
                args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                SendError("Exception", e.Error.Message);

                // put this WebClient back into the pool of available WebClients
                ReuseWebClient(webClient);
                return;
            }

            Response response = Response.Read(e.Result);
            if (response == null)
            {
                args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                SendError("Response", "Invalid response received.");
                
                // put this WebClient back into the pool of available WebClients
                ReuseWebClient(webClient);
                return;
            }

            switch (response.response)
            {
                case "success":
                    // Correct answer was provided
                    // This means a vote is placed
                    args.Player.SendSuccessMessage("[TServerWeb] " + response.message);
                    break;
                case "failure":
                    args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                    SendError("Vote", response.message);
                    break;
                case "captcha":
                    args.Player.SendSuccessMessage("[TServerWeb] Please answer the question to make sure you are human.");
                    args.Player.SendSuccessMessage("[TServerWeb] You can type /vote <answer>");
                    args.Player.SendSuccessMessage("[TServerWeb] (CAPTCHA) " + response.message);
                    break;
                case "nocaptcha":
                    // Answer was provided, but there was no pending captcha
                    doVote(args);
                    SendError("Vote", response.message);
                    break;
                case "captchafail":
                    args.Player.SendErrorMessage("[TServerWeb] Vote failed! Reason: " + response.message);
                    break;
                case "":
                case null:

                    args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                    SendError("Connection", "Response is blank, something is wrong with connection. Please email contact@tserverweb.com about this issue.");
                    break;
            }

            // put this WebClient back into the pool of available WebClients
            ReuseWebClient(webClient);
        }

        private void ReuseWebClient(WebClient webClient)
        {
            if (webClient == null)
                return;

            this.webClientQueue.Enqueue(webClient);
        }
    }
}
