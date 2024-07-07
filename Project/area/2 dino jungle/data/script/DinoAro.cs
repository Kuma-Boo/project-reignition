using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Handles the Arosaurs at the end of Dinosaur Jungle Act 1. </summary>
public partial class DinoAro : Node3D
{
	[Export]
	private AnimationPlayer animator;

	public override void _Ready()
	{
		animator.Play("idle");
		// Give each aro a different offset
		animator.Seek(animator.CurrentAnimationLength * Runtime.randomNumberGenerator.Randf(), true);
	}
}
