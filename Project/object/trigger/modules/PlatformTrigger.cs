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
		Array<Dictionary> properties = [ExtensionMethods.CreateProperty("Falling Platform Settings/Disabled", Variant.Type.Bool)];

		if (!isFallingBehaviourDisabled)
		{
			properties.Add(ExtensionMethods.CreateProperty("Falling Platform Settings/Auto Shake", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Falling Platform Settings/Shake Length", Variant.Type.Float, PropertyHint.Range, "0, 10"));
		}

		return properties;
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Falling Platform Settings/Disabled":
				isFallingBehaviourDisabled = (bool)value;
				NotifyPropertyListChanged();
				break;
			case "Falling Platform Settings/Auto Shake":
				autoShake = (bool)value;
				break;
			case "Falling Platform Settings/Shake Length":
				shakeLength = (float)value;
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
			case "Falling Platform Settings/Disabled":
				return isFallingBehaviourDisabled;
			case "Falling Platform Settings/Auto Shake":
				return autoShake;
			case "Falling Platform Settings/Shake Length":
				return shakeLength;
		}

		return base._Get(property);
	}
	#endregion

	[Signal]
	public delegate void PlatformInteractedEventHandler();

	/// <summary> Is falling behaviour disabled? </summary>
	private bool isFallingBehaviourDisabled;
	/// <summary> Should the platform automatically start to shake when the player steps on it? </summary>
	private bool autoShake = true;
	/// <summary> How long to shake before falling. </summary>
	private float shakeLength;
	/// <summary> Keeps track of the platform's position from the previous frame. </summary>
	private Vector3 previousPosition;

	// Runtime data
	/// <summary> Timer to keep track of shaking status. </summary>
	private float shakeTimer;
	/// <summary> Is the platform about to fall? </summary>
	private bool isPlatformShaking;

	[ExportGroup("Components")]
	[Export]
	/// <summary> Assign this to enable moving the player with the platform. </summary>
	private Node3D floorCalculationRoot;
	[Export]
	/// <summary> Reference to the "floor" collider. </summary>
	private PhysicsBody3D parentCollider;
	[Export]
	/// <summary> Animator to handle falling platform behaviour. </summary>
	private AnimationPlayer fallingPlatformAnimator;
	private PlayerController Player => StageSettings.Player;

	private bool isActive;
	private bool isInteractingWithPlayer;
	private float playerInfluence;
	private readonly float PlayerInfluenceReset = .5f;

	public override void _Ready()
	{
		if (Engine.IsEditorHint() || isFallingBehaviourDisabled) return;

		if (fallingPlatformAnimator == null)
			GD.PrintErr($"Falling platform animator is missing on {Name}!");

		if (autoShake) // Falling behaviour is enabled, connect signal.
			Connect(SignalName.PlatformInteracted, new Callable(this, MethodName.StartShaking));
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint()) return;

		if (isPlatformShaking)
		{
			UpdateFallingPlatformBehaviour();

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

		if (floorCalculationRoot != null)
			CallDeferred(MethodName.SyncPlayerMovement);
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
	}

	/// <summary> Called when the platform begins to fall. </summary>
	private void StartFalling()
	{
		isPlatformShaking = false; // Stop shaking
		fallingPlatformAnimator.Play("fall", .2);
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
		if (!Player.IsOnGround || !isInteractingWithPlayer)
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
				Player.GlobalTranslate(Vector3.Up * delta.Y * playerInfluence);
				playerInfluence = Mathf.MoveToward(playerInfluence, 0, PlayerInfluenceReset * PhysicsManager.physicsDelta);
			}

			previousPosition = floorCalculationRoot.GlobalPosition;
			return;
		}

		float checkLength = Mathf.Abs(Player.CenterPosition.Y - floorCalculationRoot.GlobalPosition.Y) + (Player.CollisionSize.Y * 2.0f);
		KinematicCollision3D collision = Player.MoveAndCollide(-Player.PathFollower.HeightAxis * checkLength, true);

		if (collision == null || (Node3D)collision.GetCollider() != parentCollider) // Player is not on the platform
			return;

		playerInfluence = 1f; // Set player influence to 1 for when we leave
		previousPosition = floorCalculationRoot.GlobalPosition;
		Player.GlobalTranslate(Vector3.Up * (floorCalculationRoot.GlobalPosition.Y - Player.GlobalPosition.Y));
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;
		isInteractingWithPlayer = true;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection")) return;

		isInteractingWithPlayer = false;
	}
}