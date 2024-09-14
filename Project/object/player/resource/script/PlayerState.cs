using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerState : Node
{
	protected PlayerController Player { get; private set; }

	public void Initialize(PlayerController player) => Player = player;

	/// <summary> Called when this state is entered. </summary>
	public virtual void EnterState() { }

	/// <summary> Called when this state is exited. </summary>
	public virtual void ExitState() { }

	/// <summary> Called on each frame update. </summary>
	public virtual PlayerState ProcessFrame() => null;

	/// <summary> Called on each physics update. </summary>
	public virtual PlayerState ProcessPhysics() => null;

	protected bool turnInstantly;
	protected MovementSetting ActiveMovementSettings => Player.IsOnGround ? Player.Stats.GroundSettings : Player.Stats.AirSettings;
	protected virtual void ProcessMoveSpeed()
	{
		turnInstantly = Mathf.IsZeroApprox(Player.MoveSpeed) && !Player.Skills.IsSpeedBreakActive; // Store this for turning function

		if (Player.Skills.IsSpeedBreakActive)
		{
			// Override to speedbreak speed
			if (Player.Skills.IsSpeedBreakOverrideActive)
				Player.MoveSpeed = ActiveMovementSettings.UpdateInterpolate(Player.Skills.speedBreakSpeed, 1.0f);
			return;
		}

		float inputStrength = Player.Controller.GetInputStrength();
		if (Player.IsLockoutActive)
		{
			// Process Lockouts
			if (Player.ActiveLockoutData.overrideSpeed)
			{
				Player.MoveSpeed = Player.ActiveLockoutData.ApplySpeed(Player.MoveSpeed, ActiveMovementSettings);
				return;
			}

			if (Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe)
			{
				Player.MoveSpeed = ActiveMovementSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
				return;
			}
		}

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun)) // Always move at full power when autorun is enabled
			inputStrength = 1;

		if (Mathf.IsZeroApprox(inputStrength)) // Basic slow down
		{
			Deccelerate();
			return;
		}

		float inputAngle = Player.Controller.GetTargetInputAngle();
		float targetMovementAngle = Player.Controller.GetTargetMovementAngle();
		if ((Player.Controller.IsHoldingDirection(inputAngle, Player.MovementAngle + Mathf.Pi) && !Mathf.IsZeroApprox(Player.MoveSpeed)) ||
			Input.IsActionPressed("button_brake")) // Turning around
		{
			Brake();
			return;
		}

		if (Player.IsLockoutActive && Player.ActiveLockoutData.spaceMode == LockoutResource.SpaceModes.PathFollower) // Zipper exception
		{
			// Arbitrary math to make it easier to maintain speed
			float inputDot = Mathf.Abs(ExtensionMethods.DotAngle(Player.MovementAngle, targetMovementAngle));
			inputStrength *= Mathf.Clamp(inputDot + .5f, 0, 1f);
		}

		Accelerate(inputStrength);
	}

	protected virtual void Deccelerate() => Player.MoveSpeed = ActiveMovementSettings.UpdateInterpolate(Player.MoveSpeed, 0);

	protected virtual void Accelerate(float inputStrength)
	{
		if (Player.MoveSpeed < Player.Stats.BackstepSettings.Speed) // Accelerate faster when at low speeds
			Player.MoveSpeed = Mathf.Lerp(Player.MoveSpeed, ActiveMovementSettings.Speed * ActiveMovementSettings.GetSpeedRatio(Player.Stats.BackstepSettings.Speed), .05f * inputStrength);

		Player.MoveSpeed = ActiveMovementSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength); // Accelerate based on input strength/input direction
	}

	protected virtual void Brake() => Player.MoveSpeed = ActiveMovementSettings.UpdateInterpolate(Player.MoveSpeed, -1);

	protected float strafeBlend;
	protected float turningVelocity;
	protected virtual void ProcessTurning()
	{
		if (Mathf.IsZeroApprox(Player.MoveSpeed) && Input.IsActionPressed("button_brake"))
			return;

		bool isUsingStrafeControls = Player.Skills.IsSpeedBreakActive ||
			SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) ||
			(Player.IsLockoutActive &&
			Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe); // Ignore path delta under certain lockout situations

		float pathControlAmount = Player.PathTurnInfluence;
		if (isUsingStrafeControls || Player.IsLockoutActive)
			pathControlAmount = 0; // Don't use path influence during speedbreak/autorun

		float targetMovementAngle = Player.Controller.GetTargetMovementAngle() + pathControlAmount;
		if (Player.IsLockoutActive &&
			Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace) // Direction is being overridden
		{
			Player.MovementAngle = targetMovementAngle;
		}

		if (turnInstantly) // Instantly set movement angle to target movement angle
		{
			SnapRotation(targetMovementAngle);
			return;
		}

		if (Player.Controller.IsHoldingDirection(targetMovementAngle, Player.MovementAngle + Mathf.Pi))
		{
			// Check for turning around
			if (!Player.IsLockoutActive || Player.ActiveLockoutData.movementMode != LockoutResource.MovementModes.Strafe)
				return;
		}

		float speedRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed);
		targetMovementAngle = ProcessTargetMovementAngle(targetMovementAngle);

		// Normal turning
		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, Player.PathFollower.ForwardAngle);
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.ForwardAngle);
		bool isRecentering = Player.Controller.IsRecentering(movementDeltaAngle, inputDeltaAngle);
		float maxTurnAmount = isRecentering ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;

		float turnSmoothing = Mathf.Lerp(Player.Stats.MinTurnAmount, maxTurnAmount, speedRatio);
		Player.MovementAngle += pathControlAmount;
		Turn(targetMovementAngle, turnSmoothing);

		// Strafe implementation
		if (isUsingStrafeControls)
			ProcessStrafe(targetMovementAngle);
	}

	protected virtual void ProcessStrafe(float targetMovementAngle)
	{
		if (Mathf.IsZeroApprox(Player.Controller.GetInputStrength()))
			strafeBlend = Mathf.MoveToward(strafeBlend, 1.0f, PhysicsManager.physicsDelta);
		else
			strafeBlend = 0;

		if (!Player.IsLockoutActive)
			Player.MovementAngle += Player.PathFollower.DeltaAngle;
		Player.MovementAngle = Mathf.LerpAngle(Player.MovementAngle, targetMovementAngle, strafeBlend);
	}

	protected virtual void Turn(float targetMovementAngle, float smoothing) => Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle, targetMovementAngle, ref turningVelocity, smoothing);

	protected virtual float ProcessTargetMovementAngle(float targetMovementAngle) => Player.Controller.ImproveAnalogPrecision(targetMovementAngle, Player.PathFollower.ForwardAngle);

	protected virtual void SnapRotation(float targetMovementAngle)
	{
		turningVelocity = 0;
		Player.MovementAngle = targetMovementAngle;
	}
}
