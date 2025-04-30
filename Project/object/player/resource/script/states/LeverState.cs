using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class LeverState : PlayerState
{
	public Lever Trigger { get; set; }
	private bool isTurningLever;

	public override void EnterState()
	{
		isTurningLever = false;
		Player.Skills.DisableBreakSkills();
		Player.Animator.StartLever(Trigger.IsRightLever);
		Player.GlobalPosition = Trigger.TargetStandingPosition;
	}

	public override PlayerState ProcessPhysics()
	{
		if (isTurningLever)
			return null;

		if ((Player.Controller.InputHorizontal > 0 && Trigger.IsRightLever) ||
			(Player.Controller.InputHorizontal < 0 && !Trigger.IsRightLever))
		{
			StartLeverTurn();
		}

		return null;
	}

	private void StartLeverTurn()
	{
		Trigger.StartLeverTurn();
		Player.Animator.StartLeverTurn(Trigger.IsRightLever);
	}
}
