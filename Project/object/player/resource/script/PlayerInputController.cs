using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerInputController : Node
{
	[Export]
	private Curve InputCurve { get; set; }
	public float GetInputStrength() => InputCurve.Sample(InputAxis.Length());

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

	public bool IsHoldingDirection(float reference)
	{
		GD.PushError("Unimplmented.");
		return false;
	}
}
