using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Hanging scrap found in Evil Foundry.
	/// </summary>
	public partial class HangingScrap : Node3D
	{
		[Export]
		private AnimationPlayer animator;
		private CharacterController Character => CharacterController.instance;
		private bool isInteractingWithPlayer;

		public override void _Ready() => StageSettings.instance.ConnectRespawnSignal(this);
		public void Respawn() => animator.Play("RESET");

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer) return;

			if (Character.IsOnGround)
			{
				if (animator.CurrentAnimation != "delay_drop")
					animator.Play("delay_drop");
				return;
			}

			animator.Play("drop");

			if (Character.ActionState == CharacterController.ActionStates.JumpDash)
				Character.Lockon.StartBounce(false);
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = true;
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = false;
		}
	}
}
