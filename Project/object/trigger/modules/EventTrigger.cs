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

		[Signal]
		public delegate void ActivatedEventHandler();
		private bool wasActivated;

		public override void _Ready()
		{
			StageSettings.instance.ConnectRespawnSignal(this);
		}

		public void Respawn()
		{
			wasActivated = false;
			if (animator.HasAnimation("RESET"))
				animator.Play("RESET"); //Reset event
			else if (!string.IsNullOrEmpty(animator.Autoplay)) //Fallback to autoplay animation
			{
				animator.Play(animator.Autoplay);
				animator.Stop();
				animator.Seek(0.0, true);
			}
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
		public void PlayCharacterAnimation(int animationIndex) => Character.Animator.PlayOneshotAnimation(animationIndex);
		/// <summary>
		/// Call this to reset character's movement state
		/// </summary>
		public void FinishEvent(float moveSpeed, float fallSpeed)
		{
			Character.MoveSpeed = moveSpeed;
			Character.VerticalSpd = fallSpeed;
			Character.ResetMovementState();

			if (lockout != null)
				Character.AddLockoutData(lockout);
		}
	}
}
