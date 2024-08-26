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
		float inputStrength = Player.Controller.GetInputStrength();
		bool isHoldingForward = Player.Controller.IsHoldingDirection(Player.PathFollower.ForwardAngle);// REFACTOR TODO: Extra arguments? , true, false);
		if (isHoldingForward || Input.IsActionPressed("button_brake"))
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, -1);
		else if (Player.Controller.IsHoldingDirection(Player.PathFollower.BackAngle))
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, inputStrength);
		else if (inputStrength <= Player.Controller.DeadZone)
			Player.MoveSpeed = Player.Stats.BackflipSettings.UpdateInterpolate(Player.MoveSpeed, 0);

		if (!isHoldingForward)
		{
			float targetMovementAngle = ExtensionMethods.ClampAngleRange(Player.GetTargetMovementAngle(), Player.PathFollower.BackAngle, MaxBackflipAdjustment);
			float inputDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(targetMovementAngle, Player.PathFollower.BackAngle);
			// REFACTOR TODO Move to PlayerInputController.cs
			if (Runtime.Instance.IsUsingController &&
				Player.Controller.IsHoldingDirection(Player.PathFollower.BackAngle) &&
				Mathf.Abs(inputDeltaAngle) < Player.Controller.TurningDampingRange) // Remap controls to provide more analog detail
			{
				targetMovementAngle -= inputDeltaAngle * .5f;
			}

			// Normal turning
			float movementDeltaAngle = ExtensionMethods.SignedDeltaAngleRad(Player.MovementAngle, Player.PathFollower.BackAngle);
			// Is the player trying to recenter themselves?
			bool isTurningAround = !Player.Controller.IsHoldingDirection(Player.PathFollower.ForwardAngle) && (Mathf.Sign(movementDeltaAngle) != Mathf.Sign(inputDeltaAngle) || Mathf.Abs(movementDeltaAngle) > Mathf.Abs(inputDeltaAngle));
			float turnAmount = isTurningAround ? Player.Stats.RecenterTurnAmount : Player.Stats.MaxTurnAmount;
			Player.MovementAngle = ExtensionMethods.SmoothDampAngle(Player.MovementAngle, targetMovementAngle, ref turningVelocity, turnAmount);
		}

		if (Player.CheckGround())
			return landState;

		return null;
	}
}
