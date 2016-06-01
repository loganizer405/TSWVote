using System;

namespace TSWVote
{
	internal class VoteIP
	{
		internal VoteState State = VoteState.None;
		internal DateTime StateTime;

		internal VoteIP(DateTime time, VoteState state = VoteState.None)
		{
			State = state;
			StateTime = time;
		}

		internal bool CanVote()
		{
			bool Return;

			switch (State)
			{
				case VoteState.None:
				default:
					return true;

				case VoteState.InProgress:
				case VoteState.Fail:
				case VoteState.Captcha:

					Return = (DateTime.Now - StateTime).TotalMinutes > 5;
					break;

				case VoteState.Success:
					Return = (DateTime.Now - StateTime).TotalHours > 24;
					break;

				case VoteState.Wait:
					Return = (DateTime.Now - StateTime).TotalHours > 1;
					break;
			}

			if (Return && State != VoteState.None)
			{
				State = VoteState.None;
				StateTime = DateTime.Now;
			}
			return Return;
		}

		internal void Fail()
		{
			State = VoteState.Fail;
			StateTime = DateTime.Now;
		}

		internal void NotifyPlayer(TShockAPI.TSPlayer Player)
		{
			switch (State)
			{
				case VoteState.Captcha:
					Player.SendSuccessMessage("[TServerWeb] Please enter CAPTCHA!");
					break;

				case VoteState.Fail:
					Player.SendSuccessMessage("[TServerWeb] Your previous vote failed! Please wait 5 minutes before trying again!");
					break;

				case VoteState.InProgress:
					Player.SendSuccessMessage("[TServerWeb] Your vote is being processed, please wait...");
					break;

				case VoteState.Success:
				case VoteState.Wait:
					Player.SendSuccessMessage("[TServerWeb] Please wait 24 hours before voting for this server again!");
					break;
			}
		}
	}
}
