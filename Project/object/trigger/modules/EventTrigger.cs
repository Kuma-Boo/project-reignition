using Godot;
using Godot.Collections;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Triggers environment effects/cutscenes.
/// For gameplay automated sections (such as loops), see <see cref="AutomationTrigger"/>.
/// </summary>
[Tool]
public partial class EventTrigger : StageTriggerModule
{
	[Signal] public delegate void ActivatedEventHandler();
	[Signal] public delegate void DeactivatedEventHandler();
	[Signal] public delegate void RespawnedEventHandler();
	[Signal] public delegate void EventFinishedEventHandler();
	[Signal] public delegate void EventSkippedEventHandler();

	/// <summary> Automatically reset the event when player respawns? </summary>
	private bool autoRespawn;
	/// <summary> Only allow event to play once? </summary>
	private bool isOneShot = true;
	private bool isActivated;
	private float animationBlending;

	[ExportGroup("Components")]
	[Export] private AnimationPlayer animator;
	public float AnimationLength => (float)animator.CurrentAnimationLength;
	private float AnimationTimeLeft => (float)(animator.CurrentAnimationLength - animator.CurrentAnimationPosition);

	[Export] private RespawnAnimation respawnAnimation;
	private enum RespawnAnimation
	{
		Reset,
		Activate,
		Deactivate,
	}
	[Export]
	private bool respawnToEnd = true;

	private readonly StringName ResetAnimation = "RESET";
	private readonly StringName EventAnimation = "event";
	private readonly StringName DeactivateEventAnimation = "event-deactivate";

	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty("Trigger Settings/Automatically Respawn", Variant.Type.Bool),
				ExtensionMethods.CreateProperty("Trigger Settings/Is One Shot", Variant.Type.Bool),
				ExtensionMethods.CreateProperty("Trigger Settings/Animation Blending", Variant.Type.Float),
				ExtensionMethods.CreateProperty("Trigger Settings/Player Stand-in", Variant.Type.NodePath)
			};

		if (playerStandin?.IsEmpty == false) // Add player event settings
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
			case "Trigger Settings/Animation Blending":
				return animationBlending;
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
			case "Trigger Settings/Animation Blending":
				animationBlending = (float)value;
				break;
			case "Trigger Settings/Player Stand-in":
				playerStandin = (NodePath)value;
				NotifyPropertyListChanged();
				break;

			case "Player Event Settings/Animation":
				characterAnimation = (string)value;
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
			StageSettings.Instance.Respawned += Respawn;

		StageSettings.Instance.Connect(StageSettings.SignalName.LevelStarted, new(this, MethodName.Respawn));

		PlayerStandin = GetNodeOrNull<Node3D>(playerStandin);
		if (PlayerStandin == null)
			return;

		animator.Play(EventAnimation);
		animator.Seek(animator.CurrentAnimationLength, true, true);
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
			return;

		if (playerStandin == null || Player.ExternalController != this)
			return;

		Player.CallDeferred(PlayerController.MethodName.UpdateExternalControl, true);
	}

	public override void Respawn()
	{
		isActivated = false;

		switch (respawnAnimation)
		{
			case RespawnAnimation.Reset:
				EmitSignal(SignalName.Respawned);
				if (animator.HasAnimation(ResetAnimation))
					animator.Play(ResetAnimation);
				break;
			case RespawnAnimation.Activate:
				EmitSignal(SignalName.Activated);
				if (animator.HasAnimation(EventAnimation))
					animator.Play(EventAnimation);
				break;
			case RespawnAnimation.Deactivate:
				EmitSignal(SignalName.Deactivated);
				if (animator.HasAnimation(DeactivateEventAnimation))
					animator.Play(DeactivateEventAnimation);
				break;
		}

		if (string.IsNullOrEmpty(animator.CurrentAnimation))
			return;

		animator.Seek(respawnToEnd ? animator.CurrentAnimationLength : 0, true, true);
		if (!respawnToEnd)
			animator.Stop(true);
	}

	public override void Activate()
	{
		if (isOneShot && isActivated) return;

		Visible = true;
		PlayAnimation(EventAnimation);
		EmitSignal(SignalName.Activated);
	}

	public override void Deactivate()
	{
		if (isOneShot && !isActivated) return;

		PlayAnimation(DeactivateEventAnimation);
		EmitSignal(SignalName.Deactivated);
	}

	private void PlayAnimation(StringName animation)
	{
		isActivated = true; // Update activation flag

		if (!animator.HasAnimation(animation))
		{
			GD.PrintErr($"{Name} is missing animation {animation}. Nothing will happen.");
			return;
		}

		bool blendAnimations = animator.CurrentAnimation != animation && animator.IsPlaying();
		if (!blendAnimations)
			animator.Seek(0, true); // Reset animation if necessary

		animator.Play(animation, blendAnimations ? animationBlending : 0.0f);
		animator.Advance(0);

		if (playerStandin?.IsEmpty != false) // Not a player event -- return early
			return;

		Player.StartEvent(this);
	}

	public void SkipEvent()
	{
		Player.Camera.StartCrossfade();
		StageSettings.Instance.AddTime(AnimationTimeLeft);

		animator.Advance(AnimationLength);
		EmitSignal(SignalName.EventSkipped);
	}

	#region Event Animation
	private NodePath playerStandin;
	public Node3D PlayerStandin { get; private set; }
	/// <summary> Lockout to apply when character finishes event. </summary>
	private LockoutResource characterExitLockout;
	public LockoutResource CharacterExitLockout => characterExitLockout;
	private float characterPositionSmoothing = .2f;
	public float CharacterPositionSmoothing => characterPositionSmoothing;

	/// <summary> How much to fadeout character's animation by. </summary>
	private float characterFadeoutTime;
	public float CharacterFadeoutTime => characterFadeoutTime;
	/// <summary> Which event animation to play on the Player. </summary>
	private StringName characterAnimation;
	public StringName CharacterAnimation => characterAnimation;

	/// <summary> Evaluate exit move speed as a ratio instead of a default value. </summary>
	private bool normalizeExitMoveSpeed = true;
	public bool NormalizeExitMoveSpeed => normalizeExitMoveSpeed;
	private float characterExitMoveSpeed;
	public float CharacterExitMoveSpeed => characterExitMoveSpeed;
	private float characterExitVerticalSpeed;
	public float CharacterExitVerticalSpeed => characterExitVerticalSpeed;

	private void ScreenShake(float magnitude)
	{
		Player.Camera.StartCameraShake(new()
		{
			magnitude = Vector3.One.RemoveDepth() * magnitude
		});
	}

	public void FinishEvent() => EmitSignal(SignalName.EventFinished);
	#endregion
}