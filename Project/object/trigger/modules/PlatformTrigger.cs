using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Moves player with moving platforms.
/// </summary>
[Tool]
public partial class PlatformTrigger : Node3D
{
	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = [ExtensionMethods.CreateProperty("Falling Platform Settings/Enabled", Variant.Type.Bool)];

		if (isFallingBehaviourEnabled)
		{
			properties.Add(ExtensionMethods.CreateProperty("Falling Platform Settings/Animator", Variant.Type.NodePath, PropertyHint.NodePathValidTypes, "AnimationPlayer"));
			properties.Add(ExtensionMethods.CreateProperty("Falling Platform Settings/Auto Shake", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Falling Platform Settings/Shake Length", Variant.Type.Float, PropertyHint.Range, "0, 10"));
			properties.Add(ExtensionMethods.CreateProperty("Falling Platform Settings/Fall Speed", Variant.Type.Float, PropertyHint.Range, "0.1,1,0.1,or_greater"));
		}

		properties.Add(ExtensionMethods.CreateProperty("Floating Platform Settings/Enabled", Variant.Type.Bool));
		if (isFloatingBehaviorEnabled)
		{
			properties.Add(ExtensionMethods.CreateProperty("Floating Platform Settings/Height Range", Variant.Type.Float, PropertyHint.Range, "0,5,0.1"));
			properties.Add(ExtensionMethods.CreateProperty("Floating Platform Settings/Force Multiplier", Variant.Type.Float, PropertyHint.Range, "0,2,0.1"));
			properties.Add(ExtensionMethods.CreateProperty("Floating Platform Settings/Initial Force", Variant.Type.Float));
			properties.Add(ExtensionMethods.CreateProperty("Floating Platform Settings/Initial Height", Variant.Type.Float, PropertyHint.Range, "-1,1,0.1"));
		}

		return properties;
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Falling Platform Settings/Enabled":
				isFallingBehaviourEnabled = (bool)value;
				NotifyPropertyListChanged();
				break;
			case "Falling Platform Settings/Animator":
				fallingPlatformAnimatorPath = (NodePath)value;
				break;
			case "Falling Platform Settings/Auto Shake":
				autoShake = (bool)value;
				break;
			case "Falling Platform Settings/Shake Length":
				shakeLength = (float)value;
				break;
			case "Falling Platform Settings/Fall Speed":
				fallSpeedScale = (float)value;
				break;

			case "Floating Platform Settings/Enabled":
				isFloatingBehaviorEnabled = (bool)value;
				NotifyPropertyListChanged();
				break;
			case "Floating Platform Settings/Height Range":
				floatingHeightRange = (float)value;
				break;
			case "Floating Platform Settings/Force Multiplier":
				floatingForceMultiplier = (float)value;
				break;
			case "Floating Platform Settings/Return Strength":
				buoyancyStrength = (float)value;
				break;
			case "Floating Platform Settings/Initial Force":
				currentBuoyancyForce = (float)value;
				break;
			case "Floating Platform Settings/Initial Height":
				floatingInitialHeightRatio = (float)value;
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
			case "Falling Platform Settings/Enabled":
				return isFallingBehaviourEnabled;
			case "Falling Platform Settings/Animator":
				return fallingPlatformAnimatorPath;
			case "Falling Platform Settings/Auto Shake":
				return autoShake;
			case "Falling Platform Settings/Shake Length":
				return shakeLength;
			case "Falling Platform Settings/Fall Speed":
				return fallSpeedScale;

			case "Floating Platform Settings/Enabled":
				return isFloatingBehaviorEnabled;
			case "Floating Platform Settings/Height Range":
				return floatingHeightRange;
			case "Floating Platform Settings/Force Multiplier":
				return floatingForceMultiplier;
			case "Floating Platform Settings/Return Strength":
				return buoyancyStrength;
			case "Floating Platform Settings/Initial Force":
				return currentBuoyancyForce;
			case "Floating Platform Settings/Initial Height":
				return floatingInitialHeightRatio;
		}

		return base._Get(property);
	}
	#endregion

	[Signal]
	public delegate void PlatformInteractedEventHandler();

	// Falling platform variables
	/// <summary> Animator to handle falling platform behaviour. </summary>
	private AnimationPlayer fallingPlatformAnimator;
	private NodePath fallingPlatformAnimatorPath;
	/// <summary> Will this platform fall when the player steps on it? </summary>
	private bool isFallingBehaviourEnabled;
	/// <summary> Should the platform automatically start to shake when the player steps on it? </summary>
	private bool autoShake = true;
	/// <summary> How long to shake before falling. </summary>
	private float shakeLength;
	/// <summary> How quickly to fall. </summary>
	private float fallSpeedScale = 1f;
	/// <summary> Keeps track of the platform's position from the previous frame. </summary>
	private Vector3 previousPosition;
	/// <summary> Timer to keep track of shaking status. </summary>
	private float shakeTimer;
	/// <summary> Is the platform about to fall? </summary>
	private bool isPlatformShaking;

	// Floating platform variables
	/// <summary> Will this platform bob up and down when the player jumps on it? </summary>
	private bool isFloatingBehaviorEnabled;
	/// <summary> Determines the maximum (and minimum) height the platform can bob to. </summary>
	private float floatingHeightRange = 1f;
	/// <summary> Determines how much force it takes to move the platform. </summary>
	private float floatingForceMultiplier = 1f;
	/// <summary> Determines how strongly the platform bounces back to neutral. </summary>
	private float buoyancyStrength = 5f;
	/// <summary>
	/// Keeps track of the floating platform's current velocity.
	/// Initialize to a non-zero value if you want the platform to start in a bobbing state.
	/// </summary>
	private float currentBuoyancyForce;
	/// <summary> The platform's initial Y-Position. </summary>
	private float floatingReferenceHeight;
	/// <summary> The platform's starting Y-Position, used to offset different platforms from each other. </summary>
	private float floatingInitialHeightRatio;
	/// <summary> Has the force from the incoming player been added to the platform? </summary>
	private bool appliedFloatingForce;
	private readonly float BuoyancySmoothingRatio = 0.4f;
	private readonly float BaseFloatingForceMultiplier = 0.5f;
	private readonly float BasePlayerFloatingForce = 5f;

	/// <summary> Bump this up to allow the player to go flying when jumping off. </summary>
	[Export] private float playerJumpInfluenceMultiplier = 1f;
	[Export(PropertyHint.Range, "0,1,.1,or_greater")] private float maxJumpMovementAmount;

	[ExportSubgroup("Components")]
	/// <summary> Assign this to enable moving the player with the platform. </summary>
	[Export] private Node3D floorCalculationRoot;
	/// <summary> Reference to the "floor" collider. </summary>
	[Export] private PhysicsBody3D parentCollider;
	private PlayerController Player => StageSettings.Player;

	private bool isActive;
	private bool isInteractingWithPlayer;
	private float playerInfluence;
	/// <summary> Tracks whether the platform can artificially snap the player. </summary>
	private bool canSnapPlayer;
	private readonly float PlayerInfluenceReset = .5f;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		if (isFallingBehaviourEnabled)
		{
			fallingPlatformAnimator = GetNodeOrNull<AnimationPlayer>(fallingPlatformAnimatorPath);
			if (fallingPlatformAnimator == null)
				GD.PrintErr($"Falling platform animator is missing on {Name}!");

			if (autoShake) // Falling behaviour is enabled, connect signal.
				Connect(SignalName.PlatformInteracted, new Callable(this, MethodName.StartShaking));
		}

		if (isFloatingBehaviorEnabled)
		{
			floatingReferenceHeight = GlobalPosition.Y;
			GlobalPosition += Vector3.Up * floatingInitialHeightRatio * floatingHeightRange;
		}

		StageSettings.Instance.Respawned += Respawn;
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) return;

		UpdateFloatingPlatform();
		UpdatePlatform();
	}

	private void Respawn()
	{
		if (isFallingBehaviourEnabled)
			fallingPlatformAnimator.Play("RESET");
	}

	private void UpdatePlatform()
	{
		if (!isPlatformShaking)
		{
			if (!isInteractingWithPlayer) return;

			if (!isActive && Player.IsOnGround)
			{
				isActive = true;
				EmitSignal(SignalName.PlatformInteracted);
			}
			else
			{
				return;
			}
		}

		if (isPlatformShaking)
			UpdateFallingPlatformBehaviour();

		if (floorCalculationRoot != null)
			CallDeferred(MethodName.SyncPlayerMovement);
	}

	private void UpdateFloatingPlatform()
	{
		if (!isFloatingBehaviorEnabled || Mathf.IsZeroApprox(floatingHeightRange))
			return;

		if (isInteractingWithPlayer)
			ProcessIncomingFloatForce();

		float deltaForce = (floatingReferenceHeight - GlobalPosition.Y) / floatingHeightRange;
		if (Mathf.Abs(deltaForce) > 1.0f - BuoyancySmoothingRatio) // Smooth out bottoms
		{
			float smoothingRatio = (1.0f - Mathf.Abs(deltaForce)) / BuoyancySmoothingRatio;
			if (deltaForce > 0 && currentBuoyancyForce < 0) // Bounce back faster when hitting the bottom
			{
				deltaForce += (1.0f - smoothingRatio) * 10f;
				currentBuoyancyForce *= 0.8f;
			}
		}

		if (GlobalPosition.Y > floatingReferenceHeight + floatingHeightRange) // Add gravity when the platform is too high up
			currentBuoyancyForce -= Runtime.Gravity * PhysicsManager.physicsDelta;

		currentBuoyancyForce += buoyancyStrength * deltaForce * PhysicsManager.physicsDelta;

		// Move platform, but prevent it from going too low
		GlobalPosition += Vector3.Up * currentBuoyancyForce * PhysicsManager.physicsDelta;
	}

	private void ProcessIncomingFloatForce()
	{
		if (appliedFloatingForce)
			return;

		if (!IsPlayerOnPlatform()) // Force isn't going to be applied -- don't bother calculating anything
			return;

		float targetForce = Mathf.Min(Player.VerticalSpeed, 0f);
		if (Player.IsOnGround) // Apply a consistent force if the player walks onto a platform
			targetForce = BasePlayerFloatingForce;

		currentBuoyancyForce = -Mathf.Abs(targetForce) * floatingForceMultiplier * BaseFloatingForceMultiplier;
		appliedFloatingForce = true;
	}

	/// <summary> Make the platform start shaking. This can also be called from a signal. </summary>
	public void StartShaking()
	{
		if (isPlatformShaking) return; // Already shaking

		if (Mathf.IsZeroApprox(shakeLength)) // Fall immediately
		{
			StartFalling();
			return;
		}

		isPlatformShaking = true;
		fallingPlatformAnimator.Play("shake");
		fallingPlatformAnimator.SpeedScale = 1f;
	}

	/// <summary> Called when the platform begins to fall. </summary>
	private void StartFalling()
	{
		isPlatformShaking = false; // Stop shaking
		fallingPlatformAnimator.Play("fall", .2);
		fallingPlatformAnimator.SpeedScale = fallSpeedScale;
	}

	private void UpdateFallingPlatformBehaviour()
	{
		shakeTimer += PhysicsManager.physicsDelta;

		if (shakeTimer > shakeLength)
		{
			shakeTimer = 0;
			StartFalling();
		}
	}

	/// <summary> Moves the player with the platform. </summary>
	private void SyncPlayerMovement()
	{
		if ((!Player.IsOnGround && Player.Velocity.Y >= 0) || !isInteractingWithPlayer)
		{
			Vector3 delta = floorCalculationRoot.GlobalPosition - previousPosition;
			if (delta.Y <= 0) // Not moving upwards -- reset influence instantly
				playerInfluence = 0;

			if (Mathf.IsZeroApprox(playerInfluence))
			{
				isActive = false;
			}
			else
			{
				float amount = delta.Y * playerInfluence * playerJumpInfluenceMultiplier;
				if (!Mathf.IsZeroApprox(maxJumpMovementAmount))
					amount = Mathf.Min(amount, maxJumpMovementAmount * PhysicsManager.physicsDelta);
				Player.GlobalTranslate(Vector3.Up * amount);
				playerInfluence = Mathf.MoveToward(playerInfluence, 0, PlayerInfluenceReset * PhysicsManager.physicsDelta);
			}

			previousPosition = floorCalculationRoot.GlobalPosition;
			return;
		}

		if (!IsPlayerOnPlatform())
			return;

		playerInfluence = 1f; // Set player influence to 1 for when we leave
		previousPosition = floorCalculationRoot.GlobalPosition;

		if (Player.IsOnGround)
			canSnapPlayer = true;

		if (canSnapPlayer)
			Player.GlobalTranslate(Vector3.Up * (floorCalculationRoot.GlobalPosition.Y - Player.GlobalPosition.Y));
	}

	/// <summary> Checks whether the player is currently standing on top of the platform. </summary>
	private bool IsPlayerOnPlatform()
	{
		float checkLength = Mathf.Abs(Player.CenterPosition.Y - floorCalculationRoot.GlobalPosition.Y) + (Player.CollisionSize.Y * 2.0f);
		KinematicCollision3D collision = Player.MoveAndCollide(-Player.UpDirection * checkLength, true);

		if (collision == null || (Node3D)collision.GetCollider() != parentCollider) // Player is not on the platform
			return false;

		return true;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		isInteractingWithPlayer = true;
		canSnapPlayer = false; // Prevent initial snapping
		UpdatePlatform();

		if (isFloatingBehaviorEnabled)
		{
			appliedFloatingForce = false;
			ProcessIncomingFloatForce();
		}
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		isInteractingWithPlayer = false;
	}
}