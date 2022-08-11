using Godot;

namespace Project.Gameplay.Hazards
{
	public class Hazard : Spatial
	{
		[Export]
		public bool isDisabled; //Is this hitbox active?
		private bool isInteractingWithPlayer;

		protected CharacterController Character => CharacterController.instance;

		public override void _PhysicsProcess(float _)
		{
			if (!isDisabled && isInteractingWithPlayer)
				Character.TakeDamage();
		}

		public void OnEntered(Area _) => isInteractingWithPlayer = true;
		public void OnExited(Area _) => isInteractingWithPlayer = false;
	}
}
