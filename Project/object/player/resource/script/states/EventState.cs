using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay;

public partial class EventState : PlayerState
{
	public EventTrigger Trigger { get; set; }

	[Export] private PlayerState idleState;
	[Export] private PlayerState runState;
	[Export] private PlayerState fallState;

	private bool isEventFinished;

	public override void EnterState()
	{
		isEventFinished = false;
		Trigger.EventFinished += FinishEvent;
		Player.Skills.DisableBreakSkills();

		// Not a "real" event; Don't alter the player
		if (Trigger.PlayerStandin == null)
			return;

		BGMPlayer.SetStageMusicVolume(-80f); // Mute BGM

		Player.StartExternal(this, Trigger.PlayerStandin, Trigger.CharacterPositionSmoothing);
		Player.Controller.ResetJumpBuffer();
		Player.Animator.ExternalAngle = 0; // Reset external angle
		Player.Animator.SnapRotation(Player.Animator.ExternalAngle);

		if (Player.Animator.IsOneshotAnimationValid(Trigger.EventID))
			Player.Animator.PlayOneshotAnimation(Trigger.EventID);
	}

	public override void ExitState()
	{
		if (Trigger.PlayerStandin != null)
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
		}

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
			if (Trigger.PlayerStandin != null)
				Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);

			return null;
		}

		if (Trigger.PlayerStandin == null)
			return null;

		if (!Player.CheckGround())
			return fallState;

		if (Mathf.IsZeroApprox(Player.MoveSpeed))
			return idleState;

		return runState;
	}

	private bool IsSkippingEvent()
	{
		if (!Player.Controller.IsJumpBufferActive)
			return false;

		return SaveManager.ActiveGameData.CanSkipCutscene(Trigger.EventID) || OS.IsDebugBuild();
	}

	private void SkipEvent()
	{
		Player.Controller.ResetJumpBuffer();

		Trigger.SkipEvent();
		if (!string.IsNullOrEmpty(Trigger.EventID))
			Player.Animator.SeekOneshotAnimation(Trigger.AnimationLength);
	}

	private void FinishEvent()
	{
		isEventFinished = true;
		SaveManager.ActiveGameData.AllowSkippingCutscene(Trigger.EventID);
	}
}
