using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	public partial class Boat : Node3D
	{
		[Export]
		public float speed; //How fast should the boat move?

		[Export]
		public NodePath passengerPosition;
		private Node3D _passengerPosition;
		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;
		[Export]
		public CameraSettingsResource cameraSettings;

		private bool isActive;
		private float moveSpeed;
		private float strafeSpeed;
		private float speedVelocity;
		private float strafeVelocity;
		private readonly float SPEED_TRACTION = .4f;
		private readonly float STRAFE_TRACTION = .2f;
		private readonly float STRAFE_SPEED = 10f;

		private float hitstunTimer;
		private readonly float HITSTUN_LENGTH = 2f;

		private StageSettings.SpawnData spawnData;
		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		public override void _Ready()
		{
			_passengerPosition = GetNode<Node3D>(passengerPosition);
			_animator = GetNode<AnimationPlayer>(animator);

			StageSettings.instance.RegisterRespawnableObject(this);
			spawnData = new StageSettings.SpawnData(GetParent(), Transform);
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isActive) return;

			UpdateStrafe();
			UpdatePosition();
		}

		private void UpdateStrafe()
		{
			strafeSpeed = ExtensionMethods.SmoothDamp(strafeSpeed, Controller.horizontalAxis.value * STRAFE_SPEED, ref strafeVelocity, STRAFE_TRACTION);
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

			Debug.DrawRay(GlobalPosition, this.Right() * 10, Colors.Red);
			GlobalPosition += this.Forward() * moveSpeed * PhysicsManager.physicsDelta;
			GlobalPosition += this.Left() * strafeSpeed * PhysicsManager.physicsDelta;
			GlobalRotation = Vector3.Up * Character.PathFollower.ForwardAngle;

			Character.UpdateExternalControl();
			Character.PathFollower.Resync();
		}

		private void Despawn()
		{
			isActive = false;
			_animator.Play("despawn");
		}

		public void Respawn()
		{
			isActive = false;

			Transform = spawnData.spawnTransform;
			_animator.Play("respawn");

			hitstunTimer = 0;
			moveSpeed = speedVelocity = 0;
		}

		public void PlayerEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			isActive = true;
			Character.StartExternal(_passengerPosition, true); //Snap player into position
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
