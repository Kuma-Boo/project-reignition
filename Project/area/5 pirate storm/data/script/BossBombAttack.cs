using Godot;
using Project.Gameplay.Hazards;

namespace Project.Gameplay.Bosses;

[Tool]
public partial class BossBombAttack : BombMissile
{
	[Export] private Node3D lockonDecalRoot;
	private Vector3 targetPosition;

	public override void Respawn()
	{
		base.Respawn();
	}

	public override void _PhysicsProcess(double _)
	{
		if (!IsActive && !IsExploded)
			targetPosition = GetTargetPosition(false);

		lockonDecalRoot.GlobalPosition = targetPosition;
		base._PhysicsProcess(_);
	}

	public void StartWindup()
	{
		animator.Play("windup");
		Launch(); // Queue the bomb for launch
	}
}
