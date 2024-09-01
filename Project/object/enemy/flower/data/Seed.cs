using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Spawned from a FlowerDjinn. Explodes on impact. </summary>
public partial class Seed : Area3D
{
	[Export]
	private float moveSpeed;
	[Export]
	private AnimationPlayer animator;

	private bool isMoving;
	private PlayerController Player => StageSettings.Player;

	public override void _PhysicsProcess(double _)
	{
		if (!isMoving) return;

		GlobalPosition += this.Forward() * moveSpeed * PhysicsManager.physicsDelta;
	}

	public void Spawn()
	{
		animator.Play("RESET");
		animator.Advance(0.0);

		animator.Play("move");
		isMoving = true;
	}

	private void Explode()
	{
		isMoving = false;
		animator.Play("explode");
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player")) return;

		Explode();

		/* REFACTOR TODO 
		if (Player.IsJumpDashOrHomingAttack ||
			Player.Lockon.IsHomingAttacking ||
			Player.Lockon.IsBounceLockoutActive)
		{
			Player.StartBounce();
			return;
		}
		*/

		Player.StartKnockback();
	}

	public void OnBodyEntered(PhysicsBody3D _) => Explode();
}