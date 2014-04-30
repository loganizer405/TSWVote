using System;
using System.Threading;
using System.IO;
using System.Collections.Generic;
using System.Linq;
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
		//private WebClient webClient; // Concurrency issues here

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
			/*
			this.webClient = new WebClient();
			this.webClient.Proxy = null;
			this.webClient.Headers.Add("user-agent", "TServerWeb Vote Plugin");
			this.webClient.DownloadStringCompleted += WebClient_DownloadStringCompleted;
			 */
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

				/*
				if (this.webClient != null)
				{
					this.webClient.DownloadStringCompleted -= WebClient_DownloadStringCompleted;
					this.webClient.Dispose();
				}
				 */
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

			Match M = Regex.Match(args.Text, "/vote(.*)", RegexOptions.IgnoreCase);
			if (M.Success)
			{
				args.Handled = true;
				CommandArgs e = new CommandArgs(args.Text, player, new List<string>());
				string Args = M.Groups[1].Value;
				if (!string.IsNullOrWhiteSpace(Args) && Args[0] == ' ')
					e.Parameters.Add(M.Groups[1].Value.Substring(1));

				Vote(e);
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
			}
			else
			{
				// Commands.ChatCommands.Add(new Command("", Vote, "vote"));
				Commands.ChatCommands.Add(new Command("vote.changeid", ChangeID, "tserverweb"));
				// This is so it will tell you if a new version is availiable.
				Commands.ChatCommands.Add(new Command("vote.checkversion", CheckVersion, "tswversioncheck"));
			}
		}

		private void CheckVersion(CommandArgs e)
		{
			e.Player.SendInfoMessage(Version.ToString());
		}

		private void tswQuery(string url, object userToken = null) // Not sure if this works.
		{
			Uri uri = new Uri("http://www.tserverweb.com/vote.php?" + url);
			WebClient WC = new WebClient() { Proxy = null };
			WC.Headers.Add("user-agent", "TServerWeb Vote Plugin");
			WC.DownloadStringCompleted += WebClient_DownloadStringCompleted;
			WC.DownloadStringAsync(uri, userToken);
		}

		private void validateCAPTCHA(CommandArgs e)
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

			string url = string.Format("answer={0}&user={1}&sid={2}", answer, playerName, id);
			tswQuery(url, e);
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
			CommandArgs args = e.UserState as CommandArgs;
			if (args == null)
				return;

			if (e.Error != null)
			{
				args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
				SendError("Exception", e.Error.Message);
				return;
			}

			Response response = Response.Read(e.Result);
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
				default:
					args.Player.SendErrorMessage("[TServerWeb] Vote failed! Please contact an administrator.");
					SendError("Connection", "Response is blank, something is wrong with connection. Please email contact@tserverweb.com about this issue.");
					break;
			}
		}
	}
}
