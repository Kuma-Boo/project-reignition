using Godot;

namespace Project.Gameplay.Hazards
{
	public partial class Hazard : Node3D
	{
		[Export]
		public bool isDisabled; //Is this hitbox active?

		private bool isInteractingWithPlayer;

		protected CharacterController Character => CharacterController.instance;

		[Signal]
		public delegate void DamagedPlayerEventHandler();

		public override void _PhysicsProcess(double _) => ProcessCollision();

		protected void ProcessCollision()
		{
			if (!isDisabled && isInteractingWithPlayer)
			{
				Character.Knockback();
				EmitSignal(SignalName.DamagedPlayer);
			}
		}

		public void OnEntered(Area3D _) => isInteractingWithPlayer = true;
		public void OnExited(Area3D _) => isInteractingWithPlayer = false;
	}
}
