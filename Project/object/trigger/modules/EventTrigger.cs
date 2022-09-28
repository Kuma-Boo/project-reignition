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
		public NodePath animator;
		private AnimationPlayer _animator;
		[Export]
		public string eventName = "Event";
		[Export]
		public string resetName = "RESET";
		[Export]
		public NodePath playerStandin;
		[Export]
		public NodePath cameraStandin;

		[Signal]
		public delegate void ActivatedEventHandler();
		private bool wasActivated;

		public override void _Ready()
		{
			_animator = GetNode<AnimationPlayer>(animator);
			StageSettings.instance.RegisterRespawnableObject(this);
		}

		public void Respawn()
		{
			wasActivated = false;

			if (!string.IsNullOrEmpty(resetName))
				_animator.Play(resetName); //Reset event
		}

		public override void Activate()
		{
			if (wasActivated) return;

			if (!string.IsNullOrEmpty(eventName))
				_animator.Play(eventName);
			else
				GD.Print($"{Name} doesn't have an event animation. Nothing will happen");

			if (playerStandin != null)
				Character.StartExternal(GetNode<Node3D>(playerStandin), true);

			wasActivated = true;
			EmitSignal(SignalName.Activated);
		}

		//Call this from the animator to play a specific animation on the player
		public void PlayCharacterAnimation(string anim) => Character.Animator.PlayAnimation(anim);
		public void FinishEvent() => Character.CancelMovementState(CharacterController.MovementStates.External);
	}
}
