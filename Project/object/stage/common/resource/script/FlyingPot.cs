using Project.Core;
using Godot;

namespace Project.Gameplay
{
	[Tool]
	public class FlyingPot : Spatial
	{
		[Export]
		public NodePath root;
		private Spatial _root;
		[Export]
		public NodePath environmentCollider;
		private CollisionShape _environmentCollider;
		[Export]
		public Vector2 travelBounds;
		
		private bool isControllingPlayer;
		private bool interactingWithPlayer;
		private bool enteredPot;

		private float angle;
		private Vector2 position;
		private Vector2 velocity;
		private Vector3 startPosition;

		private const float GRAVITY = 24.0f;
		private const float WING_POWER = 12.0f;
		private const float HORIZONTAL_DRAG = .1f;
		private const float ROTATION_SPEED = .1f;

		public CharacterController Character => CharacterController.instance;
		public InputManager.Controller Controller => Character.Controller;

		public override void _Ready()
		{
			if (Engine.EditorHint) return;

			startPosition = GlobalTranslation;
			_root = GetNode<Spatial>(root);
			_environmentCollider = GetNode<CollisionShape>(environmentCollider);
		}

		public override void _PhysicsProcess(float _)
		{
			if (Engine.EditorHint) return;

			if(interactingWithPlayer)
			{
				if (isControllingPlayer)
					ProcessMovement();
				else if (!enteredPot && !Character.IsOnGround)
				{
					enteredPot = true;
					_environmentCollider.Disabled = true;
					Character.ResetLockonTarget();
					Character.JumpTo(GlobalTranslation, 2f);
					Character.Connect(nameof(CharacterController.OnLauncherFinished), this, nameof(OnEnteredPot), null, (uint)ConnectFlags.Oneshot);
				}
			}

			if(!isControllingPlayer)
				angle = Mathf.Lerp(angle, 0f, ROTATION_SPEED);

			ApplyMovement();
		}

		private void OnEnteredPot()
		{
			power = 1f; //Reset power
			isControllingPlayer = true;
			Character.StartExternalControl();
		}

		public float power;
		private const float POWER_RESET_SPEED = 2f;
		private void ProcessMovement()
		{
			float targetRotation = Controller.horizontalAxis.value * Mathf.Pi * .25f;
			angle = Mathf.Lerp(angle, targetRotation, ROTATION_SPEED);

			if (Controller.jumpButton.wasPressed)
			{
				EjectPlayer();
				return;
			}

			power = Mathf.MoveToward(power, 1f, POWER_RESET_SPEED * PhysicsManager.physicsDelta);
			if (Controller.actionButton.wasPressed) //Move upwards
			{
				if (power > .5f) //Prevent spamming
					velocity = Vector2.Down.Rotated(-angle) * WING_POWER * power;
				else if (velocity.y < 0f)
					velocity.y *= power;

				power = 0f;
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
			_root.Rotation = Vector3.Forward * angle;

			velocity.x = Mathf.Lerp(velocity.x, 0f, HORIZONTAL_DRAG);
			velocity.y -= GRAVITY * PhysicsManager.physicsDelta;
			
			if(isControllingPlayer)
				Character.GlobalTranslation = GlobalTranslation;
		}

		private void EjectPlayer()
		{
			isControllingPlayer = false;
			Character.JumpTo(Character.GlobalTranslation + _root.Up() * Character.jumpHeight);
		}

		public void PlayerEntered(Area a)
		{
			if (!a.IsInGroup("player")) return;

			enteredPot = false;
			interactingWithPlayer = true;
		}

		public void PlayerExited(Area a)
		{
			if (!a.IsInGroup("player")) return;
			
			interactingWithPlayer = false;
			_environmentCollider.Disabled = false;
		}
	}
}
