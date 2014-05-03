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
			try
			{
				return JsonConvert.DeserializeObject<Response>(text);
			}
			catch
			{
				return null;
			}
		}
	}
}
