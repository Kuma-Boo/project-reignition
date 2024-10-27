using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Hanging scrap found in Evil Foundry.
	/// </summary>
	public partial class HangingScrap : Node3D
	{
		[Export(PropertyHint.NodePathValidTypes, "AnimationPlayer")] private NodePath animator;
		private AnimationPlayer Animator { get; set; }
		private PlayerController Player => StageSettings.Player;
		private bool isInteractingWithPlayer;

		public override void _Ready()
		{
			Animator = GetNode<AnimationPlayer>(animator);
			StageSettings.Instance.Respawned += Respawn;
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isInteractingWithPlayer) return;

			if (Player.IsOnGround)
			{
				if (Animator.CurrentAnimation != "delay_drop")
					Animator.Play("delay_drop");
				return;
			}

			Animator.Play("drop");

			if (Player.IsJumpDashOrHomingAttack)
				Player.StartBounce(false);
		}

		public void Respawn() => Animator.Play("RESET");

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
