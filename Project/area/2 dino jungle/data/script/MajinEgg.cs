using Godot;

namespace Project.Gameplay.Objects
{
	public partial class MajinEgg : Node3D
	{
		[Signal]
		public delegate void ShatteredEventHandler();
		[Signal]
		public delegate void RespawnedEventHandler();
		[Export]
		private AnimationPlayer animator;

		private bool isShattered;
		private bool permanentlyDestroyed;
		private float currentHealth;
		private readonly int MaxHealth = 3;
		private CharacterController Character => CharacterController.instance;
		/// <summary> True when colliding with the player. </summary>
		private bool IsInteracting { get; set; }
		/// <summary> True when a particular interaction has already been processed. </summary>
		private bool IsInteractionProcessed { get; set; }

		public override void _Ready()
		{
			StageSettings.instance.ConnectRespawnSignal(this);
			StageSettings.instance.Connect(StageSettings.SignalName.TriggeredCheckpoint, new(this, MethodName.SaveDestructionStatus));

			Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (IsInteracting)
				UpdateInteraction();
			else if (IsInteractionProcessed && Character.AttackState == CharacterController.AttackStates.None)
				ResetInteractionProcessed();
		}

		private void UpdateInteraction()
		{
			if (isShattered)
				return;

			if (IsInteractionProcessed)
				return;

			if (Character.Lockon.IsBounceLockoutActive &&
				Character.ActionState == CharacterController.ActionStates.Normal)
			{
				return;
			}

			switch (Character.AttackState)
			{
				case CharacterController.AttackStates.OneShot:
					Shatter();
					break;
				case CharacterController.AttackStates.Weak:
					currentHealth--;
					break;
				case CharacterController.AttackStates.Strong:
					currentHealth -= 2;
					break;
			}

			if (currentHealth <= 0)
				Shatter();
			else if (currentHealth == 1)
				animator.Play("crack-02");
			else if (currentHealth == 2)
				animator.Play("crack-01");

			if (Character.ActionState == CharacterController.ActionStates.JumpDash)
			{
				// Copied from Enemy.cs UpdateLockon method
				if (Character.Lockon.IsHomingAttacking)
					Character.Lockon.CallDeferred(CharacterLockon.MethodName.StopHomingAttack);

				if (!isShattered)
					Character.Camera.SetDeferred("LockonTarget", this);

				Character.Lockon.StartBounce(isShattered);
			}

			SetInteractionProcessed();
		}

		private void SetInteractionProcessed()
		{
			IsInteractionProcessed = true;
			// Connect a signal
			if (!Character.IsConnected(CharacterController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed)))
				Character.Connect(CharacterController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed), (uint)ConnectFlags.OneShot + (uint)ConnectFlags.Deferred);
		}

		private void ResetInteractionProcessed()
		{
			IsInteractionProcessed = false;

			if (Character.IsConnected(CharacterController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed)))
				Character.Disconnect(CharacterController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed));
		}

		private void SaveDestructionStatus() => permanentlyDestroyed = isShattered;

		private void Respawn()
		{
			if (permanentlyDestroyed)
				return;

			isShattered = false;
			currentHealth = MaxHealth;
			EmitSignal(SignalName.Respawned);
		}

		private void Shatter()
		{
			isShattered = true;
			EmitSignal(SignalName.Shattered);
		}

		public void AreaEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			IsInteracting = true;
			UpdateInteraction();
		}

		public void AreaExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			IsInteracting = false;
		}
	}
}
