using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Controls the carpet found inside of Night Palace.
	/// </summary>
	public partial class Carpet : Node3D
	{
		[ExportGroup("Settings")]
		[Export]
		/// <summary> How fast to move. </summary>
		private float maxSpeed;
		[Export]
		/// <summary> How fast to turn. </summary>
		private float turnSpeed;

		[Export]
		/// <summary> Maximum distance from the path allowed. </summary>
		private Vector2 bounds;
		private float HorizontalTurnSmoothing => bounds.X * TURN_SMOOTHING_RATIO;
		private float VerticalTurnSmoothing => bounds.Y * TURN_SMOOTHING_RATIO;
		/// <summary> At what ratio should inputs start being smoothed? </summary>
		private readonly float TURN_SMOOTHING_RATIO = .8f;


		/// <summary> How fast the carpet is currently moving? </summary>
		private float speedDelta;
		private Vector2 turnDelta;
		// Values for smooth damp
		private float speedVelocity;
		private Vector2 turnVelocity;
		private readonly float SPEED_SMOOTHING = .5f;
		private readonly float TURN_SMOOTHING = .25f;


		[ExportGroup("Components")]
		[Export]
		/// <summary> Reference to the Carpet's travel path. </summary>
		private Path3D path;
		[Export]
		/// <summary> Reference to the Carpet's pathfollower. </summary>
		private PathFollow3D pathFollower;
		[Export]
		/// <summary> Reference to the Carpet's root. </summary>
		private Node3D root;
		[Export]
		/// <summary> Reference to the Carpet's animator. </summary>
		private AnimationPlayer animator;


		/// <summary> Is the carpet currently active? </summary>
		private bool isActive;
		private float startingProgress;
		private SpawnData spawnData;


		private CharacterController Character => CharacterController.instance;


		public override void _Ready()
		{
			pathFollower.Progress = path.Curve.GetClosestOffset(GlobalPosition - path.GlobalPosition);
			startingProgress = pathFollower.Progress;
			spawnData = new SpawnData(GetParent(), Transform); // Create spawn data

			LevelSettings.instance.ConnectRespawnSignal(this);
		}

		public override void _PhysicsProcess(double _)
		{
			if (!isActive) return;

			// Process carpet movement
			Vector2 inputVector = Character.InputVector * turnSpeed;

			// Smooth out edges
			bool isSmoothingHorizontal = Mathf.Abs(pathFollower.HOffset) > HorizontalTurnSmoothing &&
				Mathf.Sign(inputVector.X) != Mathf.Sign(pathFollower.HOffset);
			bool isSmoothingVertical = Mathf.Abs(pathFollower.VOffset) > VerticalTurnSmoothing &&
				Mathf.Sign(inputVector.Y) != Mathf.Sign(pathFollower.VOffset);
			if (isSmoothingHorizontal)
				inputVector.X *= 1.0f - (Mathf.Abs(pathFollower.HOffset) - HorizontalTurnSmoothing) / (bounds.X - HorizontalTurnSmoothing);

			if (isSmoothingVertical)
				inputVector.Y *= 1.0f - (Mathf.Abs(pathFollower.VOffset) - VerticalTurnSmoothing) / (bounds.Y - VerticalTurnSmoothing);

			speedDelta = ExtensionMethods.SmoothDamp(speedDelta, maxSpeed, ref speedVelocity, SPEED_SMOOTHING);
			turnDelta = ExtensionMethods.SmoothDamp(turnDelta, inputVector, ref turnVelocity, TURN_SMOOTHING);


			pathFollower.Progress += speedDelta * PhysicsManager.physicsDelta;

			// Add turning offset
			pathFollower.HOffset -= turnDelta.X * PhysicsManager.physicsDelta;
			pathFollower.VOffset -= turnDelta.Y * PhysicsManager.physicsDelta;
			pathFollower.HOffset = Mathf.Clamp(pathFollower.HOffset, -bounds.X, bounds.X);
			pathFollower.VOffset = Mathf.Clamp(pathFollower.VOffset, -bounds.Y, bounds.Y);

			//Update animations
			root.Rotation = Vector3.Zero;
			root.RotateX(Mathf.Pi * .25f * (turnDelta.Y / turnSpeed));
			root.RotateZ(Mathf.Pi * .25f * (turnDelta.X / turnSpeed));
			animator.SpeedScale = 1.0f + (speedDelta / maxSpeed) * 1.5f;
			Character.Animator.UpdateBalancing();

			GlobalTransform = pathFollower.GlobalTransform;
			Character.UpdateExternalControl();
		}


		public void Respawn()
		{
			Deactivate();

			spawnData.Respawn(this);
			pathFollower.Progress = startingProgress;
			pathFollower.HOffset = pathFollower.VOffset = 0;

			root.Transform = Transform3D.Identity;
			animator.SpeedScale = 1.0f; // Reset speed scale
		}


		/// <summary> Call this from a trigger. </summary>
		public void Activate()
		{
			isActive = true;

			Character.StartExternal(this, root);
			Character.Animator.StartBalancing(); // Carpet uses balancing animations
			Character.Animator.UpdateBalanceSpeed(1.0f);
			Character.Animator.ExternalAngle = 0;
		}


		public void Deactivate()
		{
			isActive = false;
			// Reset damping values
			speedDelta = speedVelocity = 0;
			turnDelta = turnVelocity = Vector2.Zero;

			if (Character.ExternalParent == this)
				Character.StopExternal();
			Character.Animator.ResetState();
		}
	}
}
