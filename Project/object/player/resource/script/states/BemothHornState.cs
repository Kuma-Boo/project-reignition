using Godot;
using Project.Core;
using Project.Gameplay.Bosses;

namespace Project.Gameplay;

public partial class BemothHornState : PlayerState
{
	public CaptainBemothHorn Trigger { get; set; }

	public bool CanJump { get; set; }
	public bool CanPullHorns { get; set; }
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
	private readonly string JumpAction = "action_jump";
	private readonly string PullAction = "action_pull";

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

		HeadsUpDisplay.Instance.SetPrompt(PullAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(JumpAction, 1);
		HeadsUpDisplay.Instance.ShowPrompts();
	}

	public override void ExitState()
	{
		pullChargeTimer = 0f;
		Player.Animator.ResetState();
		Player.StopExternal();

		HeadsUpDisplay.Instance.HidePrompts();
	}

	public override PlayerState ProcessPhysics()
	{
		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);

		if (Trigger.IsPopping || Trigger.IsPopReady)
			return null;

		ProcessPullCharge();

		if (Trigger.IsJoltingHorn)
			return null;

		if (CanJump && Player.Controller.IsJumpBufferActive)
		{
			Trigger.JumpOff();
			Player.Effect.PlayVoice(CanPullHorns ? "grunt" : "sidle hurt");
			Player.Controller.ResetJumpBuffer();
		}

		return null;
	}

	private void ProcessPullCharge()
	{
		if (!CanPullHorns)
			return;

		if (Input.IsActionPressed("button_action") || Input.IsActionPressed("button_attack"))
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
			if (PullStrength > 2) // Feedback
				Player.Camera.StartMediumCameraShake();
			Player.Controller.ResetActionBuffer();

			pullChargeTimer = 0f;
			return;
		}

		pullChargeTimer = Mathf.MoveToward(pullChargeTimer, 0, PhysicsManager.physicsDelta);
	}
}
