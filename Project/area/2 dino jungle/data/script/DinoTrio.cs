using Godot;


namespace Project.Gameplay
{
	public partial class DinoTrio : PathFollow3D
	{
		[Export]
		private Path3D path;
		[Export]
		private AnimationTree animator;
		[Export]
		private bool log;
		private float moveSpeed;
		private float zOffset; // Used during attacks

		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			animator.Active = true;
		}


		public override void _PhysicsProcess(double _)
		{
			if (log)
				GD.Print(path.Curve.GetClosestOffset(path.GlobalPosition - Character.GlobalPosition));
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

				return;
			}
		}
	}
}
