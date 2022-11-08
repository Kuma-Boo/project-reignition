using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Switch : Area3D
	{
		[Signal]
		public delegate void ActivatedEventHandler();

		[Export]
		private float activationLength;
		[Export]
		private bool invertState; //Used for when you want a switch that needs to be disabled
		[Export]
		private AnimationPlayer animator;
		private bool isActive;

		public override void _Ready()
		{
			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
		}

		public void Respawn()
		{
			isActive = false;
			animator.Play("RESET");
		}

		private void OnEntered(Area3D _) => Activate();
		public void Activate()
		{
			if (isActive) return;

			isActive = true;
			animator.Play("activate");
			EmitSignal(SignalName.Activated);
		}
	}
}
