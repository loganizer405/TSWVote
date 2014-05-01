using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
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
		private const int NumberOfWebClientsAvailable = 30;

		private Dictionary<string, VoteIP> IPs = new Dictionary<string, VoteIP>();

		private ConcurrentQueue<VoteWC> webClientQueue;

		public string ConfigPath
		{
			get { return Path.Combine(TShock.SavePath, "TSWVote.txt"); }
		}

		public override string Name
		{
			get { return "TServerWebVote"; }
		}

		public override string Author
		{
			get { return "Loganizer + XGhozt"; }
		}

		public override string Description
		{
			get { return "A plugin to vote to TServerWeb in-game."; }
		}

		public override Version Version
		{
			get { return Assembly.GetExecutingAssembly().GetName().Version; }
		}

		public TSWVote(Main game)
			: base(game)
		{
			Order = 1000;
			WebRequest.DefaultWebProxy = null;

			webClientQueue = new ConcurrentQueue<VoteWC>();
			for (int i = 0; i < NumberOfWebClientsAvailable; i++)
			{
				VoteWC webClient = new VoteWC();
				webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
				webClientQueue.Enqueue(webClient);
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

				if (webClientQueue != null)
				{
					VoteWC webClient;
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
			if (!args.Text.StartsWith("/"))
			{
				return;
			}

			var player = TShock.Players[args.Who];

			if (player == null)
			{
				args.Handled = true;
				return;
			}

			Match M = Regex.Match(args.Text, "/vote( ?)(.*)", RegexOptions.IgnoreCase);
			if (M.Success)
			{
				CommandArgs e = new CommandArgs(args.Text, player, new List<string>());
				bool Space = M.Groups[1].Value == " ";
				string Args = M.Groups[2].Value;

				if (!IPs.ContainsKey(player.IP)) IPs.Add(player.IP, new VoteIP(DateTime.Now));

				if (!string.IsNullOrWhiteSpace(Args) && Space)
				{
					e.Parameters.Add(Args);
					TSPlayer.Server.SendMessage(player.Name + " has entered /vote captcha.", 255, 255, 255);
					Vote(e);
					args.Handled = true;
				}
				else if (string.IsNullOrWhiteSpace(Args))
				{
					TSPlayer.Server.SendMessage(player.Name + " executed: /vote.", 255, 255, 255);
					Vote(e);
					args.Handled = true;
				}
			}
		}

		private void OnInitialize(EventArgs args)
		{
			if (!File.Exists(ConfigPath))
			{
				string[] text = {"**This is the configuration file, please do not edit.**", "Help page: http://www.tserverweb.com/help/",
									"Server ID is on next line. Please DO NOT edit the following line, change it using \"/tserverweb [ID] in-game\"",
								"0"};

				File.WriteAllLines(ConfigPath, text);
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
				return;
			}

			Commands.ChatCommands.Add(new Command(delegate(CommandArgs e) { e.Player.SendErrorMessage("onChat handler by-pass!"); }, "vote"));
			// We're making sure the command can be seen in /help. It does nothing though.

			Commands.ChatCommands.Add(new Command("vote.changeid", ChangeID, "tserverweb"));

			Commands.ChatCommands.Add(new Command("vote.checkversion", CheckVersion, "tswversioncheck"));
		}

		private void CheckVersion(CommandArgs e)
		{
			e.Player.SendInfoMessage(Version.ToString());
		}

		private bool tswQuery(string url, object userToken = null)
		{
			Uri uri = new Uri("http://www.tserverweb.com/vote.php?" + url);

			VoteWC webClient;
			if (this.webClientQueue.TryDequeue(out webClient))
			{
				webClient.DownloadStringAsync(uri, userToken);
				return true;
			}
			return false;
		}

		private void validateCAPTCHA(CommandArgs e)
		{
			VoteIP IP = IPs[e.Player.IP];

			if (IP.State != VoteState.Captcha)
			{
				e.Player.SendSuccessMessage("[TServerWeb] You're not awaiting CAPTCHA.");
				return;
			}

			int id;
			string message;
			if (!GetServerID(out id, out message))
			{
				e.Player.SendErrorMessage("[TServerWeb] Vote failed, please contact an admin.");
				SendError("Configuration", message);
				IP.Fail();
				return;
			}

			string answer = HttpUtility.UrlPathEncode(e.Parameters[0].ToString());
			string playerName = HttpUtility.UrlPathEncode(e.Player.Name);

			string url = string.Format("answer={0}&user={1}&sid={2}", answer, playerName, id);
			tswQuery(url, e);
		}

		private void doVote(CommandArgs e)
		{
			VoteIP IP = IPs[e.Player.IP];

			if (!IP.CanVote())
			{
				IP.NotifyPlayer(e.Player);
				return;
			}

			int id;
			string message;
			if (!GetServerID(out id, out message))
			{
				e.Player.SendErrorMessage("[TServerWeb] Vote failed, please contact an admin.");
				SendError("Configuration", message);
				IP.Fail();
				return;
			}

			IP.State = VoteState.InProgress;
			IP.StateTime = DateTime.Now;
			string url = string.Format("user={0}&sid={1}", HttpUtility.UrlPathEncode(e.Player.Name), id);
			tswQuery(url, e);
		}

		private void Vote(CommandArgs e) // To be fair this should also have a permission.
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

				VoteIP IP = IPs[e.Player.IP];
				IP.Fail();

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

				File.WriteAllLines(ConfigPath, text);
				e.Player.SendInfoMessage("[TServerWeb] Server ID successfully changed to " + newId + "!");
				return;
			}

			e.Player.SendErrorMessage("[TServerWeb] Number not specified! Please type /tserverweb [number]");
		}

		private void SendError(string typeoffailure, string message)
		{
			string Error = string.Format("[TServerWeb] TSWVote Error: {0} failure. Reason: {1}", typeoffailure, message);
			Log.Error(Error);
			TSPlayer.Server.SendErrorMessage(Error);
		}

		private bool GetServerID(out int id, out string message)
		{
			string[] stringid = File.ReadAllLines(ConfigPath);
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
			VoteWC webClient = sender as VoteWC;

			CommandArgs args = e.UserState as CommandArgs;

			if (args == null)
			{
				ReuseWC(webClient);
				return;
			}

			VoteIP IP = IPs[args.Player.IP];

			if (e.Error != null)
			{
				args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
				SendError("Exception", e.Error.Message);

				IP.Fail();

				ReuseWC(webClient);
				return;
			}

			Response response = Response.Read(e.Result);
			if (response == null)
			{
				args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
				SendError("Response", "Invalid response received.");

				IP.Fail();

				ReuseWC(webClient);
				return;
			}

			switch (response.response)
			{
				case "success":
					// Correct answer was provided
					// This means a vote is placed
					args.Player.SendSuccessMessage("[TServerWeb] " + response.message);
					if (response.message != "Please wait 24 hours before voting for this server again!")
					{
						IP.State = VoteState.Success;
						IP.StateTime = DateTime.Now;
						VoteHooks.InvokeVoteSuccess(args.Player);
					}
					else
					{
						IP.State = VoteState.Wait;
						IP.StateTime = DateTime.Now;
					}
					break;
				case "failure":
					args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
					SendError("Vote", response.message);
					IP.Fail();
					break;
				case "captcha":
					args.Player.SendSuccessMessage("[TServerWeb] Please answer the question to make sure you are human.");
					args.Player.SendSuccessMessage("[TServerWeb] You can type /vote <answer>");
					args.Player.SendSuccessMessage("[TServerWeb] (CAPTCHA) " + response.message);
					IP.State = VoteState.Captcha;
					IP.StateTime = DateTime.Now;
					break;
				case "nocaptcha":
					// Answer was provided, but there was no pending captcha
					doVote(args);
					SendError("Vote", response.message);
					IP.Fail();
					break;
				case "captchafail":
					args.Player.SendErrorMessage("[TServerWeb] Vote failed! Reason: " + response.message);
					IP.State = VoteState.None;
					IP.StateTime = DateTime.Now;
					break;
				case "":
				case null:
				default:
					args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
					SendError("Connection", "Response is blank, something is wrong with connection. Please email contact@tserverweb.com about this issue.");
					IP.Fail();
					break;
			}

			ReuseWC(webClient);
		}

		private void ReuseWC(VoteWC WC)
		{
			if (WC == null) return;
			webClientQueue.Enqueue(WC);
		}

		private class VoteWC : WebClient
		{
			public static int Timeout = 2000; // Milliseconds

			public VoteWC()
			{
				Proxy = null;
				Headers.Add("user-agent", "TServerWeb Vote Plugin");
			}

			protected override WebRequest GetWebRequest(Uri uri)
			{
				WebRequest w = base.GetWebRequest(uri);
				w.Timeout = Timeout;
				return w;
			}
		}
	}
}
