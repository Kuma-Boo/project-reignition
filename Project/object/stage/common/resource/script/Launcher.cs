using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary>
/// Launches the player. Use <see cref="CreateLaunchSettings(Vector3, Vector3, float, bool)"/> to bypass needing a Launcher node.
/// </summary>
[Tool]
public partial class Launcher : Node3D // Jumps between static points w/ custom sfx support
{
	#region Editor
	private const string SecondarySettingsEnabled = "Secondary Settings/Enabled";
	private const string SecondarySettingsMiddleHeight = "Secondary Settings/Middle Height";
	private const string SecondarySettingsFinalHeight = "Secondary Settings/Final Height";
	private const string SecondarySettingsDistance = "Secondary Settings/Distance";
	private const string SecondarySettingsBlend = "Secondary Settings/Blend";
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties =
			[
				ExtensionMethods.CreateProperty(SecondarySettingsEnabled, Variant.Type.Bool)
			];

		if (!EnableSecondarySettings)
			return properties;

		properties.Add(ExtensionMethods.CreateProperty(SecondarySettingsMiddleHeight, Variant.Type.Float));
		properties.Add(ExtensionMethods.CreateProperty(SecondarySettingsFinalHeight, Variant.Type.Float));
		properties.Add(ExtensionMethods.CreateProperty(SecondarySettingsDistance, Variant.Type.Float));
		properties.Add(ExtensionMethods.CreateProperty(SecondarySettingsBlend, Variant.Type.Float, PropertyHint.Range, "0, 1, .1"));

		return properties;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case SecondarySettingsEnabled:
				return EnableSecondarySettings;
			case SecondarySettingsMiddleHeight:
				return SecondaryMiddleHeight;
			case SecondarySettingsFinalHeight:
				return SecondaryFinalHeight;
			case SecondarySettingsDistance:
				return SecondaryDistance;
			case SecondarySettingsBlend:
				return LaunchRatio;
		}

		return base._Get(property);
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case SecondarySettingsEnabled:
				EnableSecondarySettings = (bool)value;
				NotifyPropertyListChanged();
				break;
			case SecondarySettingsMiddleHeight:
				SecondaryMiddleHeight = (float)value;
				break;
			case SecondarySettingsFinalHeight:
				SecondaryFinalHeight = (float)value;
				break;
			case SecondarySettingsDistance:
				SecondaryDistance = (float)value;
				break;
			case SecondarySettingsBlend:
				LaunchRatio = (float)value;
				break;
			default:
				return false;
		}

		return true;
	}
	#endregion

	[Signal]
	public delegate void ActivatedEventHandler();
	[Signal]
	public delegate void DeactivatedEventHandler(); // Called after character finishes processing this launcher.

	/// <summary> Height at the beginning of the arc. </summary>
	[ExportSubgroup("Launch Settings")]
	[Export] private float StartingHeight { get; set; }
	/// <summary> Height at the highest point of the arc. </summary>
	[Export] private float MiddleHeight { get; set; }
	/// <summary> Height at the end of the arc. </summary>
	[Export] private float FinalHeight { get; set; }
	/// <summary> How far to travel. </summary>
	[Export] private float Distance { get; set; }

	[Export] public bool oneshotEnemies = true;

	/// <summary> Should the player be allowed to perform a Jump Dash after the launch is completed? </summary>
	[Export] public bool AllowJumpDashing { get; set; }

	[Export]
	public LauncherAnimations LaunchAnimationType { get; private set; }
	public enum LauncherAnimations
	{
		Auto,
		Jump,
		Launch,
	}

	[Export]
	public LaunchDirection LauncherDirection { get; private set; }
	public enum LaunchDirection
	{
		Forward,
		Up,
		Flatten,
	}

	/// <summary> Allows the launcher to blend between secondary launch settings. </summary>
	protected bool EnableSecondarySettings { get; private set; }
	/// <summary> Controls the blend between launch settings. Mostly used for editor previewing. </summary>
	protected float LaunchRatio { get; set; }
	protected float SecondaryMiddleHeight { get; private set; }
	protected float SecondaryFinalHeight { get; private set; }
	protected float SecondaryDistance { get; private set; }

	/// <summary> Returns the blend value between the launch settings. Can be overridden by scripts that inherit Launcher.cs. </summary>
	public virtual float GetLaunchRatio()
	{
		if (EnableSecondarySettings)
			return LaunchRatio;

		return 0; // Use the primary launch settings
	}

	public virtual Vector3 GetLaunchDirection()
	{
		Vector3 forward = ignoreLaunchPointRotation ? this.Forward() : LaunchPoint.Forward();
		Vector3 up = ignoreLaunchPointRotation ? this.Up() : LaunchPoint.Up();

		if (LauncherDirection == LaunchDirection.Forward)
			return forward;

		if (LauncherDirection == LaunchDirection.Flatten)
		{
			if (Mathf.Abs(forward.Dot(Vector3.Up)) > .9f)
			{
				int sign = forward.Y > -0.01f ? 1 : -1;
				sign *= up.Y > -0.01f ? 1 : -1;
				return -(up * sign).RemoveVertical().Normalized();
			}

			return forward.RemoveVertical().Normalized();
		}

		return up;
	}

	/// <summary> Overload method so launch rings can recenter the player visually. </summary>
	protected virtual Vector3 CalculateStartingPoint() => StartingPoint;
	public Vector3 StartingPoint => LaunchPoint.GlobalPosition + (Vector3.Up * StartingHeight);

	public virtual LaunchSettings GetLaunchSettings()
	{
		float blendedDistance = Mathf.Lerp(Distance, SecondaryDistance, GetLaunchRatio());
		float blendedMiddleHeight = Mathf.Lerp(MiddleHeight, SecondaryMiddleHeight, GetLaunchRatio());
		float blendedFinalHeight = Mathf.Lerp(FinalHeight, SecondaryFinalHeight, GetLaunchRatio());

		Vector3 startPosition = CalculateStartingPoint();
		Vector3 endPosition = StartingPoint + (GetLaunchDirection() * blendedDistance) + (Vector3.Up * blendedFinalHeight);

		LaunchSettings settings = LaunchSettings.Create(startPosition, endPosition, blendedMiddleHeight);
		settings.AllowJumpDash = AllowJumpDashing;
		settings.UseAutoAlign = true;
		settings.Launcher = this;
		settings.OneshotEnemies = oneshotEnemies;

		return settings;
	}

	public override void _Ready() => SetUp();
	protected virtual void SetUp() => _sfxPlayer = GetNodeOrNull<AudioStreamPlayer3D>(sfxPlayer);

	public virtual void Activate(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		IsPlayerCentered = false;
		Activate();
	}
	public virtual void Activate()
	{
		EmitSignal(SignalName.Activated);
		PlayLaunchSfx();

		if (voiceKey?.IsEmpty == false)
			Player.Effect.PlayVoice(voiceKey);

		IsPlayerCentered = recenterSpeed == 0;
		Player.StartLauncher(GetLaunchSettings());

		LaunchAnimation();
	}

	/// <summary> Sets the player's launch animation based on launchsettings. Override as needed. </summary>
	protected virtual void LaunchAnimation()
	{
		Player.Effect.StopSpinFX();
		Player.Effect.StopTrailFX();
		Player.Animator.ResetState(.1f);

		if (LaunchAnimationType == LauncherAnimations.Auto)
		{
			if (GetLaunchSettings().InitialVelocity.AngleTo(Vector3.Up) < Mathf.Pi * .1f)
				Player.Animator.JumpAnimation();
			else
				Player.Animator.LaunchAnimation();
		}
		else if (LaunchAnimationType == LauncherAnimations.Jump)
		{
			Player.Animator.JumpAnimation();
		}
		else
		{
			Player.Animator.LaunchAnimation();
		}
	}

	[Export]
	private int recenterSpeed = 32; // How fast to recenter the character

	public virtual bool IsPlayerCentered { get; protected set; }
	protected PlayerController Player => StageSettings.Player;

	public Vector3 RecenterPlayer()
	{
		Vector3 targetPosition = CalculateStartingPoint();
		Vector3 pos = Player.GlobalPosition.MoveToward(targetPosition, recenterSpeed * PhysicsManager.physicsDelta);
		IsPlayerCentered = pos.IsEqualApprox(targetPosition);
		return pos;
	}

	public void Deactivate() => EmitSignal(SignalName.Deactivated);

	[Export]
	/// <summary> Optional SFX player. </summary>
	private NodePath sfxPlayer;
	private AudioStreamPlayer3D _sfxPlayer;
	protected bool IsSfxActive => _sfxPlayer.Playing;
	protected virtual void PlayLaunchSfx() => _sfxPlayer?.Play();
	[Export]
	/// <summary> Option voice to play. </summary>
	private StringName voiceKey;
	/// <summary> Optional launch point override node. </summary>
	[Export]
	protected Node3D launchPoint;
	public Node3D LaunchPoint => launchPoint ?? this;
	[Export]
	protected bool ignoreLaunchPointRotation;
}