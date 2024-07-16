using Godot;

namespace Project.Gameplay.Objects
{
	public partial class MajinEgg : Node3D
	{
		[Signal]
		public delegate void ShatteredEventHandler();
		[Export]
		private AnimationPlayer animator;

		private bool isShattered;
		private bool permanentlyDestroyed;
		private bool interactingWithPlayer;
		private float currentHealth;
		private readonly int MaxHealth = 3;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			StageSettings.instance.ConnectRespawnSignal(this);
			StageSettings.instance.Connect(StageSettings.SignalName.TriggeredCheckpoint, new(this, MethodName.SaveDestructionStatus));

			Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (!interactingWithPlayer)
				return;

			UpdateInteraction();
		}

		private void UpdateInteraction()
		{
			if (isShattered)
				return;

			if (Character.Skills.IsSpeedBreakActive) // Instantly shatter
			{
				Shatter();
				return;
			}

			// TODO Rework to allow attack skills
			if (Character.Lockon.IsHomingAttacking) // Take Damage
			{
				currentHealth--;
				if (Character.Lockon.IsPerfectHomingAttack) // Double damage
					currentHealth--;

				if (currentHealth <= 0)
					Shatter();
				else if (currentHealth == 1)
					animator.Play("crack-02");
				else
					animator.Play("crack-01");

				Character.Lockon.CallDeferred(CharacterLockon.MethodName.StopHomingAttack);
				Character.Lockon.StartBounce(false);

				if (!isShattered)
					Character.Camera.SetDeferred("LockonTarget", this);
			}
		}

		private void SaveDestructionStatus() => permanentlyDestroyed = isShattered;

		private void Respawn()
		{
			if (permanentlyDestroyed)
				return;

			isShattered = false;
			currentHealth = MaxHealth;
		}

		private void Shatter()
		{
			isShattered = true;
			EmitSignal(SignalName.Shattered);
		}

		public void AreaEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			interactingWithPlayer = true;
			UpdateInteraction();
		}

		public void AreaExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			interactingWithPlayer = false;
		}
	}
}
