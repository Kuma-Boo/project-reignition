using Godot;
using System;


namespace Project.Gameplay.Objects
{
	public partial class WoodenBox : Node3D
	{
		[Export]
		public NodePath pieces;
		private DestructableObject _pieces;

		public void OnBodyEntered(Node3D body)
		{
			if (body.IsInGroup("crusher"))
				_pieces.Shatter();
		}

		private void OnEntered(Area3D a)
		{
			if (a.IsInGroup("player"))
			{
				if (CharacterController.instance.IsAttacking)
				{
					GD.PrintErr("TODO: Determine shatter based on speed.");
					_pieces.Shatter();
				}
			}
			else
				_pieces.Shatter(); //Must be an enemy or something
		}
	}
}
