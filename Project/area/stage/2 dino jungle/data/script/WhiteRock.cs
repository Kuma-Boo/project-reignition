using Godot;

namespace Project.Gameplay.Hazards
{
	public partial class WhiteRock : Area3D
	{
		[Export]
		private AnimationPlayer animator;

		public void Respawn()
		{
			animator.Play("RESET");
		}

		public void OnEntered(Area3D _)
		{
			animator.Play("shatter");
		}
	}
}
