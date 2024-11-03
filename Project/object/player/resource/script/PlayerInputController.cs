using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerInputController : Node
{
	private PlayerController Player { get; set; }
	public void Initialize(PlayerController player) => Player = player;

	[Export]
	private Curve InputCurve { get; set; }
	public float GetInputStrength()
	{
		float inputLength = InputAxis.Length();
		if (inputLength <= DeadZone)
			inputLength = 0;

		if (Player.IsLockoutActive && Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Replace)
		{
			float inputDot = Mathf.Abs(ExtensionMethods.DotAngle(GetTargetInputAngle(), GetTargetMovementAngle()));
			if (!Mathf.IsZeroApprox(inputLength) && inputDot < .2f) // Fixes player holding perpendicular to target direction
				return 0;
		}

		return InputCurve.Sample(inputLength);
	}

	public float DeadZone => SaveManager.Config.deadZone;

	private float jumpBuffer;
	public bool IsJumpBufferActive => !Mathf.IsZeroApprox(jumpBuffer);
	private readonly float InputBufferLength = .2f;
	public void ResetJumpBuffer() => jumpBuffer = 0;

	private float actionBuffer;
	public bool IsActionBufferActive => !Mathf.IsZeroApprox(actionBuffer);
	public void ResetActionBuffer() => actionBuffer = 0;

	/// <summary> Angle to use when transforming from world space to camera space. </summary>
	public float XformAngle { get; set; }
	public Vector2 InputAxis { get; private set; }
	public Vector2 NonZeroInputAxis { get; private set; }
	public float InputHorizontal { get; private set; }
	public float InputVertical { get; private set; }

	/// <summary> Minimum angle from PathFollower.ForwardAngle that counts as backstepping/moving backwards. </summary>
	private readonly float MinBackStepAngle = Mathf.Pi * .6f;
	/// <summary> Maximum angle that counts as holding a direction. </summary>
	private const float MaximumHoldDelta = Mathf.Pi * .4f;

	/// <summary> Maximum amount the player can turn when running at full speed. </summary>
	public readonly float TurningDampingRange = Mathf.Pi * .35f;

	public void ProcessInputs()
	{
		InputAxis = Input.GetVector("move_left", "move_right", "move_up", "move_down", DeadZone);
		InputHorizontal = Input.GetAxis("move_left", "move_right");
		InputVertical = Input.GetAxis("move_up", "move_down");
		if (!InputAxis.IsZeroApprox())
			NonZeroInputAxis = InputAxis;

		if (!Player.IsLockoutDisablingActions)
		{
			UpdateJumpBuffer();
			UpdateActionBuffer();
		}
		else
		{
			ResetJumpBuffer();
			ResetActionBuffer();
		}
	}

	private void UpdateJumpBuffer()
	{
		if (Input.IsActionJustPressed("button_jump"))
		{
			jumpBuffer = InputBufferLength;
			return;
		}

		jumpBuffer = Mathf.MoveToward(jumpBuffer, 0, PhysicsManager.physicsDelta);
	}

	private void UpdateActionBuffer()
	{
		if (Input.IsActionJustPressed("button_action"))
		{
			actionBuffer = InputBufferLength;
			return;
		}

		actionBuffer = Mathf.MoveToward(actionBuffer, 0, PhysicsManager.physicsDelta);
	}

	/// <summary> Returns the angle between the player's input angle and movementAngle. </summary>
	public float GetTargetMovementAngle() => CalculateLockoutForwardAngle(GetTargetInputAngle());

	public float CalculatePathControlAmount()
	{
		if (IsStrafeModeActive || Player.IsLockoutActive)
			return 0; // Don't use path influence during speedbreak/autorun

		return Player.PathTurnInfluence;
	}

	/// <summary> Returns whether the player is currently in strafing mode. </summary>
	public bool IsStrafeModeActive => Player.Skills.IsSpeedBreakActive ||
			SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun) ||
			(Player.IsLockoutActive &&
			Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe);

	/// <summary> Returns the automaticly calculated input angle based on the game's settings and skills. </summary>
	public float GetTargetInputAngle()
	{
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun))
			return NonZeroInputAxis.Rotated(Player.PathFollower.ForwardAngle).AngleTo(Vector2.Down);

		return NonZeroInputAxis.Rotated(-XformAngle).AngleTo(Vector2.Down);
	}

	private float CalculateLockoutForwardAngle(float inputAngle)
	{
		if (Player.Skills.IsSpeedBreakCharging)
			return Player.PathFollower.ForwardAngle;

		LockoutResource resource = Player.ActiveLockoutData;
		if (Player.IsLockoutOverridingMovementAngle)
		{
			if (Player.ActiveLockoutData.movementMode == LockoutResource.MovementModes.Strafe)
				return GetStrafeAngle();

			float forwardAngle = Player.ActiveLockoutData.movementAngle;
			switch (resource.spaceMode)
			{
				case LockoutResource.SpaceModes.Local:
					forwardAngle += Player.MovementAngle;
					break;
				case LockoutResource.SpaceModes.Camera:
					forwardAngle += XformAngle;
					break;
				case LockoutResource.SpaceModes.PathFollower:
					forwardAngle += Player.PathFollower.ForwardAngle;
					break;
			}

			if (resource.allowReversing)
			{
				float backwardsAngle = forwardAngle + Mathf.Pi;
				if ((!Mathf.IsZeroApprox(Player.MoveSpeed) && Player.IsMovingBackward) ||
					(Mathf.IsZeroApprox(Player.MoveSpeed) && IsHoldingDirection(inputAngle, backwardsAngle)))
				{
					return backwardsAngle;
				}
			}

			return forwardAngle;
		}

		if (Player.Skills.IsSpeedBreakActive)
			return GetStrafeAngle();

		if (Mathf.IsZeroApprox(GetInputStrength()))
			return Player.MovementAngle;

		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun))
			return GetStrafeAngle(true);

		return inputAngle;
	}

	private float GetStrafeAngle(bool allowBackstep = false)
	{
		CameraSettingsResource.ControlModeEnum controlMode = Player.Camera.ActiveSettings.controlMode;
		Vector2 inputs = InputAxis;

		if (controlMode == CameraSettingsResource.ControlModeEnum.Sidescrolling)
			GD.PushWarning("Sidescrolling Control Mode Hasn't Been Implemented!");

		if (controlMode == CameraSettingsResource.ControlModeEnum.Reverse) // Transform inputs based on the control mode
			inputs.X *= -1;

		float baseAngle = Player.PathFollower.ForwardAngle;
		if (allowBackstep && SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun)) // Check for backstep
		{
			if (controlMode == CameraSettingsResource.ControlModeEnum.Reverse) // Transform inputs based on the control mode
				inputs.Y *= -1;

			if (inputs.Y > SaveManager.Config.deadZone)
				baseAngle = Player.PathFollower.BackAngle;
		}

		float strafeAngle = inputs.X * TurningDampingRange;
		if (Player.IsMovingBackward)
			strafeAngle *= -1;

		return baseAngle - strafeAngle;
	}

	/// <summary> Checks whether the player is holding a particular direction. </summary>
	public bool IsHoldingDirection(float inputAngle, float referenceAngle, float maximumDelta = MaximumHoldDelta)
	{
		float deltaAngle = ExtensionMethods.DeltaAngleRad(inputAngle, referenceAngle);
		return deltaAngle <= maximumDelta;
	}

	/// <summary> Returns how far the player's input is from the reference angle, normalized to MinBackStepAngle. </summary>
	public float GetHoldingDistance(float inputAngle, float referenceAngle)
	{
		float deltaAngle = ExtensionMethods.DeltaAngleRad(referenceAngle, inputAngle);
		return deltaAngle / MinBackStepAngle;
	}

	/// <summary>
	/// Remaps controller inputs when holding forward to provide more analog detail.
	/// </summary>
	public float ImproveAnalogPrecision(float inputAngle, float referenceAngle)
	{
		if (!Runtime.Instance.IsUsingController || !IsHoldingDirection(inputAngle, referenceAngle))
			return inputAngle;

		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(inputAngle, referenceAngle);
		if (Mathf.Abs(deltaAngle) < TurningDampingRange)
			inputAngle -= deltaAngle * .5f;

		return inputAngle;
	}

	/// <summary>
	/// Returns true if the player is trying to recenter themselves.
	/// </summary>
	public bool IsRecentering(float movementDeltaAngle, float inputDeltaAngle)
	{
		return Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) ||
			Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle);
	}
}
