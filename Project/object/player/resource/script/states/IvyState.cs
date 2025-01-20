using Godot;
using Project.Gameplay.Objects;

namespace Project.Gameplay;

public partial class IvyState : PlayerState
{
	public Ivy Trigger { get; set; }

	private float currentAnimationBlend;
	private float animationBlendVelocity;
	private float animationBlendSmoothing = .2f;

	private readonly float HighSpeedSwingStrength = .8f;
	private readonly float InitialSwingStrength = .6f;
	private readonly float AdditionalSwingStrength = .2f;

	/// <summary> Determines whether the ivy should have some force automatically applied when starting. </summary>
	private bool isHighSpeedEntry;
	public void UpdateHighSpeedEntry() => isHighSpeedEntry = Player.IsJumpDashOrHomingAttack ||
		Player.MoveSpeed > Player.Stats.baseGroundSpeed * .5f;

	private readonly StringName JumpAction = "action_jump";
	private readonly StringName SwingAction = "action_swing";

	public override void EnterState()
	{
		currentAnimationBlend = 0;
		animationBlendVelocity = 0;

		float initialForce;
		if (isHighSpeedEntry)
			initialForce = HighSpeedSwingStrength;
		else
			initialForce = InitialSwingStrength * (Player.MoveSpeed / Player.Stats.baseGroundSpeed);
		if (!Trigger.IsSleeping && !Trigger.IsSwingingForward)
			initialForce *= -1;

		Trigger.AddForce(initialForce);

		Player.MoveSpeed = 0;
		Player.StartExternal(Trigger, Trigger.LaunchPoint, 0.2f);

		Player.Lockon.IsMonitoring = false;
		Player.Animator.StartIvy();

		HeadsUpDisplay.Instance.SetPrompt(SwingAction, 0);
		HeadsUpDisplay.Instance.SetPrompt(JumpAction, 1);
		HeadsUpDisplay.Instance.ShowPrompts();
	}

	public override void ExitState()
	{
		Player.StopExternal();
		HeadsUpDisplay.Instance.HidePrompts();
	}

	public override PlayerState ProcessPhysics()
	{
		if (Player.Controller.IsJumpBufferActive)
		{
			Player.Controller.ResetJumpBuffer();
			Trigger.Activate();
			return null;
		}

		if (Player.Controller.IsActionBufferActive && !Player.Animator.IsIvySwingActive)
		{
			Player.Controller.ResetActionBuffer();
			Player.Animator.StartIvySwing();

			Trigger.AddForce(CalculateSwingForce());
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
			return InitialSwingStrength;

		if (Trigger.IsSwingingForward)
			return AdditionalSwingStrength;

		if (Trigger.TargetSwingStrength < AdditionalSwingStrength)
			return -Trigger.TargetSwingStrength * .5f;

		return -AdditionalSwingStrength;
	}

	private void CalculateAnimationBlend()
	{
		float targetAnimationBlend = (1f - (Trigger.GetLaunchRatio() * 2)) * Mathf.Abs(Trigger.LaunchRatio);
		currentAnimationBlend = ExtensionMethods.SmoothDamp(currentAnimationBlend, targetAnimationBlend, ref animationBlendVelocity, animationBlendSmoothing);
	}
}
