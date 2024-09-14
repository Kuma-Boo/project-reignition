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
		private PlayerController Player => StageSettings.Player;
		/// <summary> True when colliding with the player. </summary>
		private bool IsInteracting { get; set; }
		/// <summary> True when a particular interaction has already been processed. </summary>
		private bool IsInteractionProcessed { get; set; }

		public override void _Ready()
		{
			StageSettings.Instance.ConnectRespawnSignal(this);
			StageSettings.Instance.Connect(StageSettings.SignalName.TriggeredCheckpoint, new(this, MethodName.SaveDestructionStatus));

			Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (IsInteracting)
				UpdateInteraction();
			else if (IsInteractionProcessed && Player.AttackState == PlayerController.AttackStates.None)
				ResetInteractionProcessed();
		}

		private void UpdateInteraction()
		{
			if (isShattered)
				return;

			if (IsInteractionProcessed)
				return;

			/*
			if (Player.Lockon.IsBounceLockoutActive &&
				Player.ActionState == PlayerController.ActionStates.Normal)
			{
				return;
			}
			*/

			switch (Player.AttackState)
			{
				case PlayerController.AttackStates.OneShot:
					Shatter();
					break;
				case PlayerController.AttackStates.Weak:
					currentHealth--;
					break;
				case PlayerController.AttackStates.Strong:
					currentHealth -= 2;
					break;
			}

			if (currentHealth <= 0)
				Shatter();
			else if (currentHealth == 1)
				animator.Play("crack-02");
			else if (currentHealth == 2)
				animator.Play("crack-01");

			if (Player.IsJumpDashOrHomingAttack)
			{
				// Copied from Enemy.cs UpdateLockon method
				/*
				REFACTOR TODO
				if (Player.Lockon.IsHomingAttacking)
					Player.Lockon.CallDeferred(CharacterLockon.MethodName.StopHomingAttack);
				*/

				if (!isShattered)
					Player.Camera.SetDeferred("LockonTarget", this);

				Player.StartBounce(isShattered);
			}

			SetInteractionProcessed();
		}

		private void SetInteractionProcessed()
		{
			IsInteractionProcessed = true;
			// Connect a signal
			if (!Player.IsConnected(PlayerController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed)))
				Player.Connect(PlayerController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed), (uint)ConnectFlags.OneShot + (uint)ConnectFlags.Deferred);
		}

		private void ResetInteractionProcessed()
		{
			IsInteractionProcessed = false;

			if (Player.IsConnected(PlayerController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed)))
				Player.Disconnect(PlayerController.SignalName.AttackStateChange, new(this, MethodName.ResetInteractionProcessed));
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
