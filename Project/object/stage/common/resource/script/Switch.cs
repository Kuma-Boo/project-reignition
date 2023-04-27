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

		[Export(PropertyHint.Range, "0,10,.1")]
		/// <summary> How long should activation last? </summary>
		private float activationLength;
		[Export]
		/// <summary> Used for when you want a switch to start enabled. </summary>
		private bool startActive;
		[Export]
		/// <summary> Allow the switch to be toggled on/off? </summary>
		private bool isToggleable;
		private bool isActive;
		/// <summary> Was this switch already modified? Used when isToggleable is set to false. </summary>
		private bool wasModified;

		[ExportGroup("Components")]
		[Export]
		private AnimationPlayer animator;

		public override void _Ready()
		{
			LevelSettings.instance.ConnectRespawnSignal(this);
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
