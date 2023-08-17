using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Triggers environment effects/cutscenes.
	/// For gameplay automated sections (such as loops), see <see cref="AutomationTrigger"/>.
	/// </summary>
	[Tool]
	public partial class EventTrigger : StageTriggerModule
	{
		[Signal]
		public delegate void ActivatedEventHandler();

		/// <summary> Automatically reset the event when player respawns? </summary>
		private bool autoRespawn;
		/// <summary> Only allow event to play once? </summary>
		private bool isOneShot = true;
		private bool wasActivated;

		[ExportGroup("Components")]
		[Export]
		private AnimationPlayer animator;

		private readonly StringName EVENT_ANIMATION = "event";
		private readonly StringName RESET_ANIMATION = "RESET";

		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Trigger Settings/Automatically Respawn", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Trigger Settings/Is One Shot", Variant.Type.Bool));

			properties.Add(ExtensionMethods.CreateProperty("Trigger Settings/Player Stand-in", Variant.Type.NodePath));


			if (playerStandin != null && !playerStandin.IsEmpty) // Add player event settings
			{
				properties.Add(ExtensionMethods.CreateProperty("Player Event Settings/Animation", Variant.Type.StringName));
				properties.Add(ExtensionMethods.CreateProperty("Player Event Settings/Animation Fadeout Time", Variant.Type.Float));
				properties.Add(ExtensionMethods.CreateProperty("Player Event Settings/Position Smoothing", Variant.Type.Float, PropertyHint.Range, "0,1,.1"));

				properties.Add(ExtensionMethods.CreateProperty("Player Event Settings/Normalize Exit Move Speed", Variant.Type.Bool));
				properties.Add(ExtensionMethods.CreateProperty("Player Event Settings/Exit Move Speed", Variant.Type.Float));
				properties.Add(ExtensionMethods.CreateProperty("Player Event Settings/Exit Vertical Speed", Variant.Type.Float));
				properties.Add(ExtensionMethods.CreateProperty("Player Event Settings/Exit Lockout", Variant.Type.Object));
			}

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Trigger Settings/Automatically Respawn":
					return autoRespawn;
				case "Trigger Settings/Is One Shot":
					return isOneShot;
				case "Trigger Settings/Player Stand-in":
					return playerStandin;

				case "Player Event Settings/Animation":
					return characterAnimation;
				case "Player Event Settings/Animation Fadeout Time":
					return characterFadeoutTime;
				case "Player Event Settings/Position Smoothing":
					return characterPositionSmoothing;

				case "Player Event Settings/Normalize Exit Move Speed":
					return normalizeExitMoveSpeed;
				case "Player Event Settings/Exit Move Speed":
					return characterExitMoveSpeed;
				case "Player Event Settings/Exit Vertical Speed":
					return characterExitVerticalSpeed;
				case "Player Event Settings/Exit Lockout":
					return characterExitLockout;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Trigger Settings/Automatically Respawn":
					autoRespawn = (bool)value;
					break;
				case "Trigger Settings/Is One Shot":
					isOneShot = (bool)value;
					break;
				case "Trigger Settings/Player Stand-in":
					playerStandin = (NodePath)value;
					NotifyPropertyListChanged();
					break;

				case "Player Event Settings/Animation":
					characterAnimation = new StringName((string)value);
					break;
				case "Player Event Settings/Animation Fadeout Time":
					characterFadeoutTime = (float)value;
					break;
				case "Player Event Settings/Position Smoothing":
					characterPositionSmoothing = (float)value;
					break;

				case "Player Event Settings/Normalize Exit Move Speed":
					normalizeExitMoveSpeed = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Player Event Settings/Exit Move Speed":
					characterExitMoveSpeed = (float)value;
					break;
				case "Player Event Settings/Exit Vertical Speed":
					characterExitVerticalSpeed = (float)value;
					break;
				case "Player Event Settings/Exit Lockout":
					characterExitLockout = (LockoutResource)value;
					break;

				default:
					return false;
			}

			return true;
		}
		#endregion


		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			if (autoRespawn)
				StageSettings.instance.ConnectRespawnSignal(this);
		}


		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint()) return;

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

			EmitSignal(SignalName.Activated);
			wasActivated = true; // Update activation flag

			if (!animator.IsPlaying() && animator.CurrentAnimation == EVENT_ANIMATION) // Reset animation if necessary
				animator.Seek(0, true);

			animator.Play(EVENT_ANIMATION);


			if (playerStandin != null && !playerStandin.IsEmpty)
			{
				BGMPlayer.SetStageMusicVolume(-80f); // Mute BGM

				Character.StartExternal(this, GetNode<Node3D>(playerStandin), characterPositionSmoothing);
				Character.Animator.ExternalAngle = 0; // Reset external angle
				Character.Animator.SnapRotation(Character.Animator.ExternalAngle);
				Character.Animator.PlayOneshotAnimation(characterAnimation);

				// Disable break skills
				Character.Skills.IsSpeedBreakEnabled = Character.Skills.IsTimeBreakEnabled = false;
			}
		}


		#region Event Animation
		private NodePath playerStandin;
		/// <summary> Lockout to apply when character finishes event. </summary>
		private LockoutResource characterExitLockout;
		private float characterPositionSmoothing = .2f;

		/// <summary> How much to fadeout character's animation by. </summary>
		private float characterFadeoutTime;
		/// <summary> Which event animation to play on the character. </summary>
		private StringName characterAnimation;

		/// <summary> Evaluate exit move speed as a ratio instead of a default value. </summary>
		private bool normalizeExitMoveSpeed = true;
		private float characterExitMoveSpeed;
		private float characterExitVerticalSpeed;


		/// <summary> Resets the character's movement state. </summary>
		public void FinishEvent()
		{
			BGMPlayer.SetStageMusicVolume(0f); // Unmute BGM
			Character.ResetMovementState();

			Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(Character.ExternalParent.Forward());
			Character.Animator.SnapRotation(Character.MovementAngle);
			Character.Animator.CancelOneshot(characterFadeoutTime);

			if (characterExitLockout != null)
				Character.AddLockoutData(characterExitLockout);

			Character.MoveSpeed = normalizeExitMoveSpeed ? Character.GroundSettings.speed * characterExitMoveSpeed : characterExitMoveSpeed;
			Character.VerticalSpeed = characterExitVerticalSpeed;

			// Re-enable break skills
			Character.Skills.IsSpeedBreakEnabled = Character.Skills.IsTimeBreakEnabled = true;
		}
		#endregion
	}
}
