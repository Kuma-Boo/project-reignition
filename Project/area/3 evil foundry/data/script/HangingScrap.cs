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
		private PlayerController Player => StageSettings.Player;
		private bool isInteractingWithPlayer;

		public override void _Ready() => StageSettings.instance.ConnectRespawnSignal(this);
		public void Respawn() => animator.Play("RESET");

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer) return;

			if (Player.IsOnGround)
			{
				if (animator.CurrentAnimation != "delay_drop")
					animator.Play("delay_drop");
				return;
			}

			animator.Play("drop");

			/*
			REFACTOR TODO
			if (Player.ActionState == PlayerController.ActionStates.JumpDash)
				Player.Lockon.StartBounce(false);
			*/
		}

		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;
			isInteractingWithPlayer = true;
		}

		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player detection")) return;
			isInteractingWithPlayer = false;
		}
	}
}
