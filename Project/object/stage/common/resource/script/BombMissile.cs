using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay.Objects;

namespace Project.Gameplay.Hazards;

/// <summary> Travels in an arc and lands on the target. Bested used on flat surfaces. </summary>
[Tool]
public partial class BombMissile : Node3D
{
	[Signal] public delegate void LaunchedEventHandler();
	[Signal] public delegate void ExplodedEventHandler();

	/// <summary> Enable this option to align the model's forward direction with its travel direction. </summary>
	[Export] private bool alignModelForward;
	[Export] private bool disableRespawning;
	[Export(PropertyHint.Range, "0.1,1,0.1,or_greater")] private float travelSpeedRatio = 1f;
	[Export] private AnimationPlayer animator;
	private SpawnData spawnData;

	public LaunchModes LaunchMode { get; private set; }
	public enum LaunchModes
	{
		Target, // Set launch distance via a target position (like jump triggers). Leave NULL to track the player.
		Manual, // Set launch via distance and height (like launchers)
		Code // Only allow launch settings to be initialized via code
	}

	public PlayerController Player => StageSettings.Player;
	public PlayerPathController PlayerPathfollower => StageSettings.Player?.PathFollower;

	private Launcher.LaunchDirection launchDirection;
	private const string LaunchModeKey = "Launch Settings/Mode";
	private const string LaunchDirectionKey = "Launch Settings/Direction";
	private const string DistanceKey = "Launch Settings/Distance";
	private const string MiddleHeightKey = "Launch Settings/Middle Height";
	private const string EndHeightKey = "Launch Settings/End Height";

	private const string TargetNode = "Launch Settings/Target";
	private const string SpreadKey = "Launch Settings/Spread";
	private const string SpeedTrackingKey = "Launch Settings/Speed Tracking";

	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = [
			ExtensionMethods.CreateProperty(LaunchModeKey, Variant.Type.Int, PropertyHint.Enum, LaunchMode.EnumToString())
		];

		if (LaunchMode == LaunchModes.Code) // No properties to edit if launch settings are determined via code
			return properties;

		if (LaunchMode == LaunchModes.Manual)
			properties.Add(ExtensionMethods.CreateProperty(LaunchDirectionKey, Variant.Type.Int, PropertyHint.Enum, launchDirection.EnumToString()));
		else
			properties.Add(ExtensionMethods.CreateProperty(TargetNode, Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Node3D"));

		properties.Add(ExtensionMethods.CreateProperty(MiddleHeightKey, Variant.Type.Float));

		if (LaunchMode == LaunchModes.Manual)
		{
			properties.Add(ExtensionMethods.CreateProperty(EndHeightKey, Variant.Type.Float));
			properties.Add(ExtensionMethods.CreateProperty(DistanceKey, Variant.Type.Float));
		}
		else
		{
			properties.Add(ExtensionMethods.CreateProperty(SpeedTrackingKey, Variant.Type.Float, PropertyHint.Range, "0,2,0.1,or_greater"));
			properties.Add(ExtensionMethods.CreateProperty(SpreadKey, Variant.Type.Float));
		}

		return properties;
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch (property)
		{
			case LaunchModeKey:
				LaunchMode = (LaunchModes)(int)value;
				NotifyPropertyListChanged();
				break;
			case LaunchDirectionKey:
				launchDirection = (Launcher.LaunchDirection)(int)value;
				break;

			case MiddleHeightKey:
				middleHeight = (float)value;
				break;
			case EndHeightKey:
				endHeight = (float)value;
				break;
			case DistanceKey:
				distance = (float)value;
				break;

			case TargetNode:
				_target = (NodePath)value;
				target = GetNodeOrNull<Node3D>(_target);
				break;
			case SpreadKey:
				spread = (float)value;
				break;
			case SpeedTrackingKey:
				speedTracking = (float)value;
				break;

			default:
				return false;
		}

		return true;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case LaunchModeKey:
				return (int)LaunchMode;
			case LaunchDirectionKey:
				return (int)launchDirection;
			case MiddleHeightKey:
				return middleHeight;
			case EndHeightKey:
				return endHeight;
			case DistanceKey:
				return distance;

			case TargetNode:
				return _target;
			case SpreadKey:
				return spread;
			case SpeedTrackingKey:
				return speedTracking;
		}

		return base._Get(property);
	}
	#endregion

	/// <summary> Height at the highest point of the arc. </summary>
	private float middleHeight = 5;
	/// <summary> Height at the end of the arc. </summary>
	private float endHeight;
	/// <summary> How far to travel. </summary>
	private float distance;
	/// <summary> How much to track the player's speed. </summary>
	private float speedTracking = 1f;
	/// <summary> How much horizontal spread to add to the target position. </summary>
	private float spread = 1.5f;

	private NodePath _target;
	private Node3D target;

	public bool IsActive { get; private set; } // Is the missile currently traveling?
	private float travelInterpolation;
	private Vector3 previousPosition;

	private LaunchSettings LaunchSettings { get; set; }
	public Vector3 GetLaunchDirection()
	{
		if (launchDirection == Launcher.LaunchDirection.Forward)
			return this.Forward();
		else if (launchDirection == Launcher.LaunchDirection.Flatten)
			return this.Up().RemoveVertical().Normalized();

		return this.Up();
	}

	/// <summary> Gets the vanilla LaunchSettings for Manual setup mode. </summary>
	public LaunchSettings GetLaunchSettings()
	{
		if (LaunchMode == LaunchModes.Code)
			return LaunchSettings.Create(GlobalPosition, GlobalPosition, 0);

		if (LaunchMode == LaunchModes.Target)
			return LaunchSettings.Create(GlobalPosition, GetTargetPosition(false), middleHeight, true);

		Vector3 endPosition = GlobalPosition + (GetLaunchDirection() * distance) + (Vector3.Up * endHeight);
		return LaunchSettings.Create(GlobalPosition, endPosition, middleHeight);
	}

	public Vector3 GetTargetPosition(bool enableSpread, float overrideTracking = -1f)
	{
		if (Engine.IsEditorHint() || Player == null)
			return (target != null) ? target.GlobalPosition : GlobalPosition;

		if (!Mathf.IsEqualApprox(overrideTracking, -1f))
			speedTracking = overrideTracking;

		Vector3 targetPosition;
		if (target != null)
		{
			targetPosition = target.GlobalPosition;
			if (enableSpread)
			{
				// Randomize spread by rotating a forward vector by a random amount, then multiplying in the spread factor
				targetPosition += Vector3.Forward.Rotated(Vector3.Up,
					Mathf.Tau * Runtime.randomNumberGenerator.RandfRange(0f, 1f))
					* Runtime.randomNumberGenerator.RandfRange(0, spread);
			}
			return targetPosition;
		}

		// Cache pathfollower data
		float progress = PlayerPathfollower.Progress;
		float hOffset = PlayerPathfollower.HOffset;

		// Try to predict where the player will be when the missile lands
		float dot = Player.GetMovementDirection().Dot(PlayerPathfollower.Forward());
		float offsetPrediction = Player.MoveSpeed * speedTracking * dot;
		PlayerPathfollower.Progress += offsetPrediction;
		PlayerPathfollower.HOffset = -PlayerPathfollower.LocalPlayerPositionDelta.X; // Works since the path is flat
		if (enableSpread) // Slightly randomize the middle missile's spread
			PlayerPathfollower.HOffset += Runtime.randomNumberGenerator.RandfRange(-spread, spread);

		targetPosition = PlayerPathfollower.GlobalPosition; // Cache calculated target position

		// Revert pathfollower
		PlayerPathfollower.Progress = progress;
		PlayerPathfollower.HOffset = hOffset;

		return targetPosition;
	}

	public void Respawn()
	{
		IsActive = false;
		animator.Play("RESET");
		spawnData.Respawn(this);
		previousPosition = GlobalPosition;
	}

	public override void _Ready()
	{
		target = GetNodeOrNull<Node3D>(_target);

		if (Engine.IsEditorHint() || disableRespawning)
			return;

		spawnData = new(GetParent(), Transform);
		StageSettings.Instance.Respawned += Respawn;
	}

	public override void _PhysicsProcess(double _)
	{
		if (!IsActive) return;

		UpdatePosition();
	}

	private void UpdatePosition()
	{
		previousPosition = GlobalPosition;
		GlobalPosition = LaunchSettings.InterpolatePositionTime(travelInterpolation);
		travelInterpolation += PhysicsManager.physicsDelta * travelSpeedRatio;

		// Reached the ground
		if (travelInterpolation >= LaunchSettings.TotalTravelTime)
			Explode();

		if (!alignModelForward || previousPosition.IsEqualApprox(GlobalPosition))
			return;

		LookAt(previousPosition);
	}

	/// <summary> Use this overload to fire at a specific point determined by code. </summary>
	public void Launch(LaunchSettings settings)
	{
		LaunchSettings = settings;
		Launch();
	}

	/// <summary> Launches the bomb with the current launch settings. </summary>
	public void Launch()
	{
		if (LaunchMode != LaunchModes.Code)
			LaunchSettings = GetLaunchSettings();

		IsActive = true;
		travelInterpolation = 0;
		animator.Play("fly");
		UpdatePosition();
		EmitSignal(SignalName.Launched);
	}

	public void Explode()
	{
		IsActive = false;
		animator.Play("explode"); // Impact effect
		EmitSignal(SignalName.Exploded);
	}
}