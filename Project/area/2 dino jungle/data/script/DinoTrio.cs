using Godot;

namespace Project.Gameplay
{
	public partial class DinoTrio : PathFollow3D
	{
		[Export]
		private AnimationTree animator;
		private float moveSpeed;

		[Signal]
		public delegate void SignalNsameEventHandler();


		public override void _Ready()
		{
			animator.Active = true;
		}


		public override void _PhysicsProcess(double _)
		{
		}

		public void MyFunction()
		{

		}


		public void OnAreaEntered(Area3D area)
		{
			if (area.IsInGroup("player"))
			{
				CharacterController.instance.StartKnockback(new CharacterController.KnockbackSettings()
				{
					knockForward = true, // Always knock forward
					ignoreInvincibility = true, // Always knockback the player
					disableDamage = CharacterController.instance.IsInvincible, // Don't hurt player during invincibility
				});
			}
		}
	}
}
