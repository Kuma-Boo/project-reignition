using Godot;

namespace Project.Gameplay.Hazards
{
	public partial class SpikePanel : Hazard
	{
		[Export]
		public float inactiveLength;
		[Export]
		public float warningLength;
		[Export]
		public float activeLength;
		[Export(PropertyHint.Range, "0, 1")]
		public float startingOffset;

		private AttackState attackState;
		private enum AttackState
		{
			Inactive,
			Warning,
			Active,
		}

		[Export]
		private AnimationPlayer animator;
		[Export]
		private Timer timer;

		public override void _Ready()
		{
			if (Mathf.IsZeroApprox(inactiveLength))
				TimerCompleted();
			else
			{
				timer.WaitTime = inactiveLength - (startingOffset * inactiveLength);
				timer.Start();
			}
		}

		public void TimerCompleted()
		{
			double targetWaitTime = 0;

			switch (attackState)
			{
				case AttackState.Active:
					attackState = AttackState.Inactive;
					targetWaitTime = inactiveLength;
					animator.Play("inactive");
					break;
				case AttackState.Inactive:
					attackState = AttackState.Warning;
					targetWaitTime = warningLength;
					animator.Play("warning");
					break;
				case AttackState.Warning:
					attackState = AttackState.Active;
					targetWaitTime = activeLength;
					animator.Play("active");
					break;
			}

			animator.Advance(0);

			//Update Animation
			targetWaitTime += animator.CurrentAnimationLength;
			timer.WaitTime = targetWaitTime;
			timer.Start();
		}
	}
}
