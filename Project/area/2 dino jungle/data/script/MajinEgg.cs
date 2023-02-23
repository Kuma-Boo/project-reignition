using Godot;

namespace Project.Gameplay.Objects
{
	public partial class MajinEgg : Node3D
	{
		[Signal]
		public delegate void ShatteredEventHandler();
		[Export]
		private AnimationPlayer animator;

		private float currentHealth;
		private readonly int MAX_HEALTH = 3;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			currentHealth = MAX_HEALTH;
		}

		private void Shatter()
		{
			GD.Print("Egg Shattered");
			EmitSignal(SignalName.Shattered);

			animator.Play("shatter");

			//TODO Play an animation that plays sfx, disables environment colliders, and disables hurtbox
		}

		public void PlayerEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			if (Character.Skills.IsSpeedBreakActive) //Instantly shatter
			{
				Shatter();
				return;
			}

			if (Character.Lockon.IsHomingAttacking)
			{
				GD.Print(Character.Lockon.IsPerfectHomingAttack);
				//Take Damage
				currentHealth--;
				if (Character.Lockon.IsPerfectHomingAttack) //Double damage
					currentHealth--;

				if (currentHealth <= 0)
					Shatter();
				else if (currentHealth == 1)
					animator.Play("crack-02");
				else
					animator.Play("crack-01");

				Character.Lockon.StartBounce();
			}
		}
	}
}
