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
	[Export] private float launchDelay;
	[Export] private bool screenShakeEnabled;
	[Export] protected Node3D root;
	[Export] protected AnimationPlayer animator;
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
	private const string LaunchOffsetKey = "Launch Settings/Starting Offset";
	private const string MiddleHeightKey = "Launch Settings/Middle Height";

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

		if (LaunchMode != LaunchModes.Manual)
			properties.Add(ExtensionMethods.CreateProperty(TargetNode, Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "Node3D"));

		if (LaunchMode == LaunchModes.Manual)
		{
			properties.Add(ExtensionMethods.CreateProperty(LaunchOffsetKey, Variant.Type.Vector3));
		}
		else
		{
			properties.Add(ExtensionMethods.CreateProperty(SpeedTrackingKey, Variant.Type.Float, PropertyHint.Range, "0,2,0.1,or_greater"));
			properties.Add(ExtensionMethods.CreateProperty(SpreadKey, Variant.Type.Float));
		}

		properties.Add(ExtensionMethods.CreateProperty(MiddleHeightKey, Variant.Type.Float));

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

			case MiddleHeightKey:
				middleHeight = (float)value;
				break;
			case LaunchOffsetKey:
				launchOffset = (Vector3)value;
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
			case MiddleHeightKey:
				return middleHeight;
			case LaunchOffsetKey:
				return launchOffset;

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
	/// <summary> Where to start the launch from. </summary>
	private Vector3 launchOffset;
	/// <summary> How much to track the player's speed. </summary>
	private float speedTracking = 1f;
	/// <summary> How much horizontal spread to add to the target position. </summary>
	private float spread = 1.5f;

	private NodePath _target;
	private Node3D target;

	public bool IsActive { get; private set; } // Is the missile currently traveling?
	public bool IsExploded { get; private set; }
	private float launchTimer;
	private Vector3 initialPosition;
	private Vector3 previousPosition;

	private Vector3 StartPosition => EndPosition + GlobalBasis * launchOffset;
	private Vector3 EndPosition => Engine.IsEditorHint() ? root.GlobalPosition : initialPosition;

	private LaunchSettings LaunchSettings { get; set; }
	public LaunchSettings GetLaunchSettings()
	{
		if (LaunchMode == LaunchModes.Code)
			return LaunchSettings.Create(root.GlobalPosition, GlobalPosition, 0);

		if (LaunchMode == LaunchModes.Target)
			return LaunchSettings.Create(root.GlobalPosition, GetTargetPosition(false), middleHeight, true);

		return LaunchSettings.Create(StartPosition, EndPosition, middleHeight);
	}

	public Vector3 GetTargetPosition(bool enableSpread, float overrideTracking = -1f)
	{
		if (Engine.IsEditorHint() || Player == null)
			return (target != null) ? target.GlobalPosition : root.GlobalPosition;

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

	public virtual void Respawn()
	{
		IsActive = false;
		IsExploded = false;
		animator.Play("RESET");
		if (animator.HasAnimation("init"))
		{
			animator.Advance(0.0);
			animator.Play("init");
		}

		TopLevel = false;
		spawnData.Respawn(this);
		previousPosition = root.GlobalPosition;
	}

	public override void _Ready()
	{
		target = GetNodeOrNull<Node3D>(_target);
		root ??= this;

		if (Engine.IsEditorHint() || disableRespawning)
			return;

		if (LaunchMode == LaunchModes.Manual)
		{
			initialPosition = root.GlobalPosition;
			root.GlobalPosition = StartPosition;
		}

		spawnData = new(GetParent(), Transform);
		StageSettings.Instance.Respawned += Respawn;
		Respawn();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
			return;

		if (!IsActive)
		{
			if (launchTimer < 0)
			{
				launchTimer = Mathf.MoveToward(launchTimer, 0, PhysicsManager.physicsDelta);
				if (Mathf.IsZeroApprox(launchTimer))
					Activate();
			}

			return;
		}

		UpdatePosition();
	}

	private void UpdatePosition()
	{
		previousPosition = root.GlobalPosition;
		root.GlobalPosition = LaunchSettings.InterpolatePositionTime(launchTimer);
		launchTimer += PhysicsManager.physicsDelta * travelSpeedRatio;

		// Reached the ground
		if (launchTimer >= LaunchSettings.TotalTravelTime)
		{
			root.GlobalPosition = LaunchSettings.endPosition;
			Explode();
		}

		if (!alignModelForward || previousPosition.IsEqualApprox(GlobalPosition))
			return;

		if (Mathf.Abs(Vector3.Up.Dot(root.GlobalPosition - previousPosition)) > .9f) // Co-linear vectors
			return;

		root.LookAt(previousPosition);
	}

	/// <summary> Use this overload to fire at a specific point determined by code. </summary>
	public void Launch(LaunchSettings settings)
	{
		LaunchSettings = settings;
		Activate();
	}

	/// <summary> Launches the bomb with the current launch settings. </summary>
	public void Launch()
	{
		if (Mathf.IsZeroApprox(launchDelay))
		{
			Activate();
			return;
		}

		launchTimer = -launchDelay;
	}

	public void Activate()
	{
		if (LaunchMode != LaunchModes.Code)
			LaunchSettings = GetLaunchSettings();

		IsActive = true;
		launchTimer = 0;
		animator.Play("fly");

		// Convert to global space
		Transform3D transform = GlobalTransform;
		TopLevel = true;
		GlobalTransform = transform;

		UpdatePosition();
		ResetPhysicsInterpolation();
		EmitSignal(SignalName.Launched);
	}

	public void Explode()
	{
		IsActive = false;
		IsExploded = true;
		animator.Play("explode"); // Impact effect

		if (screenShakeEnabled)
			Player.Camera.StartCameraShake(new PlayerCameraController.CameraShakeSettings()
			{
				origin = root.GlobalPosition,
				maximumDistance = 20,
			});

		EmitSignal(SignalName.Exploded);
	}
}