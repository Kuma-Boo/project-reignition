using Project.Core;
using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Mash the action button for maximum speed.
	/// </summary>
	[Tool]
	public class FlyingPot : Spatial
	{
		[Export]
		public Vector2 travelBounds;
		[Export]
		public NodePath root;
		private Spatial _root;
		[Export]
		public NodePath environmentCollider;
		private CollisionShape _environmentCollider;
		[Export]
		public NodePath lockonArea;
		private Area _lockonArea;
		[Export]
		public CameraSettingsResource cameraSettings;
		
		private bool isControllingPlayer;
		private bool interactingWithPlayer;
		private bool isLeavingPot;
		private bool isEnteringPot;

		private float angle;
		private Vector2 position;
		private Vector2 velocity;
		private Vector3 startPosition;

		private const float GRAVITY = 16.0f;
		private const float WING_POWER = 4.0f;
		private const float MAX_SPEED = 12.0f;
		private const float HORIZONTAL_DRAG = .1f;
		private const float ROTATION_SPEED = .1f;
		private const float MAX_ANGLE = Mathf.Pi * .2f;

		public CharacterController Character => CharacterController.instance;
		public InputManager.Controller Controller => Character.Controller;

		public override void _Ready()
		{
			if (Engine.EditorHint) return;

			startPosition = GlobalTranslation;
			_root = GetNode<Spatial>(root);
			_lockonArea = GetNode<Area>(lockonArea);
			_environmentCollider = GetNode<CollisionShape>(environmentCollider);

			StageSettings.instance.RegisterRespawnableObject(this);
		}

		private void Respawn()
		{
			angle = 0f;
			position = Vector2.Zero;
			velocity = Vector2.Zero;
			GlobalTranslation = startPosition;

			_lockonArea.Monitorable = true;
		}

		public override void _PhysicsProcess(float _)
		{
			if (Engine.EditorHint) return;

			if (interactingWithPlayer)
			{
				if (isControllingPlayer)
					ProcessMovement();
				else if (!isEnteringPot && !Character.IsOnGround)
				{
					isEnteringPot = true;
					_environmentCollider.Disabled = true;

					if (Character.GlobalTranslation.y > GlobalTranslation.y)
						Character.JumpTo(GlobalTranslation);
					else
						Character.JumpTo(GlobalTranslation, 2f, true);

					_lockonArea.Monitorable = false;

					Character.CanJumpDash = false;
					Character.Lockon.ResetLockonTarget();
					Character.Connect(nameof(CharacterController.OnLauncherFinished), this, nameof(OnEnteredPot), null, (uint)ConnectFlags.Oneshot);
				}
			}
			else if (!_lockonArea.Monitorable) //Re-enable lockon
				_lockonArea.Monitorable = Character.IsFalling;

			if (!isControllingPlayer)
				angle = Mathf.Lerp(angle, 0f, ROTATION_SPEED);

			ApplyMovement();
		}

		private void OnEnteredPot()
		{
			isControllingPlayer = true;
			Character.StartExternal(this, true);
			Character.Visible = false;
		}

		private void EjectPlayer()
		{
			isLeavingPot = true;
			isControllingPlayer = false;

			velocity.y = 0f; //Kill all vertical velocity

			Character.VerticalSpeed = Character.JumpPower;
			Character.StrafeSpeed = Character.airStrafeSettings.speed * (angle / MAX_ANGLE);
			Character.ResetMovementState();
			Character.Visible = true;
		}

		private void ProcessMovement()
		{
			float targetRotation = Controller.horizontalAxis.value * MAX_ANGLE;
			angle = Mathf.Lerp(angle, targetRotation, ROTATION_SPEED);

			if (Controller.jumpButton.wasPressed)
			{
				EjectPlayer();
				return;
			}

			if (Controller.actionButton.wasPressed) //Move upwards
			{
				velocity += Vector2.Down.Rotated(angle) * WING_POWER;
				if (velocity.y > 0)
					velocity = velocity.LimitLength(MAX_SPEED);
			}
		}

		private void ApplyMovement()
		{
			position += velocity * PhysicsManager.physicsDelta;
			position.x = Mathf.Clamp(position.x, -travelBounds.x, travelBounds.x);
			position.y = Mathf.Clamp(position.y, 0f, travelBounds.y);
			if(Mathf.IsZeroApprox(position.y))
				velocity.y = 0;

			GlobalTranslation = startPosition + Vector3.Up * position.y + this.Right() * position.x;
			_root.Rotation = Vector3.Back * angle;

			velocity.x = Mathf.Lerp(velocity.x, 0f, HORIZONTAL_DRAG);
			velocity.y -= GRAVITY * PhysicsManager.physicsDelta;
			
			if(isControllingPlayer)
				Character.UpdateExternalControl();
		}

		public void PlayerEntered(Area a)
		{
			if (!a.IsInGroup("player")) return;

			interactingWithPlayer = true;
			isLeavingPot = false;

			Character.Soul.IsSpeedBreakEnabled = false;
			
			cameraSettings.viewAngle.y = 180f - Mathf.Rad2Deg(GlobalRotation.y); //Sync viewAngle to current flying pot's rotation
			cameraSettings.viewPosition = startPosition;
			cameraSettings.viewPosition.y = 0f; //Since heightTracking is enabled, this is unneeded.
			Character.Camera.SetCameraData(cameraSettings);
		}

		public void PlayerExited(Area a)
		{
			if (!a.IsInGroup("player")) return;

			if (isLeavingPot)
			{
				Character.CanJumpDash = true; //So the player isn't completely helpless
				isEnteringPot = false;
			}

			interactingWithPlayer = false;
			_environmentCollider.Disabled = false;
		}
	}
}
