using System;
using System.Collections.Generic;
using System.IO;
using TShockAPI;
using Newtonsoft.Json;

namespace TSWVote
{
	internal class Config
	{
		internal static string ConfigPath
		{
			get { return Path.Combine(TShock.SavePath, "TSWVote.json"); }
		}

		internal int ServerID = 0;
		internal int NumberOfWebClients = 30;
		internal int Timeout = 2000;

		internal static Config Read()
		{
			if (!File.Exists(ConfigPath))
				File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(new Config()));

			return JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath));
		}

		internal void Write()
		{
			File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this));
		}
	}
}
