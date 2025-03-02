using Godot;

namespace Project.Gameplay.Objects;

[Tool]
public partial class Spring : Launcher
{
	[Export] private bool startHidden;
	[Export] private AnimationPlayer animator;

	private readonly StringName LaunchAnim = "launch";
	private readonly StringName ShowAnim = "show";
	private readonly StringName HideAnim = "hide";

	protected override void SetUp()
	{
		base.SetUp();

		if (Engine.IsEditorHint())
			return;

		if (startHidden)
		{
			animator.Play(HideAnim);
			animator.Seek(animator.CurrentAnimationLength, true, true);
		}
	}

	protected override void LaunchAnimation()
	{
		animator.Play(LaunchAnim);
		base.LaunchAnimation();
	}

	public void ShowLauncher() => animator.Play(ShowAnim);
	public void HideLauncher() => animator.Play(HideAnim);
}