using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Handles the Pterosaurs at the end of Dinosaur Jungle Act 1. </summary>
public partial class DinoPtero : Node3D
{
	[Export]
	private AnimationPlayer animator;

	private readonly StringName FlapAnimation = "flap";
	private readonly StringName GlideAnimation = "glide";

	public override void _Ready()
	{
		animator.Play(FlapAnimation);
		// Give each ptero a different offset
		animator.Seek(animator.CurrentAnimationLength * Runtime.randomNumberGenerator.Randf(), true);
	}

	private void UpdateAnimation()
	{
		float nextAnimation = Runtime.randomNumberGenerator.Randf();

		if (nextAnimation < .5f)
			return;

		if (nextAnimation < .6f)
		{
			animator.Play(GlideAnimation);
			animator.SpeedScale = 1.0f;
			return;
		}

		animator.Play(FlapAnimation);
		animator.SpeedScale = Mathf.Lerp(1, 3f, Runtime.randomNumberGenerator.Randf());
	}
}
