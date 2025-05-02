using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

public partial class LightSpeedDashState : PlayerState
{
	[Export]
	private PlayerState landState;
	[Export]
	private PlayerState fallState;
	[Export]
	private PlayerState jumpState;
	[Export]
	private float lightDashSpeed;
	[Export]
	private float lightDashSpeedAcceleration;

	private bool allowJumpBuffer;

	public Node3D CurrentTarget { get; private set; }
	private readonly Array<Node3D> targetList = [];
	private readonly StringName LightDashNodeGroup = "light dashable";
	public Node3D GetNewTarget()
	{
		if (ValidateTarget(CurrentTarget)) // Already targeting something
			return CurrentTarget;

		// Find the closest target
		CurrentTarget = null;
		float closestDistanceSquared = Mathf.Inf;
		foreach (Node3D target in targetList)
		{
			if (!ValidateTarget(target))
				continue;

			float currentDistanceSquared = target.GlobalPosition.DistanceSquaredTo(Player.CenterPosition);
			if (currentDistanceSquared > closestDistanceSquared)
				continue;

			CurrentTarget = target;
			closestDistanceSquared = currentDistanceSquared;
		}

		return CurrentTarget;
	}

	private bool ValidateTarget(Node3D target)
	{
		if (target == null)
			return false;

		if (!targetList.Contains(target))
			return false;

		if (!target.IsInsideTree() || !target.IsVisibleInTree())
			return false;

		Vector3 direction = target.GlobalPosition - Player.CenterPosition;
		bool isValidFacingDirection = direction.Dot(Player.Animator.Forward()) >= 0.5f && direction.Dot(Player.PathFollower.ForwardAxis) >= 0.5f;

		return isValidFacingDirection;
	}

	public override void EnterState()
	{
		allowJumpBuffer = Player.IsOnGround;

		Player.IsOnGround = false;
		Player.AllowLandingSkills = true;
		Player.IsMovingBackward = false;

		Player.Lockon.IsMonitoring = false;
		Player.Controller.ResetLightDashBuffer(); // Always reset light dash input buffer
		Player.ChangeHitbox("light-dash");
		Player.AttackState = PlayerController.AttackStates.Weak;

		// Play animations
		Player.Effect.StartTrailFX();
		Player.Effect.StartLightDashFX();
		Player.Animator.StartLightDashAnimation();
	}

	public override void ExitState()
	{
		CurrentTarget = null;
		Player.Effect.StopTrailFX();
		Player.Effect.StopLightDashFX();
		Player.Animator.StopLightDashAnimation();
		Player.Animator.IsFallTransitionEnabled = false;

		Player.ChangeHitbox("RESET");
		Player.AttackState = PlayerController.AttackStates.None;
	}

	public override PlayerState ProcessPhysics()
	{
		if (GetNewTarget() == null || Player.IsOnWall)
		{
			Player.VerticalSpeed = 0f;
			Player.CheckGround();
			Player.MoveAndSlide();

			if (Player.Controller.IsJumpBufferActive && allowJumpBuffer)
			{
				Player.Controller.ResetJumpBuffer();

				float inputAngle = Player.Controller.GetTargetInputAngle();
				float inputStrength = Player.Controller.GetInputStrength();
				if (!Mathf.IsZeroApprox(inputStrength) &&
					Player.Controller.IsHoldingDirection(inputAngle, Player.PathFollower.BackAngle))
				{
					Player.MoveSpeed = 0;
				}

				return jumpState;
			}

			return Player.IsOnGround ? landState : fallState;
		}

		Vector3 movementDirection = (CurrentTarget.GlobalPosition - Player.CenterPosition).Normalized();
		Player.Velocity = movementDirection * Player.MoveSpeed;
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(movementDirection);
		Player.MoveSpeed = Mathf.MoveToward(Player.MoveSpeed, lightDashSpeed, lightDashSpeedAcceleration * PhysicsManager.physicsDelta);

		Player.MoveAndSlide();
		Player.CheckWall();
		Player.UpdateUpDirection(false, Player.PathFollower.Up());

		Player.Animator.SnapRotation(Player.MovementAngle);
		Player.PathFollower.Resync();

		return null;
	}

	public void OnAreaEntered(Area3D a)
	{
		if (!a.IsInGroup(LightDashNodeGroup))
			return;

		targetList.Add(a);
	}

	public void OnAreaExited(Area3D a)
	{
		if (!a.IsInGroup(LightDashNodeGroup))
			return;

		targetList.Remove(a);
	}
}
