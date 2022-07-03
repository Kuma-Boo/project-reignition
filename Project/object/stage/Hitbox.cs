using Godot;
using System;

namespace Project.Gameplay
{
	public class Hitbox : Area
	{
		[Export]
		public bool isActive; //Is this hitbox active?

		private CharacterController Character => CharacterController.instance;

		public void OnEntered(Area _)
		{
			Character.TakeDamage();
		}
	}
}
