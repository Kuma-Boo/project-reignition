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
		Player.VerticalSpeed = Runtime.CalculateJumpPower(jumpHeight);
		Player.ApplyMovement();

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
			Player.VerticalSpeed *= jumpCurve; // Kill jump height
		}
		else if (!isAccelerationJump)
		{
			jumpTimer += PhysicsManager.physicsDelta;

			if (isAccelerationJumpQueued && jumpTimer > AccelerationJumpLength)
				StartAccelerationJump();
		}

		Player.VerticalSpeed = Mathf.MoveToward(Player.VerticalSpeed, Runtime.MaxGravity, Runtime.Gravity * PhysicsManager.physicsDelta);
		Player.ApplyMovement();

		if (Player.CheckGround())
			return landState;

		if (Player.VerticalSpeed <= 0)
			return fallState;

		return null;
	}

	private void StartAccelerationJump()
	{
		// Keep acceleration jump heights consistent
		Player.MoveSpeed = accelerationJumpSpeed;
		Player.VerticalSpeed = accelerationJumpHeightVelocity;
		isAccelerationJump = true;
	}
}
