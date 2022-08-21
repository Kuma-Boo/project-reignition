using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public class Boat : KinematicBody
	{
		private Transform startingTransform;

		[Export]
		public CameraSettingsResource cameraSettings;

		private bool isActive;
		[Export]
		public float moveSpeed = 15f;
		private const float ACCELERATION = 8f;
		[Export]
		public MovementResource strafeSettings;

		private Vector2 velocity;
		
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		private float damageTimer;
		private readonly float DAMAGE_LOCKOUT_LENGTH = .5f;

		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			startingTransform = GlobalTransform;
			StageSettings.instance.RegisterRespawnableObject(this);

			_animator = GetNode<AnimationPlayer>(animator);
		}

		public override void _PhysicsProcess(float _)
		{
			if (!isActive)
				return;

			if(damageTimer != 0) //Update strafing, but only when not being damaged
				damageTimer = Mathf.MoveToward(damageTimer, 0, PhysicsManager.physicsDelta);
			else
				velocity.x = strafeSettings.Interpolate(velocity.x, Character.Controller.horizontalAxis.value);

			velocity.y = Mathf.MoveToward(velocity.y, moveSpeed, ACCELERATION * PhysicsManager.physicsDelta);
			Vector3 moveDirection = Character.PathFollower.Forward().RemoveVertical().Normalized();
			CheckWall();
			MoveAndSlide((this.Left() * velocity.x + moveDirection * velocity.y));

			Character.PathFollower.Resync();
			Character.UpdateExternalControl();
			GlobalRotation = Vector3.Up * (moveDirection.Flatten().AngleTo(Vector2.Up) + Mathf.Pi);
		}

		private const float SMOOTHING_DISTANCE = 2f;
		private const float COLLISION_RADIUS = .6f; //Larger than actual collision for smoother corners
		private void CheckWall() //Smooths out collision against walls
		{
			Vector3 castDirection = this.Left() * Mathf.Sign(velocity.x) * SMOOTHING_DISTANCE;
			RaycastHit h = this.CastRay(GlobalTranslation, castDirection, Character.environmentMask);
			Debug.DrawRay(GlobalTranslation, castDirection, h ? Colors.Red : Colors.White);
			if(h)
			{
				if (h.distance < COLLISION_RADIUS)
					velocity.x = 0f;
				else
				{
					float strafeClamp = strafeSettings.speed * ((h.distance - COLLISION_RADIUS) / (SMOOTHING_DISTANCE - COLLISION_RADIUS));
					velocity.x = Mathf.Clamp(velocity.x, -strafeClamp, strafeClamp);
				}
			}
		}

		public void OnPlayerEntered(Area _) //Start Boat
		{
			isActive = true;

			damageTimer = 0f;
			velocity = Vector2.Down * moveSpeed; //Acccelerate instantly

			Character.StartExternal(this, true);
			Character.DisableEnvironmentCollider();
			
			Character.Soul.IsSpeedBreakEnabled = false;
			Character.Camera.SetCameraData(cameraSettings);

			Character.Connect(nameof(CharacterController.Damaged), this, nameof(TakeDamage), null); //Connect damage signal
			Character.Connect(nameof(CharacterController.ExternalControlCompleted), this, nameof(Deactivate), null, (uint)ConnectFlags.Oneshot);
		}

		public void Respawn() //Called from "Despawn" animation
		{
			isActive = false;
			velocity = Vector2.Zero;
			GlobalTransform = startingTransform;
			//Play spawn animation
			_animator.Play("Spawn");
		}

		private void TakeDamage(Spatial n)
		{
			velocity.x = 0f;
			velocity.y = 2f; //kill speed
			damageTimer = DAMAGE_LOCKOUT_LENGTH;

			if (n.IsInGroup("damage boat")) //Fall off boat
			{
				Character.JumpTo(GlobalTranslation + (this.Forward() + this.Right()).Normalized(), 2f);
			}
		}

		private void Deactivate()
		{
			Character.EnableEnvironmentCollider();
			Character.Disconnect(nameof(CharacterController.Damaged), this, nameof(TakeDamage)); //Disconnect damage signal

			//Play despawn animation
			isActive = false;
			_animator.Play("Despawn");
		}
	}
}
