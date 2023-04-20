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
		private CameraTrigger cameraTrigger;
		[Export]
		private CollisionShape3D environmentCollider;

		[ExportGroup("Animation")]
		[Export]
		private AnimationPlayer interactionAnimator;
		[Export]
		private AnimationPlayer wingAnimator;
		[Export]
		private bool canTransitionToFalling; //Animator parameter to sync falling transition
		[Export]
		private bool isFalling; //Animator parameter to check falling status

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
		private const float GRAVITY_SCALE = 0.26f;
		private const float WING_POWER = 4.0f;
		private const float MAX_SPEED = 12.0f;
		private const float ROTATION_SPEED = .1f;
		private const float MAX_ANGLE = Mathf.Pi * .2f;
		private const float FLAP_INTERVAL = .32f; //How fast can the player flap?

		public CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			LevelSettings.instance.ConnectRespawnSignal(this);

			if (customCameraSettings != null)
				cameraTrigger.settings = customCameraSettings;
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

			float jumpHeight = (GlobalPosition.Y + 1) - Character.GlobalPosition.Y;
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
			interactionAnimator.Play("enter");
		}

		private void EjectPlayer()
		{
			isLeavingPot = true;
			isControllingPlayer = false;

			velocity = 0f; //Kill all velocity

			float angleRatio = angle / MAX_ANGLE;
			Character.MovementAngle = Character.CalculateForwardAngle(this.Back());
			Character.StrafeSpeed = Character.Skills.AirSettings.speed * angleRatio;
			Character.VerticalSpeed = Runtime.CalculateJumpPower(Character.jumpHeight);

			Character.Animator.Visible = true;
			Character.Animator.SnapRotation(Character.MovementAngle - Mathf.Pi * angleRatio);
			Character.ResetMovementState();

			interactionAnimator.Play("exit");

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

			flapTimer = Mathf.MoveToward(flapTimer, 0, PhysicsManager.physicsDelta);

			if (Mathf.IsZeroApprox(flapTimer) && Input.IsActionJustPressed("button_action")) //Move upwards
			{
				if (velocity < 0)
					velocity = 0;

				velocity += WING_POWER;
				if (velocity > MAX_SPEED)
					velocity = MAX_SPEED;

				wingAnimator.Play("flap");
				wingAnimator.Seek(0.0);
				flapTimer = FLAP_INTERVAL;
			}
		}

		private void ApplyMovement()
		{
			if (velocity > 0)
				localPosition += Vector2.Down.Rotated(-angle) * velocity * PhysicsManager.physicsDelta;
			else
				localPosition += Vector2.Down * velocity * PhysicsManager.physicsDelta;

			localPosition.X = Mathf.Clamp(localPosition.X, -travelBounds.X + boundOffset, travelBounds.X + boundOffset);
			localPosition.Y = Mathf.Clamp(localPosition.Y, 0f, travelBounds.Y);
			if (Mathf.IsZeroApprox(localPosition.Y))
			{
				velocity = 0;

				if (isFalling)
					wingAnimator.Play("flap");
			}
			else if (velocity < 0 && canTransitionToFalling)
				wingAnimator.Play("fall"); //Start fall animation

			//Update velocity
			velocity -= Runtime.GRAVITY * GRAVITY_SCALE * PhysicsManager.physicsDelta; //Floaty fall
			if (velocity < MAX_GRAVITY)
				velocity = MAX_GRAVITY;

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
