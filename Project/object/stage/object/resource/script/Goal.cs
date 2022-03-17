using Godot;

namespace Project.Gameplay
{
	public class Goal : StageObject
	{
		public override bool IsRespawnable() => false;


		/*
		Ranking system

		Score is calculated as
		(Action bonus + Enemy Bonus + Ring Bonus) * Technical Bonus

		Action Bonus:
		Pearls - 1 point, Resets
		Rings - 5 points
		Grinding- 10 point every two meters on the rail multiplied by the 
		number of rails (I think... can't understand the site...), 20 points per 
		trick (leaning on a rail or changing rails.)
		Time Break- 30 points first 3 seconds, then 10 points for each second 
		afterwards.
		Speed Break- 10 points for each meter multiplied by number of times 
		Speed Break was used (I think that's what the official site is trying 
		to say >_>;)
		
		Ring Bonus: (Number of Rings / Max Rings) * 1000. Note that skills that give rings will provide more leeway

		Enemy Bonus:
		50 points per enemy, 1,000-4,000 for each Boss

		Technical Bonus goes down each time you are hit or you die. If you 
		fail a mission, Technical bonus is below 1.
		No damage: x2
		Damage once: x1.5
		2-3 times: x1.2
		4-5 times: x1.1
		6 or more times, or you died: X1
		Failed Mission: x0.3

		Mission Bonus: Received the very first time you beat a mission and is 
		a set number, if you replay that mission you can't get it again.

		Getting a gold medal requires all requirements at once.
		- Beat the time record
		- Break the score record
			- Doing this will almost certainly require a deathless run.
		*/
		[Export]
		public int scoreRequirement; //Requirement for score rank
		[Export]
		public int timeRequirement; //Requirement (in seconds) for time rank
		[Export]
		public int goldRankScore;

		public override void OnEnter()
		{
			//End the stage
			FinishStage(false);
		}

		public void FinishStage(bool isStageFailed)
		{
			if (isStageFailed)
			{
				return;
			}

			//Asssign rank
			//GameplayInterface.instance.Score;
		}
	}
}
