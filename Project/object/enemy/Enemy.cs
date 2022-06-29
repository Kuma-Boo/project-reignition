using Godot;

namespace Project.Gameplay
{
	public class Enemy : RespawnableObject
	{
		public override bool IsRespawnable() => true;

		[Signal]
		public delegate void OnDefeated();
	}
}
