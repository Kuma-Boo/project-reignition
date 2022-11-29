using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Switch : Area3D
	{
		[Signal]
		public delegate void ActivatedEventHandler();
		[Signal]
		public delegate void DeactivatedEventHandler();
		[Signal]
		public delegate void RespawnedEventHandler();

		[Export]
		private float activationLength;
		[Export]
		private bool startActive; //Used for when you want a switch to start enabled
		[Export]
		private bool isToggleable; //Allow the switch to be toggled on/off?
		[Export]
		private AnimationPlayer animator;
		private bool isActive;
		private bool wasModified; //Was this switch already modified? Used when isToggleable is set to false.

		public override void _Ready()
		{
			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
		}

		public void Respawn()
		{
			wasModified = false;
			isActive = startActive;
			animator.Play(isActive ? "activate-loop" : "RESET");
			EmitSignal(SignalName.Respawned);
		}

		private void OnEntered(Area3D _) => Activate();
		public void Activate()
		{
			if (!isToggleable && wasModified) return;

			isActive = !isActive;
			animator.Play(isActive ? "activate" : "deactivate");
			EmitSignal(isActive ? SignalName.Activated : SignalName.Deactivated);

			wasModified = true;
		}
	}
}
