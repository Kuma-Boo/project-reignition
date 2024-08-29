using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerController : CharacterBody3D
{
	[Signal]
	public delegate void LaunchFinishedEventHandler();

	[Export]
	public PlayerStateMachine StateMachine { get; private set; }
	[Export]
	public PlayerInputController Controller { get; private set; }
	[Export]
	public PlayerStatsController Stats { get; private set; }
	[Export]
	public PlayerStateController State { get; private set; }
	[Export]
	public PlayerSkillController Skills { get; private set; }
	[Export]
	public PlayerLockonController Lockon { get; private set; }
	[Export]
	public PlayerAnimator Animator { get; private set; }
	[Export]
	public PlayerEffect Effect { get; private set; }
	[Export]
	public PlayerPathController PathFollower { get; private set; }

	/// <summary> Player's horizontal movespeed, ignoring slopes. </summary>
	public float MoveSpeed { get; set; }
	/// <summary> Player's vertical speed -- only effective when not on the ground. </summary>
	public float VerticalSpeed { get; set; }
	public bool IsMovingBackward { get; set; }

	/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs. </summary>
	public float MovementAngle { get; set; }
	public float PathTurnInfluence => 0;// REFACTOR TODO PathFollower.DeltaAngle * Camera.ActiveSettings.pathControlInfluence;
	public Vector3 GetMovementDirection()
	{
		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
		return PathFollower.Forward().Rotated(UpDirection, deltaAngle);
	}

	public override void _Ready()
	{
		StageSettings.RegisterPlayer(this); // Update global reference

		StateMachine.Initialize(this);
		Stats.Initialize();
		State.Initialize(this);
		Lockon.Initialize(this);
		Animator.Initialize(this);
		PathFollower.Initialize(this);
	}

	public override void _PhysicsProcess(double _)
	{
		Controller.ProcessInputs();
		StateMachine.ProcessPhysics();
		Lockon.ProcessPhysics();
		Animator.ProcessPhysics();
		PathFollower.Resync();
	}

	public bool IsOnGround { get; private set; }
	public bool CheckGround()
	{
		// REFACTOR TODO Use Raycasts. There's currently a bug because CheckGround uses IsOnFloor() which only updates during MoveAndSlide().

		IsOnGround = IsOnFloor();
		return IsOnGround;
	}

	public void ApplyMovement()
	{
		Vector3 movementVelocity = Vector3.Zero;
		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
		Vector3 movementDirection = PathFollower.GlobalBasis.Z.Rotated(UpDirection, deltaAngle);
		movementVelocity += movementDirection * MoveSpeed;
		movementVelocity += UpDirection * VerticalSpeed;
		Velocity = movementVelocity;

		MoveAndSlide();
	}

	/// <summary> Size to use for collision checks. </summary>
	[Export]
	public Vector2 CollisionSize;
	/// <summary> Center of collision calculations </summary>
	public Vector3 CenterPosition
	{
		get => GlobalPosition + (UpDirection * .4f);
		set => GlobalPosition = value - (UpDirection * .4f);
	}
	public Vector3 CollisionPosition
	{
		get => GlobalPosition + (UpDirection * CollisionSize.Y);
		set => GlobalPosition = value - (UpDirection * CollisionSize.Y);
	}
	private const float CollisionPadding = .02f;

	public void UpdateUpDirection(bool quickReset = true, Vector3 upDirection = new())
	{
		// Calculate target up direction
		if (upDirection.IsEqualApprox(Vector3.Zero))
		{
			upDirection = Vector3.Up;
			/* REFACTOR TODO
			if (Camera.ActiveSettings.followPathTilt) // Use PathFollower.Up when on a tilted path.
				upDirection = PathFollower.Up();
			*/
		}

		// Calculate reset factor
		float resetFactor = .2f;
		if (!quickReset)
			resetFactor = VerticalSpeed > 0 ? .01f : VerticalSpeed * .2f / Runtime.MaxGravity;

		UpDirection = UpDirection.Lerp(upDirection, Mathf.Clamp(resetFactor, 0f, 1f)).Normalized();
	}

	/*
	[Export]
	public CameraController Camera { get; private set; }
	*/
}