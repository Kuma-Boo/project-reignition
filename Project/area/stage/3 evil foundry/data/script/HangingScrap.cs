using Godot;
using System;

namespace Project.Gameplay
{
	public partial class HangingScrap : Node3D
	{

		[Export]
		private AnimationPlayer animator;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			LevelSettings.instance.ConnectRespawnSignal(this);
		}

		public void Respawn()
		{
			animator.Play("RESET");
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (Character.Lockon.IsHomingAttacking)
			{
				animator.Play("drop");
				Character.Lockon.StartBounce();
			}
		}
	}
}
