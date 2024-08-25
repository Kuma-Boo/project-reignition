using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class JumpState : PlayerState
{
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState landState;

	[Export]
	private float jumpHeight = 4.8f;
	[Export]
	private float jumpCurve = .95f;

	[Export]
	private float accelerationJumpSpeed = 36;
	[Export]
	private float accelerationJumpHeightVelocity = 5;

	private float jumpTimer;

	private bool isShortenedJump;
	private bool isAccelerationJump;
	private bool isAccelerationJumpQueued;

	/// <summary> How fast the jump button needs to be released to count as an "acceleration jump." </summary>
	private readonly float AccelerationJumpLength = .1f;

	public override void EnterState()
	{
		Controller.VerticalSpeed = Runtime.CalculateJumpPower(jumpHeight);
		Controller.ApplyMovement();

		jumpTimer = 0;
		isShortenedJump = false;
		isAccelerationJump = false;
		isAccelerationJumpQueued = false;
	}

	public override PlayerState ProcessPhysics()
	{
		if (!Input.IsActionPressed("button_jump"))
		{
			// Check for acceleration jump
			if (jumpTimer <= AccelerationJumpLength)
				isAccelerationJumpQueued = true;
			else
				isShortenedJump = true;
		}

		if (isShortenedJump)
		{
			Controller.VerticalSpeed *= jumpCurve; // Kill jump height
		}
		else if (!isAccelerationJump)
		{
			jumpTimer += PhysicsManager.physicsDelta;

			if (isAccelerationJumpQueued && jumpTimer > AccelerationJumpLength)
				StartAccelerationJump();
		}

		Controller.VerticalSpeed = Mathf.MoveToward(Controller.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
		Controller.ApplyMovement();

		if (Controller.CheckGround())
			return landState;

		if (Controller.VerticalSpeed <= 0)
			return fallState;

		return null;
	}

	private void StartAccelerationJump()
	{
		// Keep acceleration jump heights consistent
		Controller.MoveSpeed = accelerationJumpSpeed;
		Controller.VerticalSpeed = accelerationJumpHeightVelocity;
		isAccelerationJump = true;
	}
}
