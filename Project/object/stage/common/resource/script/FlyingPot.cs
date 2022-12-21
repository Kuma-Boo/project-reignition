using Project.Core;
using Godot;

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
		private Node3D root;
		[Export]
		private CollisionShape3D environmentCollider;
		[Export]
		private Area3D lockonArea;
		[Export]
		private CameraSettingsResource cameraSettings;

		private bool isControllingPlayer;
		private bool interactingWithPlayer;
		private bool isLeavingPot;
		private bool isEnteringPot;
		[Export]
		private AnimationPlayer interactionAnimator;
		[Export]
		private AnimationPlayer wingAnimator;
		[Export]
		private bool canTransitionToFalling; //Animator parameter to sync falling transition
		[Export]
		private bool isFalling; //Animator parameter to check falling status
		private bool isProcessing;

		private float flapTimer;
		private float angle;
		private float velocity;
		private Vector2 position;
		private Vector3 startPosition;

		private const float MAX_GRAVITY = -10.0f;
		private const float GRAVITY_SCALE = 0.26f;
		private const float WING_POWER = 4.0f;
		private const float MAX_SPEED = 12.0f;
		private const float ROTATION_SPEED = .1f;
		private const float MAX_ANGLE = Mathf.Pi * .2f;
		private const float FLAP_INTERVAL = .32f; //How fast can the player flap?

		public CharacterController Character => CharacterController.instance;
		public InputManager.Controller Controller => Character.Controller;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			startPosition = GlobalPosition;
			StageSettings.instance.ConnectRespawnSignal(this);
		}

		private void Respawn()
		{
			angle = 0f;
			velocity = 0f;
			position = Vector2.Zero;
			GlobalPosition = startPosition;

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
				lockonArea.SetDeferred("monitorable", Character.VerticalSpd < 0f);

			ApplyMovement();
		}

		private void StartJump()
		{
			isEnteringPot = true;
			environmentCollider.Disabled = true;

			float jumpHeight = (GlobalPosition.y + 1) - Character.GlobalPosition.y;
			jumpHeight = Mathf.Clamp(jumpHeight * 2, 0, 2);
			Character.JumpTo(GlobalPosition, jumpHeight, true);

			lockonArea.SetDeferred("monitorable", false);

			Character.CanJumpDash = false;
			Character.Skills.IsSpeedBreakEnabled = false; //Disable speed break

			Character.Lockon.ResetLockonTarget();
			Character.Connect(CharacterController.SignalName.LauncherFinished, new Callable(this, MethodName.OnEnteredPot), (uint)ConnectFlags.OneShot);

			//Update camera
			if (cameraSettings != null)
			{
				cameraSettings.viewAngle.y = (Mathf.RadToDeg(GlobalRotation.y) + 180) % 360; //Sync viewAngle to current flying pot's rotation
				GD.Print(cameraSettings.viewAngle.y);
				Character.Camera.SetCameraData(cameraSettings);
			}
		}

		private void OnEnteredPot()
		{
			flapTimer = 0;
			isControllingPlayer = true;
			Character.StartExternal(this);
			Character.Animator.Visible = false;
			interactionAnimator.Play("enter");
		}

		private void EjectPlayer()
		{
			isLeavingPot = true;
			isControllingPlayer = false;

			velocity = 0f; //Kill all velocity

			Character.VerticalSpd = RuntimeConstants.GetJumpPower(Character.jumpHeight);
			//Character.StrafeSpeed = Character.airStrafeSettings.speed * (angle / MAX_ANGLE);
			Character.ResetMovementState();
			Character.Animator.Visible = true;
			interactionAnimator.Play("exit");
		}

		private void ProcessMovement()
		{
			float targetRotation = -Controller.horizontalAxis.value * MAX_ANGLE;
			angle = Mathf.Lerp(angle, targetRotation, ROTATION_SPEED);

			if (Controller.jumpButton.wasPressed)
			{
				EjectPlayer();
				return;
			}

			flapTimer = Mathf.MoveToward(flapTimer, 0, PhysicsManager.physicsDelta);

			if (Mathf.IsZeroApprox(flapTimer) && Controller.actionButton.wasPressed) //Move upwards
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
				position += Vector2.Down.Rotated(angle) * velocity * PhysicsManager.physicsDelta;
			else
				position += Vector2.Down * velocity * PhysicsManager.physicsDelta;

			position.x = Mathf.Clamp(position.x, -travelBounds.x, travelBounds.x);
			position.y = Mathf.Clamp(position.y, 0f, travelBounds.y);
			if (Mathf.IsZeroApprox(position.y))
			{
				velocity = 0;

				if (isFalling)
					wingAnimator.Play("flap");
			}
			else if (velocity < 0 && canTransitionToFalling)
				wingAnimator.Play("fall"); //Start fall animation

			//Update velocity
			velocity -= RuntimeConstants.GRAVITY * GRAVITY_SCALE * PhysicsManager.physicsDelta; //Floaty fall
			if (velocity < MAX_GRAVITY)
				velocity = MAX_GRAVITY;

			if (!isControllingPlayer)
				angle = Mathf.Lerp(angle, 0f, ROTATION_SPEED);

			GlobalPosition = startPosition + Vector3.Up * position.y + this.Right() * position.x;
			root.Rotation = Vector3.Back * angle;

			if (isControllingPlayer)
				Character.UpdateExternalControl(); //Sync player object

			if (lockonArea.Monitorable && !interactingWithPlayer)
				isProcessing = !Mathf.IsZeroApprox(position.y) || !Mathf.IsZeroApprox(angle); //Update sleeping status
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
