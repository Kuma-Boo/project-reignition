using Godot;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Triggers a non-playable cutscene.
	/// For gameplay automated sections (such as loops), see <see cref="AutomationTrigger"/>.
	/// </summary>
	public partial class EventTrigger : StageTriggerModule
	{
		[Export]
		private AnimationPlayer animator;
		[Export]
		private Node3D playerStandin;
		[Export]
		private Node3D cameraStandin;
		[Export]
		private LockoutResource lockout;

		private bool wasActivated;
		[Signal]
		public delegate void ActivatedEventHandler();

		public override void _PhysicsProcess(double _)
		{
			if (playerStandin != null &&
			 Character.MovementState == CharacterController.MovementStates.External && Character.ExternalController == this)
				Character.UpdateExternalControl();
		}

		public override void Respawn()
		{
			if (animator.HasAnimation("RESET")) //Only reset if a RESET animation exists.
			{
				wasActivated = false;
				animator.Play("RESET"); //Reset event
			}
			else
				GD.PrintErr(Name + " doesn't have a RESET animation.");

			/*
			else if (!string.IsNullOrEmpty(animator.Autoplay)) //DEPRECATED. Fallback to autoplay animation
			{
				animator.Play(animator.Autoplay);
				animator.Stop();
				animator.Seek(0.0, true);
				wasActivated = false;
			}
			*/
		}

		public override void Activate()
		{
			if (wasActivated) return;

			if (animator.HasAnimation("event"))
				animator.Play("event");
			else
				GD.PrintErr($"{Name} doesn't have an event animation. Nothing will happen.");

			if (playerStandin != null)
				Character.StartExternal(this, playerStandin, .2f);

			wasActivated = true;
			EmitSignal(SignalName.Activated);
		}

		/// <summary>
		/// Call this to play a specific animation on the player
		/// </summary>
		public void PlayCharacterAnimation(StringName animationName) => Character.Animator.PlayOneshotAnimation(animationName);
		/// <summary>
		/// Call this to reset character's movement state
		/// </summary>
		public void FinishEvent(float moveSpeed, float fallSpeed)
		{
			Character.MoveSpeed = moveSpeed;
			Character.VerticalSpd = fallSpeed;

			Character.ResetMovementState();

			Character.MovementAngle = Character.CalculateForwardAngle(playerStandin.Forward());
			Character.Animator.SnapRotation(Character.MovementAngle);

			if (lockout != null)
				Character.AddLockoutData(lockout);
		}
	}
}
