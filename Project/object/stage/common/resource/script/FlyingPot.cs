using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Objects;

/// <summary> Mash the action button for maximum speed. </summary>
[Tool]
public partial class FlyingPot : Node3D
{
	[Export]
	public Vector2 travelBounds;
	[Export]
	public float boundOffset;

	[Export]
	private CameraSettingsResource customCameraSettings;

	[ExportGroup("Components")]
	[Export]
	private Node3D root;
	[Export]
	private Area3D lockonArea;
	[Export]
	private CollisionShape3D environmentCollider;
	[Export]
	private AnimationTree animationTree;
	[Export]
	private AudioStreamPlayer enterSFX;
	[Export]
	private AudioStreamPlayer exitSFX;
	private CameraTrigger cameraTrigger;

	private readonly StringName EnterTrigger = "parameters/enter_trigger/request";
	private readonly StringName FallTransition = "parameters/fall_transition/transition_request";
	private readonly StringName FlapTrigger = "parameters/flap_trigger/request";
	private readonly StringName FlapSpeed = "parameters/flap_speed/scale";
	private readonly StringName EnabledState = "enabled";
	private readonly StringName DisabledState = "disabled";

	private bool isProcessing;
	private bool isControllingPlayer;
	private bool interactingWithPlayer;
	private bool isLeavingPot;
	private bool isEnteringPot;

	private float flapSpeed = 1;
	private float flapTimer;
	private float angle;
	private float velocity;
	private Vector2 localPosition;

	private const float MaxGravity = -10.0f;
	private const float WingAcceleration = 4f;
	private const float MaxSpeed = 12.0f;
	private const float RotationSpeed = .1f;
	private const float MaxAngle = Mathf.Pi * .2f;
	private const float FlapInterval = .5f; // How long is a single flap?
	private const float FlapAccelerationLength = .4f; // How long does a flap accelerate?

	public PlayerController Player => StageSettings.Player;

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;

		animationTree.Active = true;
		StageSettings.Instance.ConnectRespawnSignal(this);

		if (customCameraSettings != null) // Create the camera trigger
		{
			cameraTrigger = new CameraTrigger()
			{
				transitionTime = .2f, // Default to .2 seconds for transitions
				settings = customCameraSettings
			};
			AddChild(cameraTrigger);
		}
	}

	private void Respawn()
	{
		angle = 0f;
		velocity = 0f;
		localPosition = Vector2.Zero;
		ApplyMovement();

		lockonArea.SetDeferred("monitorable", true);
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing || Engine.IsEditorHint()) return;

		if (interactingWithPlayer)
		{
			if (isControllingPlayer)
				ProcessMovement();
			else if (!isEnteringPot && !Player.IsOnGround)
				StartJump();
		}
		else if (!lockonArea.Monitorable) // Re-enable lockon
		{
			lockonArea.SetDeferred("monitorable", Player.VerticalSpeed < 0f);
		}

		ApplyMovement();
	}

	private void StartJump()
	{
		isEnteringPot = true;
		environmentCollider.Disabled = true;

		float jumpHeight = GlobalPosition.Y + 1 - Player.GlobalPosition.Y;
		jumpHeight = Mathf.Clamp(jumpHeight * 2, 0, 2);
		LaunchSettings settings = LaunchSettings.Create(Player.GlobalPosition, root.GlobalPosition, jumpHeight, false);
		settings.IsJump = true;
		settings.AllowJumpDash = false;
		Player.StartLauncher(settings);

		lockonArea.SetDeferred("monitorable", false);

		Player.Skills.IsSpeedBreakEnabled = false; // Disable speed break
		Player.LaunchFinished += OnEnteredPot;
		
		// Update camera
		if (cameraTrigger != null)
		{
			cameraTrigger.settings.yawAngle = ExtensionMethods.ModAngle(GlobalRotation.Y + Mathf.Pi); // Sync viewAngle to current flying pot's rotation
			cameraTrigger.Activate();
		}
	}

	private void OnEnteredPot()
	{
		flapTimer = 0;
		isControllingPlayer = true;
		Player.LaunchFinished -= OnEnteredPot;
		Player.StartExternal(this, root);
		Player.Animator.Visible = false;
		animationTree.Set(EnterTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		enterSFX.Play();
	}

	private void EjectPlayer()
	{
		isLeavingPot = true;
		isControllingPlayer = false;

		velocity = 0f; // Kill all velocity

		float angleRatio = angle / MaxAngle;
		Player.MovementAngle = ExtensionMethods.CalculateForwardAngle(this.Back());
		Player.VerticalSpeed = Runtime.CalculateJumpPower(Player.Stats.JumpHeight);

		Player.Animator.JumpAnimation();
		Player.Animator.SnapRotation(Player.MovementAngle - (Mathf.Pi * angleRatio));
		Player.Animator.Visible = true;
		Player.StopExternal();

		animationTree.Set(EnterTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		exitSFX.Play();

		cameraTrigger?.Deactivate();
	}

	private void ProcessMovement()
	{
		float targetRotation = Player.Controller.InputHorizontal * MaxAngle;
		angle = Mathf.Lerp(angle, targetRotation, RotationSpeed);

		if (Input.IsActionJustPressed("button_jump"))
		{
			EjectPlayer();
			return;
		}

		if (Input.IsActionJustPressed("button_action")) // Move upwards
		{
			animationTree.Set(FlapTrigger, (uint)AnimationNodeOneShot.OneShotRequest.Fire);
			flapSpeed = 1.5f + (flapTimer / FlapInterval);
			flapTimer = FlapInterval;
		}

		if (!Mathf.IsZeroApprox(flapTimer)) // Accelerate
		{
			if (velocity < 0)
				velocity *= .2f;

			flapTimer = Mathf.MoveToward(flapTimer, 0, PhysicsManager.physicsDelta);
			if (flapTimer >= FlapAccelerationLength)
				velocity += WingAcceleration;
		}

		if (velocity > MaxSpeed)
			velocity = MaxSpeed;

		flapSpeed = Mathf.Lerp(flapSpeed, 1, .1f);
		animationTree.Set(FlapSpeed, flapSpeed);
	}

	private void ApplyMovement()
	{
		if (velocity > 0)
			localPosition += Vector2.Down.Rotated(-angle) * velocity * PhysicsManager.physicsDelta;
		else
			localPosition += Vector2.Down * velocity * PhysicsManager.physicsDelta;

		localPosition.X = Mathf.Clamp(localPosition.X, -travelBounds.X + boundOffset, travelBounds.X + boundOffset);
		localPosition.Y = Mathf.Clamp(localPosition.Y, 0f, travelBounds.Y);

		if (!Mathf.IsZeroApprox(localPosition.Y)) // Fall
		{
			// Update velocity
			velocity -= Runtime.Gravity * PhysicsManager.physicsDelta; // Floaty fall
			if (velocity < MaxGravity)
				velocity = MaxGravity;

			if (velocity < 0)
				animationTree.Set(FallTransition, EnabledState);
		}
		else if (velocity != 0) // Return to idle flapping
		{
			velocity = 0;
			animationTree.Set(FallTransition, DisabledState);
		}

		if (!isControllingPlayer)
			angle = Mathf.Lerp(angle, 0f, RotationSpeed);

		root.Position = new Vector3(localPosition.X, localPosition.Y, 0);
		root.Rotation = Vector3.Forward * angle;

		if (isControllingPlayer)
			Player.UpdateExternalControl(); // Sync player object

		if (lockonArea.Monitorable && !interactingWithPlayer)
			isProcessing = !Mathf.IsZeroApprox(localPosition.Y) || !Mathf.IsZeroApprox(angle); // Update sleeping status
	}

	public void PlayerEntered(Area3D _)
	{
		isProcessing = true;
		isLeavingPot = false;
		interactingWithPlayer = true;
	}

	public void PlayerExited(Area3D _)
	{
		if (isLeavingPot)
		{
			Player.CanJumpDash = true; // So the player isn't completely helpless
			isEnteringPot = false;
		}

		interactingWithPlayer = false;
		environmentCollider.SetDeferred("disabled", false);
	}
}