using Godot;

namespace Project.Gameplay.Hazards
{
	public class Hazard : Spatial
	{
		[Export]
		public bool isDisabled; //Is this hitbox active?

		private bool isInteractingWithPlayer;

		protected CharacterController Character => CharacterController.instance;

		[Signal]
		public delegate void DamagedPlayer();

		public override void _PhysicsProcess(float _) => ProcessCollision();

		protected void ProcessCollision()
		{
			if (!isDisabled && isInteractingWithPlayer)
			{
				Character.TakeDamage(this);
				EmitSignal(nameof(DamagedPlayer));
			}
		}

		public void OnEntered(Area _) => isInteractingWithPlayer = true;
		public void OnExited(Area _) => isInteractingWithPlayer = false;
	}
}
