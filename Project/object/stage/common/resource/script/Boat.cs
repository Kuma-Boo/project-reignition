using Godot;
using Project.Core;

namespace Project.Gameplay
{
	public class Boat : KinematicBody
	{
		[Export]
		public NodePath overridePath;
		private Path _overridePath;
		private Transform startingTransform;

		[Export]
		public CameraSettingsResource cameraSettings;

		private bool isActive;
		[Export]
		public float moveSpeed = 15f;
		[Export]
		public float acceleration;
		private readonly float traction = 40f;
		private readonly float friction = 40f;
		private readonly float turnaround = 60f;
		private readonly float strafeSpeed = 12f;

		[Export]
		public NodePath animator;
		private AnimationPlayer _animator;

		private Vector2 velocity;
		private CharacterController Character => CharacterController.instance;

		public override void _Ready()
		{
			if(overridePath != null)
				_overridePath = GetNodeOrNull<Path>(overridePath);

			startingTransform = GlobalTransform;
			StageSettings.instance.RegisterRespawnableObject(this);

			_animator = GetNode<AnimationPlayer>(animator);
		}

		public override void _PhysicsProcess(float _)
		{
			if (!isActive)
				return;
			
			float inputDirection = Character.Controller.horizontalAxis.value;
			if (Mathf.IsZeroApprox(inputDirection))
				velocity.x = Mathf.MoveToward(velocity.x, 0, friction * PhysicsManager.physicsDelta);
			else if (Mathf.Sign(inputDirection) == Mathf.Sign(velocity.x) || Mathf.IsZeroApprox(velocity.x))
				velocity.x = Mathf.MoveToward(velocity.x, inputDirection * strafeSpeed, traction * PhysicsManager.physicsDelta);
			else
				velocity.x = Mathf.MoveToward(velocity.x, inputDirection * strafeSpeed, turnaround * PhysicsManager.physicsDelta);

			velocity.y = Mathf.MoveToward(velocity.y, moveSpeed, acceleration * PhysicsManager.physicsDelta);
			Vector3 moveDirection = Character.PathFollower.Forward().Flatten().Normalized();
			MoveAndSlide(this.Left() * velocity.x + moveDirection * velocity.y);

			Character.PathFollower.Resync();
			Character.UpdateExternalControl();
			GlobalRotation = Vector3.Up * (moveDirection.RemoveVertical().AngleTo(Vector2.Up) + Mathf.Pi);
		}

		public void OnPlayerEntered(Area a)
		{
			if (!a.IsInGroup("player")) return;

			isActive = true;
			Character.StartExternal(this, true);
			Character.Soul.IsSpeedBreakEnabled = false;

			if(_overridePath != null)
				Character.PathFollower.SetActivePath(_overridePath);

			Character.Connect(nameof(CharacterController.ExternalControlCompleted), this, nameof(Deactivate), null, (uint)ConnectFlags.Oneshot);
			Character.Camera.SetCameraData(cameraSettings);
		}

		public void Respawn() //Called from "Despawn" animation
		{
			isActive = false;
			velocity = Vector2.Zero;
			GlobalTransform = startingTransform;
			//Play spawn animation
			_animator.Play("Spawn");
		}

		private void Deactivate()
		{
			//Play despawn animation
			isActive = false;
			_animator.Play("Despawn");
		}
	}
}
