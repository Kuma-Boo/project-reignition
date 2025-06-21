using Godot;
using Project.Core;
using Project.Gameplay.Bosses;

namespace Project.Gameplay;

public partial class BemothHornState : PlayerState
{
	public CaptainBemothHorn Trigger { get; set; }

	private float timer;
	private readonly float PopDelay = 0.2f;

	public override void EnterState()
	{
		Player.Lockon.IsMonitoring = false;

		Player.Animator.StartBemothHorn();
		Player.StartExternal(Trigger, Trigger.FollowObject, .5f);

		Trigger.JoltHorn(false);
	}

	public override void ExitState()
	{
		Player.StopExternal();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);

		if (Trigger.IsJoltingHorn || Trigger.IsPopping)
			return null;

		if (Trigger.IsPopReady)
		{
			if (Mathf.IsEqualApprox(timer, PopDelay))
				Trigger.StartPop();

			timer = Mathf.MoveToward(timer, PopDelay, PhysicsManager.physicsDelta);
			return null;
		}

		if (Player.Controller.IsJumpBufferActive)
			Trigger.JumpOff();
		else if (Player.Controller.IsActionBufferActive)
			Trigger.JoltHorn();

		Player.Controller.ResetJumpBuffer();
		Player.Controller.ResetActionBuffer();

		return null;
	}
}
