using System;
using System.Timers;
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
                return "Loganizer + XGhozt";
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
                return new Version("1.0");
            }
        }
        public TSWVote(Main game)
            : base(game)
        {
            Order = 1000;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);    
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
            }
            base.Dispose(disposing);
        }       
        public void OnInitialize(EventArgs args)
        {
            int ID;
            string message;
            if (!File.Exists(Path.Combine(TShock.SavePath, "TSWVote.txt")))
            {
                string[] text = {"**This is the configuration file, please do not edit.**", "Help page: http://www.tserverweb.com/help/",
                                    "Server ID is on next line. Please DO NOT edit the following line, change it using \"/tserverweb [ID] in-game\"",
                                "0"};
                File.WriteAllLines(Path.Combine(TShock.SavePath, "TSWVote.txt"), text);
            }
            else
            {
                if (!GetServerID(out ID, out message))
                    SendError("Configuration", message);
            }
            if (TShock.Config.RestApiEnabled == false)
            {
                SendError("REST API", "REST API Not Enabled! TSWVote plugin will not load!");
            }
            else
            {
                Commands.ChatCommands.Add(new Command("server.vote", Vote, "vote"));
                Commands.ChatCommands.Add(new Command("vote.changeid", ChangeID, "tserverweb"));
            }
        }
        public void Vote(CommandArgs e)
        {
            int ID;
            string message;
            try
            {
                if (!GetServerID(out ID, out message))
                {
                    e.Player.SendErrorMessage("[TServerWeb] Vote failed, please contact an admin.");
                    SendError("Configuration", message);
                    return;
                }
                WebClient wc = new WebClient();
                wc.Headers.Add("user-agent", "TServerWeb Vote Plugin");
                Uri uri = new Uri("http://www.tserverweb.com/vote.php?user=" + HttpUtility.UrlPathEncode(e.Player.Name) + "&sid=" + ID);


                Response response = Response.Read(wc.DownloadString(uri));
                
                switch (response.response)
                {
                    case "success":
                        e.Player.SendSuccessMessage("[TServerWeb] " + response.message);
                        break;
                    case "failure":
                        e.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                        SendError("Vote", response.message);
                        break;
                    case "":
                    case null:
                        e.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                        SendError("Connection", "Response is blank, something is wrong with connection. Please email contact@tserverweb.com about this issue.");
                        break;
                }
            }
            catch (Exception ex)
            {
                e.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
                SendError("Vote", "Connection failure: " + ex.ToString());
                return;
            }           
        }
        public void ChangeID(CommandArgs e)
        {
            int ID;
            string message;
            if (e.Parameters.Count == 0)
            {  
                if (!GetServerID(out ID, out message))
                {
                    e.Player.SendErrorMessage("[TServerWeb] Server ID is currently not specified! Please type /tserverweb [number] to set it. Reason:");
                    e.Player.SendErrorMessage(message);
                    return;
                }
                else
                {
                        e.Player.SendInfoMessage("[TServerWeb] Server ID is currently set to " + ID +
                        ". Type /tserverweb [number] to change it.");
                    return;
                }
            }
            if (e.Parameters.Count >= 2)
            {
                e.Player.SendErrorMessage("[TServerWeb] Incorrect syntax! Correct syntax: /tserverweb [number]");
                return;
            }
            else
            {
                int newID;
                if (int.TryParse(e.Parameters[0].ToString(), out newID))
                {
                    string[] text = {"**This is the configuration file, please do not edit.**", "Help page: http://www.tserverweb.com/help/",
                                    "Server ID is on next line. Please DO NOT edit the following line, change it using \"/tserverweb [ID] in-game\"",
                                newID.ToString()};
                    File.WriteAllLines(Path.Combine(TShock.SavePath, "TSWVote.txt"), text);
                    e.Player.SendInfoMessage("[TServerWeb] Server ID successfully changed to " + newID + "!");
                    return;
                }
                else
                {
                    e.Player.SendErrorMessage("[TServerWeb] Number not specified! Please type /tserverweb [number]");
                    return;
                }
            }
        }
        public void SendError(string typeoffailure, string message)
        {
            Log.Error("[TServerWeb] TSWVote Error: " + typeoffailure + "failure. Reason: " + message);
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[TServerWeb] TSWVote Error: " + typeoffailure + " failure. Reason: " + message);
            Console.ResetColor();
        }
        public bool GetServerID(out int ID, out string message)
        {
            string[] stringid = File.ReadAllLines(Path.Combine(TShock.SavePath, "TSWVote.txt"));
            foreach (string str in stringid)
            {
                if(int.TryParse(str, out ID))
                {
                    if (ID == 0)
                    {
                        message = "Server ID not specified. Type /tserverweb [ID] to specify it.";
                        return false;
                    }
                    else
                    {
                        message = "";
                        return true;
                    }
                }               
            }
            ID = 0;
            message = "Server ID is not a number. Please type /tserverweb [ID] to set it.";
            return false;
        }
    }
}
