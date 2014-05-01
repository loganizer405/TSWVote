using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TSWVote
{
	internal enum VoteState
	{
		None = 0, // Player hasn't voted yet (since server start)
		InProgress, // Vote is being processed, new vote can't be initiated
		Captcha, // Awaiting captcha entry, new vote can't be initiated (timeout 1 minute?)
		Success, // Voted successfully, wait 24 hours to vote again
		Wait, // Server doesn't remember player voting, yet website does, wait 1 hour before retrying
		Fail // Wait 5 minutes before retrying

	}
}