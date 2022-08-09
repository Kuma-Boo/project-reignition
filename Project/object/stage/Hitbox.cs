using Godot;

namespace Project.Gameplay
{
	public class Hitbox : Spatial
	{
		[Export]
		public bool isActive; //Is this hitbox active?

		protected CharacterController Character => CharacterController.instance;

		public void OnEntered(Area _) => Character.TakeDamage(this);
		public void OnExited(Area _) => Character.DequeueHitbox(this);
	}
}
