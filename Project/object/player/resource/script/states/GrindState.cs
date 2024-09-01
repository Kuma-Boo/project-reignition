using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class GrindState : PlayerState
{
	[Export]
	private PlayerState jumpState;
	[Export]
	private PlayerState grindstepState;
	[Export]
	private PlayerState fallState;

	public GrindRail ActiveGrindRail { get; set; }

	/// <summary> How "magnetic" the rail is. Early 3D Sonic games had a habit of putting this too low. </summary>
	private readonly float GrindrailSnapping = 1.0f;
	/// <summary> Rail snapping is more generous when performing a grind step. </summary>
	private readonly float GrindstepRailSnapping = 1.4f;
	/// <summary> Basic measure for attaching at the end of the rail. </summary>
	private float RailFudgeFactor => Player.Stats.GrindSettings.Speed * PhysicsManager.physicsDelta;
	public override bool ProcessOnEnter => true;
	public override void EnterState()
	{
		currentCharge = 0;
		perfectChargeTimer = 0;

		ActiveGrindRail.Activate();
		if (!ActiveGrindRail.IsBonusDisabled && Player.IsGrindstepping)
			BonusManager.instance.QueueBonus(new(BonusType.Grindstep));

		Player.AllowLandingGrind = false;
		Player.IsGrindstepping = false;

		float positionSmoothing = .2f;
		float smoothFactor = RailFudgeFactor * 5f;
		if (ActiveGrindRail.PathFollower.Progress >= ActiveGrindRail.RailLength - smoothFactor) // Calculate smoothing when activating at the end of the rail
		{
			float progressFactor = Mathf.Abs(ActiveGrindRail.PathFollower.Progress - ActiveGrindRail.RailLength);
			positionSmoothing = Mathf.SmoothStep(0f, positionSmoothing, Mathf.Clamp(progressFactor / smoothFactor, 0f, 1f));
		}

		// REFACTOR TODO Player.ResetActionState(); // Reset grind step, cancel stomps, jumps, etc
		Player.StartExternal(this, ActiveGrindRail.PathFollower, positionSmoothing);

		Player.IsMovingBackward = false;
		// REFACTOR TODO Player.LandOnGround(); // Rail counts as being on the ground
		Player.IsOnGround = true;
		Player.VerticalSpeed = 0f;
		Player.MoveSpeed = Player.Stats.GrindSettings.Speed * Player.Stats.CalculateGrindSpeedRatio(); // Start at the correct speed
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.GrindUp) &&
			SaveManager.ActiveSkillRing.GetAugmentIndex(SkillKey.GrindUp) == 3)
		{
			StageSettings.Instance.UpdateRingCount(5, StageSettings.MathModeEnum.Subtract, true);
		}

		Player.Animator.ExternalAngle = 0; // Reset rotation
		Player.Animator.StartBalancing();
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);

		// Reset FX
		Player.Effect.StartGrindFX(true);
		Player.Lockon.IsMonitoring = false;

		/*
		REFACTOR TODO
		ActiveGrindRail.Connect(PlayerController.SignalName.Knockback, new Callable(this, GrindRail.MethodName.Deactivate));
		ActiveGrindRail.Connect(PlayerController.SignalName.ExternalControlCompleted, new Callable(this, GrindRail.MethodName.Deactivate));
		*/
	}

	public override void ExitState()
	{
		Player.IsOnGround = false; // Disconnect from the ground
		Player.StopExternal();

		// Preserve speed
		float launchSpeed = Player.MoveSpeed;
		float launchAngle = ActiveGrindRail.PathFollower.Up().AngleTo(Vector3.Up) * Mathf.Sign(ActiveGrindRail.PathFollower.Up().Y);
		Player.MoveSpeed = Mathf.Cos(launchAngle) * launchSpeed;
		Player.VerticalSpeed = Mathf.Sin(launchAngle) * -launchSpeed;

		if (!Player.IsGrindstepping) // Smoother transition to falling animation
			Player.Animator.ResetState(.2f);
		Player.Animator.SnapRotation(Player.MovementAngle);
		Player.Animator.IsFallTransitionEnabled = true;
		Player.Effect.StopGrindFX();

		ActiveGrindRail.Deactivate();
		ActiveGrindRail = null;

		// Disconnect signals
		/*
		REFACTOR TODO
		Player.Disconnect(PlayerController.SignalName.Knockback, new Callable(this, MethodName.Deactivate));
		Player.Disconnect(PlayerController.SignalName.ExternalControlCompleted, new Callable(this, MethodName.Deactivate));
		*/
	}

	public override PlayerState ProcessPhysics()
	{
		ProcessMovement();
		UpdateCharge();

		bool isGrindCompleted = Mathf.IsEqualApprox(ActiveGrindRail.PathFollower.ProgressRatio, 1);
		if (Player.Controller.IsJumpBufferActive &&
			(!Player.Animator.IsBalanceShuffleActive || isGrindCompleted))
		{
			return ProcessJump();
		}

		if (isGrindCompleted || Mathf.IsZeroApprox(Player.MoveSpeed)) // Disconnect from the rail
			return fallState;

		return null;
	}

	private void ProcessMovement()
	{
		// Check wall
		float movementDelta = Player.MoveSpeed * PhysicsManager.physicsDelta;
		RaycastHit hit = CheckWall(ActiveGrindRail, movementDelta);
		if (hit && hit.collidedObject is StaticBody3D) // Stop player when colliding with a static body
		{
			movementDelta = 0; // Limit movement distance
			Player.MoveSpeed = 0f;
		}
		else // No walls, Check for crushers
		{
			// REFACTOR TODO Player.CheckCeiling();
		}

		ActiveGrindRail.PathFollower.Progress += movementDelta;
		ActiveGrindRail.PathFollower.ProgressRatio = Mathf.Clamp(ActiveGrindRail.PathFollower.ProgressRatio, 0.0f, 1.0f);
		Player.UpdateExternalControl(true);
		Player.Animator.UpdateBalanceSpeed(Player.Stats.GrindSettings.GetSpeedRatioClamped(Player.MoveSpeed));

		ActiveGrindRail.UpdateInvisibleRailPosition();

		Player.UpDirection = ActiveGrindRail.PathFollower.Up();
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(ActiveGrindRail.PathFollower.Forward(), ActiveGrindRail.PathFollower.Up());
	}

	public bool IsRailActivationValid(GrindRail grindrail)
	{
		if (ActiveGrindRail != null) // Already grinding
			return false;

		if (Player.VerticalSpeed >= 0f) // Player must be falling to start grinding!
			return false;

		// Resync Grindrail's PathFollower
		Vector3 delta = grindrail.Rail.GlobalTransform.Basis.Inverse() * (Player.GlobalPosition - grindrail.Rail.GlobalPosition);
		grindrail.PathFollower.Progress = grindrail.Rail.Curve.GetClosestOffset(delta);

		// Ignore rails when the player is too close to the end
		if (grindrail.PathFollower.Progress >= grindrail.Rail.Curve.GetBakedLength() - RailFudgeFactor)
			return false;

		// Ignore grinds that would immediately put the player into a wall
		if (CheckWall(grindrail, Player.Stats.GrindSettings.Speed * PhysicsManager.physicsDelta))
			return false;

		delta = grindrail.PathFollower.GlobalTransform.Basis.Inverse() * (Player.GlobalPosition - grindrail.PathFollower.GlobalPosition);
		delta.Y -= Player.VerticalSpeed * PhysicsManager.physicsDelta;
		if (delta.Y < 0.01f && !(Player.IsOnGround && Player.AllowLandingGrind))
			return false;

		// Horizontal validation
		if (Mathf.Abs(delta.X) > GrindrailSnapping &&
			!(Player.IsGrindstepping && Mathf.Abs(delta.X) > GrindstepRailSnapping))
		{
			return false;
		}

		return true;
	}

	private PlayerState ProcessJump()
	{
		Player.Controller.ResetJumpBuffer();

		// Check if the player is holding a direction parallel to rail and start a grindstep
		float targetInputAngle = Player.Controller.GetTargetInputAngle();
		Player.IsGrindstepping = !Mathf.IsZeroApprox(Player.Controller.GetInputStrength()) &&
			(Player.Controller.IsHoldingDirection(targetInputAngle, Player.MovementAngle + (Mathf.Pi * .5f)) ||
			Player.Controller.IsHoldingDirection(targetInputAngle, Player.MovementAngle - (Mathf.Pi * .5f)));

		if (Player.IsGrindstepping)
			return grindstepState;

		// Jump normally
		Player.DisableAccelerationJump = true;
		return jumpState;
	}

	private float currentCharge;
	private float perfectChargeTimer;
	private readonly float PerfectChargeInputWindow = .3f;
	private readonly float ChargeSpeed = 3.0f;
	private void UpdateCharge()
	{
		bool isCharging = Input.IsActionPressed("button_action");
		bool isCharged = Mathf.IsEqualApprox(currentCharge, 1.0f);

		perfectChargeTimer = Mathf.MoveToward(perfectChargeTimer, 0, PhysicsManager.physicsDelta);

		if (isCharging)
			Charge(isCharged);
		else if (!Mathf.IsZeroApprox(currentCharge))
			Uncharge(isCharged);

		UpdateChargeAnimations(isCharging);
	}

	private void Charge(bool isCharged)
	{
		currentCharge = Mathf.MoveToward(currentCharge, 1.0f, ChargeSpeed * PhysicsManager.physicsDelta);
		if (Player.Animator.IsBalanceShuffleActive)
		{
			// Prevent fully charging during a grind shuffle
			currentCharge = Mathf.Min(currentCharge, 0.99f);
			return;
		}

		if (!Mathf.IsEqualApprox(currentCharge, 1.0f))
			Player.Effect.StartChargeFX();

		if (!isCharged)
			return;

		// Play fully charged VFX
		perfectChargeTimer = PerfectChargeInputWindow;
		Player.Effect.FullGrindChargeFX();
	}

	private void Uncharge(bool isCharged)
	{
		currentCharge = Mathf.MoveToward(currentCharge, 0f, ChargeSpeed * PhysicsManager.physicsDelta);
		Player.Effect.StopChargeFX();

		// Update shuffling
		if (!Player.Animator.IsBalanceShuffleActive && isCharged)
		{
			StartShuffle();
			currentCharge = 0;
			return;
		}

		if (!Mathf.IsZeroApprox(currentCharge))
			return;

		ActiveGrindRail.IsBonusDisabled = true;
	}

	private void UpdateChargeAnimations(bool isCharging)
	{
		Player.Animator.UpdateBalanceCrouch(isCharging && !Player.Animator.IsBalanceShuffleActive);
		Player.Animator.UpdateBalancing(isCharging ? 0.0f : Player.Animator.CalculateTurnRatio());
		if (!Player.Animator.IsBalanceShuffleActive) // Only slow down when not shuffling
		{
			float speedRatio = Player.Stats.GrindSettings.GetSpeedRatioClamped(Player.MoveSpeed);
			Player.Effect.UpdateGrindFX(speedRatio);

			if (Mathf.IsZeroApprox(perfectChargeTimer))
				Player.MoveSpeed = Player.Stats.GrindSettings.UpdateInterpolate(Player.MoveSpeed, isCharging ? 0f : -1f);
		}
	}

	private void StartShuffle()
	{
		bool isPerfectCharge = !Mathf.IsZeroApprox(perfectChargeTimer);
		Player.MoveSpeed = isPerfectCharge ? Player.Stats.perfectShuffleSpeed : Player.Stats.GrindSettings.Speed;
		Player.Effect.StartGrindFX(false);
		Player.Animator.StartGrindShuffle();
		if (isPerfectCharge)
			Player.Effect.PerfectGrindShuffleFX();
		else
			ActiveGrindRail.IsBonusDisabled = true;

		if (!ActiveGrindRail.IsBonusDisabled)
			BonusManager.instance.QueueBonus(new(BonusType.GrindShuffle));
	}

	private RaycastHit CheckWall(Node3D castNode, float length)
	{
		length += Player.CollisionSize.X;
		RaycastHit hit = castNode.CastRay(castNode.GlobalPosition, castNode.Forward() * length, Player.CollisionMask);
		DebugManager.DrawRay(castNode.GlobalPosition, castNode.Forward() * length, hit ? Colors.Red : Colors.White);

		// Block grinding through objects in the given group
		if (hit && hit.collidedObject.IsInGroup("grind wall"))
			return hit;

		return new();
	}
}
