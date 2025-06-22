using Godot;
using Project.Core;
using Project.Gameplay.Bosses;

namespace Project.Gameplay;

public partial class BemothHornState : PlayerState
{
	public CaptainBemothHorn Trigger { get; set; }

	public override void EnterState()
	{
		Player.Lockon.IsMonitoring = false;

		// Prevent accidental inputs
		Player.Controller.ResetJumpBuffer();
		Player.Controller.ResetActionBuffer();

		Player.Animator.StartBemothHorn();
		Player.StartExternal(Trigger, Trigger.FollowObject, 1f);

		Trigger.CallDeferred(CaptainBemothHorn.MethodName.JoltHorn, false);
	}

	public override void ExitState()
	{
		Player.Animator.ResetState();
		Player.StopExternal();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);

		if (Trigger.IsJoltingHorn || Trigger.IsPopping)
			return null;

		if (Trigger.IsPopReady)
			return null;

		if (Player.Controller.IsJumpBufferActive)
		{
			Trigger.JumpOff();
			Player.Controller.ResetJumpBuffer();
		}
		else if (Player.Controller.IsActionBufferActive)
		{
			Trigger.JoltHorn();
			Player.Camera.StartMediumCameraShake();
			Player.Controller.ResetActionBuffer();
		}

		return null;
	}
}
