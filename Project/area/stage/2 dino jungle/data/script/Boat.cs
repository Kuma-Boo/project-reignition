using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public partial class Boat : CharacterBody3D
	{
		[Export]
		private float speed; //How fast should the boat move?

		[Export]
		private Node3D passengerPosition;
		[Export]
		private RayCast3D frontOrientationChecker;
		[Export]
		private RayCast3D rearOrientationChecker;
		[Export]
		private AnimationPlayer animator;
		[Export]
		private CameraSettingsResource cameraSettings;
		[Export(PropertyHint.Layers3dPhysics)]
		private uint environmentMask;

		private bool isActive;
		private float moveSpeed;
		private float strafeSpeed;
		private float speedVelocity;
		private float strafeVelocity;
		private readonly float SPEED_TRACTION = .4f;
		private readonly float STRAFE_TRACTION = .2f;
		private readonly float STRAFE_SPEED = 14f;

		private float hitstunTimer;
		private readonly float HITSTUN_LENGTH = 2f;

		private Vector3 CastOrigin
		{
			get => GlobalPosition + Vector3.Up * 5.0f;
			set => GlobalPosition = value - Vector3.Up * 5.0f;
		}

		private SpawnData spawnData;
		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		public override void _Ready()
		{
			StageSettings.instance.ConnectRespawnSignal(this);
			spawnData = new SpawnData(GetParent(), Transform);
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isActive) return;

			UpdateStrafe();
			UpdatePosition();
		}

		private readonly float COLLISION_SIZE = .3f;
		private readonly float CAST_DISTANCE = 1.5f;
		private void UpdateStrafe()
		{
			strafeSpeed = ExtensionMethods.SmoothDamp(strafeSpeed, Controller.horizontalAxis.value * STRAFE_SPEED, ref strafeVelocity, STRAFE_TRACTION);

			CheckWall(1);
			CheckWall(-1);
		}

		private void CheckWall(int direction)
		{
			bool isActiveDirection = !Mathf.IsZeroApprox(strafeSpeed) && Mathf.Sign(direction) == Mathf.Sign(strafeSpeed);
			float distance = CAST_DISTANCE;
			if (isActiveDirection)
				distance += Mathf.Abs(strafeSpeed * PhysicsManager.physicsDelta);

			Vector3 castDirection = this.Left() * direction;
			Vector3 castVector = castDirection * distance;
			RaycastHit hit = this.CastRay(CastOrigin, castVector, environmentMask);
			Debug.DrawRay(CastOrigin, castVector, hit ? Colors.Red : Colors.White);

			if (hit)
			{
				if (hit.distance <= COLLISION_SIZE)
				{
					CastOrigin = hit.point - castDirection * COLLISION_SIZE;

					if (isActiveDirection)
						strafeSpeed = strafeVelocity = 0;
				}
				else if (hit.distance < CAST_DISTANCE)
				{
					if (isActiveDirection)
					{
						float clampFac = (hit.distance - COLLISION_SIZE) / (CAST_DISTANCE - COLLISION_SIZE);
						strafeSpeed *= clampFac;
						strafeVelocity *= clampFac;
					}
				}
			}
		}

		private void UpdatePosition()
		{
			if (hitstunTimer != 0)
			{
				hitstunTimer = Mathf.MoveToward(hitstunTimer, 0, PhysicsManager.physicsDelta);
				return;
			}
			else
				moveSpeed = ExtensionMethods.SmoothDamp(moveSpeed, speed, ref speedVelocity, SPEED_TRACTION);

			UpDirection = this.Up();
			Velocity = this.Forward() * moveSpeed + this.Left() * strafeSpeed + this.Down() * 5.0f;
			MoveAndSlide();

			frontOrientationChecker.ForceRaycastUpdate();
			rearOrientationChecker.ForceRaycastUpdate();

			float targetAngle = Character.PathFollower.ForwardAngle + Character.PathFollower.DeltaAngle;
			Transform3D t = GlobalTransform;
			t.basis.z = Vector3.Back;
			t.basis.y = (frontOrientationChecker.GetCollisionNormal() + rearOrientationChecker.GetCollisionNormal()) * .5f;
			t.basis.x = -t.basis.z.Cross(t.basis.y);
			t.basis = GlobalTransform.basis.Slerp(t.basis.Orthonormalized(), .1f);
			targetAngle -= CharacterController.CalculateForwardAngle(t.basis.z);
			t = t.RotatedLocal(Vector3.Up, targetAngle);
			GlobalTransform = t.Orthonormalized();

			Character.UpdateExternalControl();
			Character.PathFollower.Resync();
		}

		private void Despawn()
		{
			isActive = false;
			Character.ResetMovementState();
			animator.Play("despawn");
		}

		public void Respawn()
		{
			isActive = false;

			Transform = spawnData.spawnTransform;
			animator.Play("respawn");

			hitstunTimer = 0;
			moveSpeed = speedVelocity = 0;
		}

		public void PlayerEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			isActive = true;
			Character.StartExternal(passengerPosition); //Snap player into position
			CameraController.instance.SetCameraData(cameraSettings);
		}

		public void OnHurtboxEntered(Area3D a)
		{
			if (a.IsInGroup("stagger boat")) //Keep going
			{
				moveSpeed = speedVelocity = 0; //Kill speed
				hitstunTimer = HITSTUN_LENGTH;
				Character.TakeDamage(this);
			}
			else //Knock the player off and respawn
			{
				Despawn();
			}
		}
	}
}
