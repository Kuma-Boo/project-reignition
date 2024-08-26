using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class BackflipState : PlayerState
{
	[Export]
	public PlayerState landState;
	[Export]
	public float backflipHeight;

	private float turningVelocity;
	/// <summary> How much can the player adjust their angle while backflipping? </summary>
	private readonly float MaxBackflipAdjustment = Mathf.Pi * .25f;

	public override void EnterState()
	{
		turningVelocity = 0;
		Player.MoveSpeed = Player.Stats.BackflipSettings.Speed;
		Player.IsMovingBackward = true;
		Player.VerticalSpeed = Runtime.CalculateJumpPower(backflipHeight);

		/*
			REFACTOR TODO Add Effects
			Effect.PlayActionSFX(Effect.JumpSfx);
			Animator.BackflipAnimation();

			if (Skills.IsSkillEquipped(SkillKey.BackstepAttack))
			{
				Effect.PlayFireFX();
				AttackState = AttackStates.Weak;
			}
		*/
	}

	public override PlayerState ProcessPhysics()
	{
		UpdateMoveSpeed();

		if (Player.CheckGround())
			return landState;

		return null;
	}

	private void UpdateMoveSpeed()
	{
		float inputStrength = Player.Controller.GetInputStrength();
		float targetMovementAngle = ExtensionMethods.ClampAngleRange(Player.GetTargetMovementAngle(), Player.PathFollower.BackAngle, MaxBackflipAdjustment);
		bool isHoldingForward = Player.Controller.IsHoldingDirection(targetMovementAngle, Player.PathFollower.ForwardAngle);// REFACTOR TODO: Extra arguments? , true, false);
		bool isHoldingBackward = Player.Controller.IsHoldingDirection(targetMovementAngle, Player.PathFollower.BackAngle);
		if (isHoldingForward || Input.IsActionPressed("button_brake"))
		{
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, -1);
			return;
		}

		if (isHoldingBackward)
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
		else if (inputStrength <= Player.Controller.DeadZone)
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, 0);

		UpdateTurning(targetMovementAngle);
	}

	private void UpdateTurning(float targetMovementAngle)
	{
		targetMovementAngle += Player.PathTurnInfluence;
		targetMovementAngle = Player.Controller.ImproveAnalogPrecision(targetMovementAngle, Player.PathFollower.BackAngle);

		float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, Player.PathFollower.BackAngle);
		float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.BackAngle);
		// Is the player trying to recenter themselves?
		bool isRecentering = Player.Controller.IsRecentering(movementDeltaAngle, inputDeltaAngle);
		float turnAmount = isRecentering ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;
		Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle, targetMovementAngle, ref turningVelocity, turnAmount);
	}
}
