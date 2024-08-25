using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerController : CharacterBody3D
{
	[Export]
	public PlayerStateMachine StateMachine { get; private set; }
	[Export]
	public PlayerPathController PathFollower { get; private set; }

	/// <summary> Player's true movespeed, ignoring slopes. </summary>
	public float MoveSpeed { get; set; }
	/// <summary> Player's true vertical speed -- only effective when not on the ground. </summary>
	public float VerticalSpeed { get; set; }

	public override void _Ready()
	{
		StateMachine.Initialize(this);
		PathFollower.Initialize(this);
	}

	public override void _PhysicsProcess(double _)
	{
		StateMachine.ProcessPhysics();
		PathFollower.Resync();
	}

	public bool CheckGround()
	{
		return IsOnFloor();
	}

	#region Input Processing
	private float jumpBuffer;
	public bool IsJumpBufferActive => !Mathf.IsZeroApprox(jumpBuffer);
	public void ResetJumpBuffer() => jumpBuffer = 0;

	private float actionBuffer;
	public bool IsActionBufferActive => !Mathf.IsZeroApprox(actionBuffer);
	public void ResetActionBuffer() => actionBuffer = 0;

	private readonly float InputBufferLength = .2f;

	public Vector2 InputAxis { get; private set; }
	public float InputHorizontal { get; private set; }
	public float InputVertical { get; private set; }

	public void ProcessInputs()
	{
		InputAxis = Input.GetVector("move_left", "move_right", "move_up", "move_down", SaveManager.Config.deadZone);
		InputHorizontal = Input.GetAxis("move_left", "move_right");
		InputVertical = Input.GetAxis("move_up", "move_down");

		UpdateJumpBuffer();
		UpdateActionBuffer();
	}

	private void UpdateJumpBuffer()
	{
		if (Input.IsActionPressed("button_jump"))
		{
			jumpBuffer = InputBufferLength;
			return;
		}

		jumpBuffer = Mathf.MoveToward(jumpBuffer, 0, PhysicsManager.physicsDelta);
	}

	private void UpdateActionBuffer()
	{
		if (Input.IsActionPressed("button_action"))
		{
			actionBuffer = InputBufferLength;
			return;
		}

		actionBuffer = Mathf.MoveToward(actionBuffer, 0, PhysicsManager.physicsDelta);
	}
	#endregion

	[Export]
	public CameraController Camera { get; private set; }

	[Export]
	public CharacterAnimator Animator { get; private set; }
	[Export]
	public CharacterEffect Effect { get; private set; }
	[Export]
	public CharacterSkillManager Skills { get; private set; }
	[Export]
	public CharacterLockon Lockon { get; private set; }
}