using Godot;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class IvyState : PlayerState
{
	public Ivy Trigger { get; set; }

	[Export] private float highSpeedSwingStrength;
	[Export] private float initialSwingStrength;
	[Export] private float additionalSwingStrength;

	private float currentAnimationBlend;
	private float animationBlendVelocity;
	private float animationBlendSmoothing = .2f;

	/// <summary> Determines whether the ivy should have some force automatically applied when starting. </summary>
	private bool isHighSpeedEntry;
	public void UpdateHighSpeedEntry() => isHighSpeedEntry = Player.IsJumpDashOrHomingAttack ||
		Player.MoveSpeed > Player.Stats.baseGroundSpeed * .5f;

	private readonly string JumpAction = "action_jump";
	private readonly string SwingAction = "action_swing";

	public override void EnterState()
	{
		currentAnimationBlend = 0;
		animationBlendVelocity = 0;

		float initialForce;
		if (isHighSpeedEntry)
			initialForce = highSpeedSwingStrength;
		else
			initialForce = highSpeedSwingStrength * (Player.MoveSpeed / Player.Stats.baseGroundSpeed);

		Trigger.AddImpulseForce(initialForce, true);

		Player.MoveSpeed = 0;
		Player.StartExternal(Trigger, Trigger.LaunchPoint, 0.2f);

		Player.Controller.ResetActionBuffer();
		Player.Skills.IsSpeedBreakEnabled = false;
		Player.Lockon.IsMonitoring = false;
		Player.Animator.StartIvy();

		HeadsUpDisplay.Instance.SetPrompt(SwingAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(JumpAction, 1);
		HeadsUpDisplay.Instance.ShowPrompts();
	}

	public override void ExitState()
	{
		Player.Skills.IsSpeedBreakEnabled = true;
		Player.StopExternal();
		HeadsUpDisplay.Instance.HidePrompts();
		Trigger.UnlinkReversePath(); // Clear any reverse paths
		Trigger = null;
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			Trigger.Activate();
			return null;
		}

		if (Player.Controller.IsGimmickBufferActive && (!Player.Animator.IsIvySwingActive))
		{
			Player.Controller.ResetGimmickBuffer();
			Player.Animator.StartIvySwing();
			Trigger.AddImpulseForce(CalculateSwingForce());
		}

		CalculateAnimationBlend();
		Player.Animator.SetIvyBlend(currentAnimationBlend);
		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, false);

		return null;
	}

	/// <summary> Calculates how much addition force to add based on swing state. </summary>
	private float CalculateSwingForce()
	{
		if (Trigger.IsSleeping)
			return initialSwingStrength;

		return additionalSwingStrength;
	}


	private void CalculateAnimationBlend()
	{
		float targetAnimationBlend = (1f - (Trigger.GetLaunchRatio() * 2)) * Mathf.Abs(Trigger.IvyRatio);
		currentAnimationBlend = ExtensionMethods.SmoothDamp(currentAnimationBlend, targetAnimationBlend, ref animationBlendVelocity, animationBlendSmoothing);
	}
}
