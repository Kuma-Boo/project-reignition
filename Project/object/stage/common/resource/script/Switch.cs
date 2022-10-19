using Godot;

namespace Project.Gameplay.Objects
{
	public partial class Switch : Area3D
	{
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		[Export]
		public float activationLength;
		[Export]
		public bool invertState; //Used for when you want a switch that needs to be disabled

		[Signal]
		public delegate void ActivatedEventHandler();

		private bool isActive;

		public override void _Ready()
		{
			_animator = GetNode<AnimationPlayer>(animator);

			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
		}

		public void Respawn()
		{
			isActive = false;
			_animator.Play("RESET");
		}

		private void OnEntered(Area3D _) => Activate();
		public void Activate()
		{
			if (isActive) return;

			isActive = true;
			_animator.Play("activate");
			EmitSignal(SignalName.Activated);
		}
	}
}
