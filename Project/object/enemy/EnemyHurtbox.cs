using Godot;

namespace Project.Gameplay;

/// <summary>
/// Helper script attached directly to an enemy's hitbox.
/// Allows enemy detection using only triggers.
/// </summary>
public partial class EnemyHurtbox : Area3D
{
	[Export] public Enemy enemy;
}