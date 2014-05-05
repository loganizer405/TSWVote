using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace TSWVote
{
	internal class Response
	{
		public string response;
		public string message;
		public string question;

		public static Response Read(string text)
		{
			Response R = null;

			try
			{
				R = JsonConvert.DeserializeObject<Response>(text);
			}
			catch (Exception ex)
			{
				TShockAPI.Log.Error(ex.Message);
				TShockAPI.Log.Error("Error parsing as JSON: " + text);
			}

			return R;
		}
	}
}
