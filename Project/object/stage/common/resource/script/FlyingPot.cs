using Godot;
using Project.Core;
using Project.Gameplay.Triggers;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Mash the action button for maximum speed.
	/// </summary>
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


		private AnimationNodeStateMachinePlayback WingStatePlayback => animationTree.Get(WING_STATE_PLAYBACK_PARAMETER).Obj as AnimationNodeStateMachinePlayback;
		private readonly StringName ACTION_TRIGGER = "parameters/action_trigger/request";
		private readonly StringName WING_STATE_PLAYBACK_PARAMETER = "parameters/wing_state/playback";
		private readonly StringName FLAP_STATE = "pot-flap";
		private readonly StringName IDLE_STATE = "pot-idle";

		private bool isProcessing;
		private bool isControllingPlayer;
		private bool interactingWithPlayer;
		private bool isLeavingPot;
		private bool isEnteringPot;

		private float flapTimer;
		private float angle;
		private float velocity;
		private Vector2 localPosition;

		private const float MAX_GRAVITY = -10.0f;
		private const float WING_ACCELERATION = 4f;
		private const float MAX_SPEED = 12.0f;
		private const float ROTATION_SPEED = .1f;
		private const float MAX_ANGLE = Mathf.Pi * .2f;
		private const float FLAP_INTERVAL = .5f; // How long is a single flap?
		private const float FLAP_ACCELERATION_LENGTH = .4f; // How long does a flap accelerate?

		public CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			animationTree.Active = true;
			StageSettings.instance.ConnectRespawnSignal(this);

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
				else if (!isEnteringPot && !Character.IsOnGround)
					StartJump();
			}
			else if (!lockonArea.Monitorable) //Re-enable lockon
				lockonArea.SetDeferred("monitorable", Character.VerticalSpeed < 0f);

			ApplyMovement();
		}

		private void StartJump()
		{
			isEnteringPot = true;
			environmentCollider.Disabled = true;

			float jumpHeight = GlobalPosition.Y + 1 - Character.GlobalPosition.Y;
			jumpHeight = Mathf.Clamp(jumpHeight * 2, 0, 2);
			LaunchSettings settings = LaunchSettings.Create(Character.GlobalPosition, root.GlobalPosition, jumpHeight, false);
			settings.IsJump = true;
			Character.StartLauncher(settings);

			lockonArea.SetDeferred("monitorable", false);

			Character.CanJumpDash = false;
			Character.Skills.IsSpeedBreakEnabled = false; //Disable speed break

			Character.Lockon.ResetLockonTarget();
			Character.Connect(CharacterController.SignalName.LaunchFinished, new Callable(this, MethodName.OnEnteredPot), (uint)ConnectFlags.OneShot);

			//Update camera
			if (cameraTrigger != null)
			{
				cameraTrigger.settings.yawAngle = ExtensionMethods.ModAngle(GlobalRotation.Y + Mathf.Pi); //Sync viewAngle to current flying pot's rotation
				cameraTrigger.Activate();
			}
		}

		private void OnEnteredPot()
		{
			flapTimer = 0;
			isControllingPlayer = true;
			Character.StartExternal(this, root);
			Character.Animator.Visible = false;
			animationTree.Set(ACTION_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			enterSFX.Play();
		}

		private void EjectPlayer()
		{
			isLeavingPot = true;
			isControllingPlayer = false;

			velocity = 0f; //Kill all velocity

			float angleRatio = angle / MAX_ANGLE;
			Character.MovementAngle = ExtensionMethods.CalculateForwardAngle(this.Back());
			Character.VerticalSpeed = Runtime.CalculateJumpPower(Character.jumpHeight);

			Character.Animator.JumpAnimation();
			Character.Animator.Visible = true;
			Character.Animator.SnapRotation(Character.MovementAngle - Mathf.Pi * angleRatio);
			Character.ResetMovementState();

			animationTree.Set(ACTION_TRIGGER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			exitSFX.Play();

			if (cameraTrigger != null)
				cameraTrigger.Deactivate();
		}

		private void ProcessMovement()
		{
			float targetRotation = Character.InputHorizontal * MAX_ANGLE;
			angle = Mathf.Lerp(angle, targetRotation, ROTATION_SPEED);

			if (Input.IsActionJustPressed("button_jump"))
			{
				EjectPlayer();
				return;
			}


			if (!Mathf.IsZeroApprox(flapTimer)) // Accelerate
			{
				if (velocity < 0)
					velocity *= .2f;

				flapTimer = Mathf.MoveToward(flapTimer, 0, PhysicsManager.physicsDelta);
				if (flapTimer >= FLAP_ACCELERATION_LENGTH)
					velocity += WING_ACCELERATION;
			}
			else if (Input.IsActionJustPressed("button_action")) // Move upwards
			{
				WingStatePlayback.Travel(FLAP_STATE);
				flapTimer = FLAP_INTERVAL;
			}

			if (velocity > MAX_SPEED)
				velocity = MAX_SPEED;
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
				velocity -= Runtime.GRAVITY * PhysicsManager.physicsDelta; //Floaty fall
				if (velocity < MAX_GRAVITY)
					velocity = MAX_GRAVITY;
			}
			else if (velocity != 0) // Return to idle flapping
			{
				velocity = 0;
				WingStatePlayback.Travel(IDLE_STATE);
			}

			if (!isControllingPlayer)
				angle = Mathf.Lerp(angle, 0f, ROTATION_SPEED);

			root.Position = new Vector3(localPosition.X, localPosition.Y, 0);
			root.Rotation = Vector3.Forward * angle;

			if (isControllingPlayer)
				Character.UpdateExternalControl(); //Sync player object

			if (lockonArea.Monitorable && !interactingWithPlayer)
				isProcessing = !Mathf.IsZeroApprox(localPosition.Y) || !Mathf.IsZeroApprox(angle); //Update sleeping status
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
				Character.CanJumpDash = true; //So the player isn't completely helpless
				isEnteringPot = false;
			}

			interactingWithPlayer = false;
			environmentCollider.SetDeferred("disabled", false);
		}
	}
}
