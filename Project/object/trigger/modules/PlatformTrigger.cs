using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers
{
	/// <summary>
	/// Moves player with moving platforms.
	/// </summary>
	public partial class PlatformTrigger : Node3D
	{
		[Signal]
		public delegate void PlatformInteractedEventHandler();

		[ExportSubgroup("Falling Platform Settings")]
		[Export]
		/// <summary> How long to shake before falling. </summary>
		private float shakeLength;
		/// <summary> Timer to keep track of shaking status. </summary>
		private float shakeTimer;
		/// <summary> Is the platform about to fall? </summary>
		private bool isPlatformShaking;
		private bool IsFallingBehaviourEnabled => fallingPlatformAnimator != null;

		[ExportSubgroup("Components")]
		[Export]
		/// <summary> Assign this to enable moving the player with the platform. </summary>
		private Node3D floorCalculationRoot;
		[Export]
		/// <summary> Reference to the "floor" collider. </summary>
		private PhysicsBody3D parentCollider;
		[Export]
		/// <summary> Assign this to enable falling platform behaviour. </summary>
		private AnimationPlayer fallingPlatformAnimator;
		private CharacterController Character => CharacterController.instance;

		private bool isActive;
		private bool isInteractingWithPlayer;


		public override void _Ready()
		{
			if (IsFallingBehaviourEnabled) // Falling behaviour is enabled, connect signal.
				Connect(SignalName.PlatformInteracted, new Callable(this, MethodName.StartShaking));
		}


		public override void _PhysicsProcess(double _)
		{
			if (isPlatformShaking)
				UpdateFallingPlatformBehaviour();

			if (!isInteractingWithPlayer) return;

			if (!isActive && Character.IsOnGround)
			{
				isActive = true;
				EmitSignal(SignalName.PlatformInteracted);
			}

			if (!isActive) return;

			if (floorCalculationRoot != null)
				UpdatePlayerMovement();
		}


		private void StartShaking()
		{
			if (Mathf.IsZeroApprox(shakeLength)) // Fall immediately
			{
				StartFalling();
				return;
			}

			isPlatformShaking = true;
			fallingPlatformAnimator.Play("shake");
		}
		private void StartFalling()
		{
			isPlatformShaking = false; // Stop shaking
			fallingPlatformAnimator.Play("fall", .2);
		}

		private void UpdateFallingPlatformBehaviour()
		{
			shakeTimer += PhysicsManager.physicsDelta;

			if (shakeTimer > shakeLength)
			{
				shakeTimer = 0;
				StartFalling();
			}
		}


		/// <summary> Moves the player with the platform. </summary>
		private void UpdatePlayerMovement()
		{
			if (!Character.IsOnGround) return; // Player isn't on the ground

			float checkLength = Mathf.Abs(Character.GlobalPosition.Y - floorCalculationRoot.GlobalPosition.Y) + Character.CollisionRadius * 2.0f;
			KinematicCollision3D collision = Character.MoveAndCollide(Vector3.Down * checkLength, true);
			if (collision == null || (Node3D)collision.GetCollider() != parentCollider) // Player is not on the platform
				return;

			Character.GlobalTranslate(Vector3.Up * (floorCalculationRoot.GlobalPosition.Y - Character.GlobalPosition.Y));
		}


		public void OnEntered(Area3D a)
		{
			if (!a.IsInGroup("player")) return;
			isInteractingWithPlayer = true;
		}


		public void OnExited(Area3D a)
		{
			if (!a.IsInGroup("player")) return;

			isInteractingWithPlayer = false;
			isActive = false;
		}
	}
}
