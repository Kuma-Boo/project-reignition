namespace Project.Gameplay;

public partial class IdleState : PlayerState
{
	public override void EnterState()
	{
		Controller.MoveSpeed = 0;
	}
}
