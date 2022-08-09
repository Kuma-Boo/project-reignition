using Godot;

namespace Project.Gameplay
{
	public class Button : RespawnableObject
	{
		[Export]
		public NodePath buttonAnimator;
		private AnimationPlayer _buttonAnimator;

		[Signal]
		public delegate void Activated();

		private bool isActive;

		protected override void SetUp()
		{
			_buttonAnimator = GetNode<AnimationPlayer>(buttonAnimator);

			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
		}

		public override void Respawn()
		{
			isActive = false;
			_buttonAnimator.Play("RESET");
		}

		private void OnEntered(Area _) => Activate();
		public void Activate()
		{
			if (isActive) return;

			isActive = true;
			_buttonAnimator.Play("activate");
			EmitSignal(nameof(Activated));
		}
	}
}
