using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Objects;

/// <summary> Mash the action button for maximum speed. </summary>
[Tool]
public partial class FlyingPot : Node3D
{
	[Export] public Vector2 travelBounds;
	[Export] public float boundOffset;

	[Export] private CameraSettingsResource customCameraSettings;

	[ExportGroup("Components")]
	[Export] private Node3D root;
	public Node3D Root => root;
	[Export] private Area3D lockonArea;
	[Export] private CollisionShape3D environmentCollider;
	[Export] private AnimationTree animationTree;
	[Export] private AnimationPlayer interactionAnimator;
	[Export] private AudioStreamPlayer enterSFX;
	[Export] private AudioStreamPlayer exitSFX;
	private CameraTrigger cameraTrigger;

	private readonly StringName EnterTrigger = "parameters/enter_trigger/request";
	private readonly StringName FallTransition = "parameters/fall_transition/transition_request";
	private readonly StringName FlapTrigger = "parameters/flap_trigger/request";
	private readonly StringName FlapSpeed = "parameters/flap_speed/scale";
	private readonly StringName EnabledState = "enabled";
	private readonly StringName DisabledState = "disabled";

	private bool isSleeping = true;
	private bool interactingWithPlayer;
	private float angle;
	public float Angle => angle;
	public float Velocity { get; set; }
	private Vector2 localPosition;

	private const float RotationSpeed = .1f;
	private const float MaxGravity = -10.0f;

	public PlayerController Player => StageSettings.Player;

	public override void _Ready()
	{
		if (Engine.IsEditorHint()) return;

		animationTree.Active = true;
		StageSettings.Instance.ConnectRespawnSignal(this);

		if (customCameraSettings == null)
			return;

		// Create the camera trigger
		cameraTrigger = new CameraTrigger()
		{
			transitionTime = .2f, // Default to .2 seconds for transitions
			settings = customCameraSettings
		};
		AddChild(cameraTrigger);
	}

	private void Respawn()
	{
		ResetPosition();

		interactionAnimator.Play("RESET");
		lockonArea.SetDeferred("monitorable", true);
	}

	public override void _PhysicsProcess(double _)
	{
		if (isSleeping || Engine.IsEditorHint()) return;

		if (!interactingWithPlayer && !lockonArea.Monitorable) // Re-enable lockon
			lockonArea.SetDeferred("monitorable", Player.VerticalSpeed < 0f);

		if (Player.IsFlyingPotActive)
			return;

		if (interactingWithPlayer && !Player.IsOnGround && !environmentCollider.Disabled)
			StartJump();

		UpdateAngle(0);
		ApplyMovement();
	}

	private void ResetPosition()
	{
		angle = 0f;
		Velocity = 0f;
		localPosition = Vector2.Zero;
		ApplyMovement();
	}

	public void Flap() => animationTree.Set(FlapTrigger, (uint)AnimationNodeOneShot.OneShotRequest.Fire);
	public void UpdateFlap(float speed) => animationTree.Set(FlapSpeed, speed);
	public void UpdateAngle(float targetAngle) => angle = Mathf.Lerp(angle, targetAngle, RotationSpeed);

	private void StartJump()
	{
		environmentCollider.Disabled = true;

		float jumpHeight = GlobalPosition.Y + 1 - Player.GlobalPosition.Y;
		jumpHeight = Mathf.Clamp(jumpHeight * 2, 0, 2);
		LaunchSettings settings = LaunchSettings.Create(Player.GlobalPosition, root.GlobalPosition, jumpHeight, false);
		settings.IsJump = true;
		settings.AllowJumpDash = false;
		Player.StartLauncher(settings);

		lockonArea.SetDeferred("monitorable", false);

		Player.Skills.IsSpeedBreakEnabled = false;
		Player.LaunchFinished += OnEnteredPot;

		if (cameraTrigger == null)
			return;

		// Sync viewAngle to current flying pot's rotation
		cameraTrigger.settings.yawAngle = ExtensionMethods.ModAngle(GlobalRotation.Y + Mathf.Pi);
		cameraTrigger.Activate();
	}

	private void OnEnteredPot()
	{
		enterSFX.Play();
		animationTree.Set(EnterTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		Player.StartFlyingPot(this);
		Player.LaunchFinished -= OnEnteredPot;
	}

	public void PlayExitFX()
	{
		exitSFX.Play();
		animationTree.Set(EnterTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		cameraTrigger?.Deactivate();
	}

	public void Shatter()
	{
		interactionAnimator.Play("shatter");
		cameraTrigger?.Deactivate();
	}

	public void ApplyMovement()
	{
		if (Velocity > 0)
			localPosition += Vector2.Down.Rotated(-Angle) * Velocity * PhysicsManager.physicsDelta;
		else
			localPosition += Vector2.Down * Velocity * PhysicsManager.physicsDelta;

		localPosition.X = Mathf.Clamp(localPosition.X, -travelBounds.X + boundOffset, travelBounds.X + boundOffset);
		localPosition.Y = Mathf.Clamp(localPosition.Y, 0f, travelBounds.Y);

		if (!Mathf.IsZeroApprox(localPosition.Y)) // Fall
		{
			// Update velocity
			Velocity -= Runtime.Gravity * PhysicsManager.physicsDelta; // Floaty fall
			if (Velocity < MaxGravity)
				Velocity = MaxGravity;

			if (Velocity < 0)
				animationTree.Set(FallTransition, EnabledState);
		}
		else if (Velocity != 0) // Return to idle flapping
		{
			Velocity = 0;
			animationTree.Set(FallTransition, DisabledState);
		}

		root.Position = new Vector3(localPosition.X, localPosition.Y, 0);
		root.Rotation = Vector3.Forward * Angle;

		if (lockonArea.Monitorable && !interactingWithPlayer)
			isSleeping = Mathf.IsZeroApprox(localPosition.Y) && Mathf.IsZeroApprox(Angle); // Update sleeping status
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isSleeping = false;
		interactingWithPlayer = true;

		if (!Player.IsFlyingPotActive && !Player.IsOnGround)
			StartJump();
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		interactingWithPlayer = false;
		environmentCollider.SetDeferred("disabled", false);
	}
}