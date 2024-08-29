using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerInputController : Node
{
	private PlayerController Player { get; set; }
	public void RegisterPlayer(PlayerController player) => Player = player;

	[Export]
	private Curve InputCurve { get; set; }
	public float GetInputStrength()
	{
		if (Input.IsActionPressed("button_brake"))
			return 0;

		float inputLength = InputAxis.Length();
		if (inputLength <= DeadZone)
			inputLength = 0;
		return InputCurve.Sample(inputLength);
	}

	public float DeadZone => SaveManager.Config.deadZone;

	private float jumpBuffer;
	public bool IsJumpBufferActive => !Mathf.IsZeroApprox(jumpBuffer);
	public void ResetJumpBuffer() => jumpBuffer = 0;

	private float actionBuffer;
	public bool IsActionBufferActive => !Mathf.IsZeroApprox(actionBuffer);
	public void ResetActionBuffer() => actionBuffer = 0;

	private readonly float InputBufferLength = .2f;

	public Vector2 CameraInputAxis { get; private set; }
	public Vector2 InputAxis { get; private set; }
	public float InputHorizontal { get; private set; }
	public float InputVertical { get; private set; }

	/// <summary> Maximum angle that counts as holding a direction. </summary>
	private readonly float MaximumHoldDelta = Mathf.Pi * .4f;

	/// <summary> Maximum amount the player can turn when running at full speed. </summary>
	public readonly float TurningDampingRange = Mathf.Pi * .35f;

	public void ProcessInputs()
	{
		InputAxis = Input.GetVector("move_left", "move_right", "move_up", "move_down", DeadZone);
		InputHorizontal = Input.GetAxis("move_left", "move_right");
		InputVertical = Input.GetAxis("move_up", "move_down");

		CameraInputAxis = InputAxis; // TODO Update based on camera yaw rotation.

		UpdateJumpBuffer();
		UpdateActionBuffer();
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

	public enum InputMode
	{
		Auto, // Calls GetAutomaticInputMode
		Camera, // Inputs are rotated with the camera
		Path, // Inputs are rotated with the path (Up is always forward)
		Global, // I think this is unused for now.
	}

	/// <summary> Gets the dot angle between the player's input angle and movementAngle. </summary>
	public float GetTargetMovementAngle(InputMode mode = InputMode.Auto)
	{
		if (mode == InputMode.Auto)
			mode = GetAutomaticInputMode();

		if (mode == InputMode.Camera)
			return CameraInputAxis.AngleTo(Vector2.Down);

		if (mode == InputMode.Path)
			return InputAxis.Rotated(Player.PathFollower.ForwardAngle).AngleTo(Vector2.Down);

		return InputAxis.AngleTo(Vector2.Down);
	}

	/// <summary> Returns the automatic input mode [based on the game's settings and] skills. </summary>
	public InputMode GetAutomaticInputMode()
	{
		// TODO Add configuration option for path based inputs
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun))
			return InputMode.Path;

		return InputMode.Camera;
	}

	/// <summary>
	/// Checks whether the player is holding a particular direction.
	/// </summary>
	public bool IsHoldingDirection(float inputAngle, float referenceAngle)
	{
		float deltaAngle = ExtensionMethods.DeltaAngleRad(referenceAngle, inputAngle);
		return deltaAngle <= MaximumHoldDelta;
	}

	/// <summary>
	/// Remaps controller inputs when holding forward to provide more analog detail.
	/// </summary>
	public float ImproveAnalogPrecision(float inputAngle, float referenceAngle)
	{
		if (!Runtime.Instance.IsUsingController || IsHoldingDirection(inputAngle, referenceAngle))
			return inputAngle;

		float deltaAngle = ExtensionMethods.DeltaAngleRad(referenceAngle, inputAngle);
		if (deltaAngle < TurningDampingRange)
			inputAngle -= deltaAngle * .5f;

		return inputAngle;
	}

	/// <summary>
	/// Returns true if the player is trying to recenter themselves.
	/// </summary>
	public bool IsRecentering(float movementDeltaAngle, float inputDeltaAngle) => Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) || Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle);
}
