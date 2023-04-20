using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Triggers environment effects/cutscenes.
	/// For gameplay automated sections (such as loops), see <see cref="AutomationTrigger"/>.
	/// </summary>
	public partial class EventTrigger : StageTriggerModule
	{
		[Signal]
		public delegate void ActivatedEventHandler();

		[Export]
		private bool autoRespawn;
		[Export]
		/// <summary> Only allow event to play once? </summary>
		private bool isOneShot = true;
		private bool wasActivated;

		[ExportGroup("Components")]
		[Export]
		private AnimationPlayer animator;
		[Export]
		private Node3D playerStandin;
		[Export]
		private Node3D cameraStandin;
		[Export]
		private LockoutResource lockout;

		private readonly StringName EVENT_ANIMATION = "event";
		private readonly StringName RESET_ANIMATION = "RESET";


		public override void _Ready()
		{
			if (autoRespawn)
				LevelSettings.instance.ConnectRespawnSignal(this);
		}


		public override void _PhysicsProcess(double _)
		{
			if (playerStandin == null)
				return;

			if (Character.MovementState != CharacterController.MovementStates.External || Character.ExternalController != this)
				return;

			Character.UpdateExternalControl();
		}


		public override void Respawn()
		{
			// Only reset if a RESET animation exists.
			if (!animator.HasAnimation(RESET_ANIMATION)) return;

			wasActivated = false;
			animator.Play(RESET_ANIMATION);
		}


		public override void Activate()
		{
			if (!animator.HasAnimation(EVENT_ANIMATION))
			{
				GD.PrintErr($"{Name} doesn't have an event animation. Nothing will happen.");
				return;
			}

			if (isOneShot && wasActivated) return;
			wasActivated = true; // Update activation flag


			if (!animator.IsPlaying() && animator.CurrentAnimation == EVENT_ANIMATION) // Reset animation if necessary
				animator.Seek(0, true);

			animator.Play(EVENT_ANIMATION);


			if (playerStandin != null)
				Character.StartExternal(this, playerStandin, .2f);

			if (cameraStandin != null)
				Character.Camera.SetExternalController(cameraStandin);


			EmitSignal(SignalName.Activated);
		}



		// Helper functions to be called from the animator

		/// <summary> Plays a specific oneshot animation on the player. </summary>
		public void PlayCharacterAnimation(StringName animationName) => Character.Animator.PlayOneshotAnimation(animationName);


		/// <summary> Sets the character's speeds. </summary>
		public void SetCharacterSpeed(float moveSpeed, float verticalSpeed)
		{
			Character.MoveSpeed = moveSpeed;
			Character.VerticalSpeed = verticalSpeed;
		}


		/// <summary> Resets the character's movement state. </summary>
		public void FinishEvent()
		{
			Character.ResetMovementState();

			Character.MovementAngle = Character.CalculateForwardAngle(playerStandin.Forward());
			Character.Animator.SnapRotation(Character.MovementAngle);

			if (lockout != null)
				Character.AddLockoutData(lockout);
		}
	}
}
