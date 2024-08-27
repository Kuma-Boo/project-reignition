using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class PlayerController : CharacterBody3D
{
	[Export]
	public PlayerInputController Controller { get; private set; }
	[Export]
	public PlayerStatsController Stats { get; private set; }
	[Export]
	public PlayerStateMachine StateMachine { get; private set; }
	[Export]
	public PlayerPathController PathFollower { get; private set; }

	/// <summary> Player's horizontal movespeed, ignoring slopes. </summary>
	public float MoveSpeed { get; set; }
	/// <summary> Player's vertical speed -- only effective when not on the ground. </summary>
	public float VerticalSpeed { get; set; }
	public bool IsMovingBackward { get; set; }

	/// <summary> Global movement angle, in radians. Note - VISUAL ROTATION is controlled by CharacterAnimator.cs. </summary>
	public float MovementAngle { get; set; }
	public float PathTurnInfluence => 0; // REFACTOR TODO PathFollower.DeltaAngle * Camera.ActiveSettings.pathControlInfluence;
	public Vector3 GetMovementDirection()
	{
		float deltaAngle = ExtensionMethods.SignedDeltaAngleRad(MovementAngle, PathFollower.ForwardAngle);
		return PathFollower.Forward().Rotated(UpDirection, deltaAngle);
	}

	public override void _Ready()
	{
		StateMachine.Initialize(this);
		PathFollower.Initialize(this);
		Stats.Initialize();
	}

	public override void _PhysicsProcess(double _)
	{
		Controller.ProcessInputs();
		StateMachine.ProcessPhysics();
		PathFollower.Resync();
	}

	public bool CheckGround() => IsOnFloor();

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
			return Controller.CameraInputAxis.AngleTo(Vector2.Down);

		if (mode == InputMode.Path)
			return Controller.InputAxis.Rotated(PathFollower.ForwardAngle).AngleTo(Vector2.Down);

		return Controller.InputAxis.AngleTo(Vector2.Down);
	}

	/// <summary> Returns the automatic input mode [based on the game's settings and] skills. </summary>
	public InputMode GetAutomaticInputMode()
	{
		// TODO Add configuration option for path based inputs
		if (SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.Autorun))
			return InputMode.Path;

		return InputMode.Camera;
	}

	/*
	[Export]
	public CameraController Camera { get; private set; }
	[Export]
	public CharacterAnimator Animator { get; private set; }
	[Export]
	public CharacterEffect Effect { get; private set; }
	[Export]
	public CharacterLockon Lockon { get; private set; }
	*/
}

/// <summary>
/// Contains data of movement settings. Leave values at -1 to ignore (primarily for skill overrides)
/// </summary>
public class MovementSetting
{
	public float Speed { get; set; }
	public float Traction { get; set; } // Speed up rate
	public float Friction { get; set; } // Slow down rate
	public float Overspeed { get; set; } // Slow down rate when going faster than speed
	public float Turnaround { get; set; } // Skidding

	public MovementSetting()
	{
		Speed = 0;
		Traction = 0;
		Friction = 0;
	}

	/// <summary> Interpolates between speeds based on input. </summary>
	public float UpdateInterpolate(float currentSpeed, float input)
	{
		float delta = Traction;
		float targetSpeed = Speed * input;
		targetSpeed = Mathf.Max(targetSpeed, 0);

		if (Mathf.Abs(currentSpeed) > Speed)
			delta = Overspeed;

		if (input == 0) // Deccelerate
			delta = Friction;
		else if (!Mathf.IsZeroApprox(currentSpeed) && Mathf.Sign(targetSpeed) != Mathf.Sign(Speed)) // Turnaround
			delta = Turnaround;

		return Mathf.MoveToward(currentSpeed, targetSpeed, delta * PhysicsManager.physicsDelta);
	}

	/// <summary> Special addition mode for Sliding. Does NOT support negative speeds. </summary>
	public float UpdateSlide(float currentSpeed, float input)
	{
		bool clampFinalSpeed = Mathf.Abs(currentSpeed) <= Speed;
		if (Mathf.Abs(currentSpeed) > Speed) // Reduce by overspeed
		{
			currentSpeed -= Overspeed * PhysicsManager.physicsDelta;
			if (Mathf.Abs(currentSpeed) > Speed && (Mathf.IsZeroApprox(input) || input > 0)) // Allow overspeed sliding
				return currentSpeed;
		}

		if (input > 0) // Accelerate
		{
			if (!clampFinalSpeed)
				currentSpeed = Speed;
			else
				currentSpeed += Traction * input * PhysicsManager.physicsDelta;
		}
		else
		{
			// Deccelerate and Turnaround
			currentSpeed -= Mathf.Lerp(Friction, Turnaround, Mathf.Abs(input)) * PhysicsManager.physicsDelta;
			clampFinalSpeed = Mathf.Abs(currentSpeed) <= Speed;
		}

		if (clampFinalSpeed)
			currentSpeed = Mathf.Clamp(currentSpeed, 0, Speed);

		return currentSpeed;
	}

	public float GetSpeedRatio(float spd) => spd / Speed;
	public float GetSpeedRatioClamped(float spd) => Mathf.Clamp(GetSpeedRatio(spd), -1f, 1f);
}