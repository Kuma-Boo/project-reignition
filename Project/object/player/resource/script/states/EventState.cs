using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay;

public partial class EventState : PlayerState
{
	public EventTrigger Trigger { get; set; }

	[Export]
	private PlayerState idleState;
	[Export]
	private PlayerState runState;
	[Export]
	private PlayerState fallState;

	private bool isEventFinished;

	public override void EnterState()
	{
		isEventFinished = false;
		BGMPlayer.SetStageMusicVolume(-80f); // Mute BGM

		Player.StartExternal(this, Trigger.PlayerStandin, Trigger.CharacterPositionSmoothing);
		Player.Controller.ResetJumpBuffer();
		Player.Animator.ExternalAngle = 0; // Reset external angle
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);
		Player.Skills.DisableBreakSkills();

		Trigger.EventFinished += FinishEvent;
		if (!Trigger.CharacterAnimation.IsEmpty)
			Player.Animator.PlayOneshotAnimation(Trigger.CharacterAnimation);
	}

	public override void ExitState()
	{
		BGMPlayer.SetStageMusicVolume(0f); // Unmute BGM

		Player.MoveSpeed = Trigger.NormalizeExitMoveSpeed ? Player.Stats.GroundSettings.Speed * Trigger.CharacterExitMoveSpeed : Trigger.CharacterExitMoveSpeed;
		Player.VerticalSpeed = Trigger.CharacterExitVerticalSpeed;
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(Player.ExternalParent.Forward());
		Player.Animator.SnapRotation(Player.MovementAngle);
		Player.Animator.CancelOneshot(Trigger.CharacterFadeoutTime);
		Player.Animator.DisabledSpeedSmoothing = true;
		Player.Animator.ResetState(0);
		Player.UpDirection = Vector3.Up;
		Player.UpdateOrientation(true);
		Player.StopExternal();

		if (Trigger.CharacterExitLockout != null)
			Player.AddLockoutData(Trigger.CharacterExitLockout);

		// Re-enable break skills
		Player.Skills.EnableBreakSkills();

		Trigger.EventFinished -= FinishEvent;
		Trigger = null;
	}

	public override PlayerState ProcessPhysics()
	{
		if (!isEventFinished)
		{
			if (IsSkippingEvent())
				SkipEvent();

			// Call deferred so sync happens AFTER event animator updates
			Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);
			return null;
		}

		Player.CheckGround();
		if (!Player.IsOnGround)
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		return runState;
	}

	private bool IsSkippingEvent()
	{
		if (!Player.Controller.IsJumpBufferActive)
			return false;

		return SaveManager.ActiveGameData.CanSkipCutscene(Trigger.CharacterAnimation);
	}

	private void SkipEvent()
	{
		Player.Controller.ResetJumpBuffer();

		Trigger.SkipEvent();
		if (!Trigger.CharacterAnimation.IsEmpty)
			Player.Animator.SeekOneshotAnimation(Trigger.AnimationLength);
	}

	private void FinishEvent()
	{
		isEventFinished = true;
		SaveManager.ActiveGameData.AllowSkippingCutscene(Trigger.CharacterAnimation);
	}
}
