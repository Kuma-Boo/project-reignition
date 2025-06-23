using Godot;
using Project.Core;
using Project.Gameplay.Bosses;

namespace Project.Gameplay;

public partial class BemothHornState : PlayerState
{
	public CaptainBemothHorn Trigger { get; set; }

	private int PullStrength
	{
		get
		{
			if (Mathf.IsEqualApprox(pullChargeTimer, OptimalPullChargeTiming))
				return 4; // Max damage

			if (pullChargeTimer >= OptimalPullChargeTiming * .5f)
				return 3; // Mid damage

			return 2; // Low damage
		}
	}
	/// <summary> Used to mimmick the "optimal speedrun strat" in the original game. </summary>
	private float pullChargeTimer;
	private readonly float OptimalPullChargeTiming = .4f;

	public override void EnterState()
	{
		pullChargeTimer = 0f;
		Player.Lockon.IsMonitoring = false;

		// Prevent accidental inputs
		Player.Controller.ResetJumpBuffer();
		Player.Controller.ResetActionBuffer();

		Player.Animator.StartBemothHorn();
		Player.StartExternal(Trigger, Trigger.FollowObject, 1f);

		Trigger.CallDeferred(CaptainBemothHorn.MethodName.JoltHorn, 1);
	}

	public override void ExitState()
	{
		pullChargeTimer = 0f;
		Player.Animator.ResetState();
		Player.StopExternal();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);

		if (Trigger.IsPopping || Trigger.IsPopReady)
			return null;

		ProcessPullCharge();

		if (Trigger.IsJoltingHorn)
			return null;

		if (Player.Controller.IsJumpBufferActive)
		{
			Trigger.JumpOff();
			Player.Controller.ResetJumpBuffer();
		}

		return null;
	}

	private void ProcessPullCharge()
	{
		if (Input.IsActionPressed("button_action"))
		{
			if (Mathf.IsEqualApprox(pullChargeTimer, OptimalPullChargeTiming)) // Already charged
				return;

			pullChargeTimer = Mathf.MoveToward(pullChargeTimer, OptimalPullChargeTiming, PhysicsManager.physicsDelta);
			return;
		}

		if (Mathf.IsZeroApprox(pullChargeTimer))
			return;

		if (!Trigger.IsJoltingHorn)
		{
			Trigger.JoltHorn(PullStrength);
			pullChargeTimer = 0f;
			Player.Camera.StartMediumCameraShake();
			Player.Controller.ResetActionBuffer();
			return;
		}

		pullChargeTimer = Mathf.MoveToward(pullChargeTimer, 0, PhysicsManager.physicsDelta);
	}
}
