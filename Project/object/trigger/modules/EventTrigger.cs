using Godot;
using Godot.Collections;

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
		private readonly Array<DialogTrigger> dialogTriggers = new Array<DialogTrigger>();

		[Signal]
		public delegate void ActivatedEventHandler();
		private bool wasActivated;

		public override void _Ready()
		{
			StageSettings.instance.RegisterRespawnableObject(this);

			for (int i = 0; i < GetChildCount(); i++)
			{
				if (GetChild(i) is DialogTrigger)
					dialogTriggers.Add(GetChild<DialogTrigger>(i));
			}
		}

		public void Respawn()
		{
			wasActivated = false;
			if (animator.HasAnimation("RESET"))
				animator.Play("RESET"); //Reset event
		}

		public override void Activate()
		{
			if (wasActivated) return;

			if (animator.HasAnimation("event"))
				animator.Play("event");
			else
				GD.PrintErr($"{Name} doesn't have an event animation. Nothing will happen.");

			if (playerStandin != null)
				Character.StartExternal(playerStandin, .2f);

			wasActivated = true;
			EmitSignal(SignalName.Activated);
		}

		/// <summary>
		/// Call this to play a dialog track (DialogTriggers must be children to this EventTrigger node)
		/// </summary>
		public void PlayDialog(int index) => SoundManager.instance.PlayDialog(dialogTriggers[index]);

		/// <summary>
		/// Call this to play a specific animation on the player
		/// </summary>
		public void PlayCharacterAnimation(string anim) => Character.Animator.PlayAnimation(anim);
		/// <summary>
		/// Call this to reset character's movement state
		/// </summary>
		public void FinishEvent(float moveSpeed, float fallSpeed)
		{
			Character.MoveSpeed = moveSpeed;
			Character.VerticalSpd = fallSpeed;
			Character.ResetMovementState();
		}
	}
}
