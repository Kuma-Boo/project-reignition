using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards
{
	[Tool]
	public partial class SpikeBall : Hazard
	{
		[Export]
		public float moveSpeed;
		[Export]
		public float distance; //How far to travel, or the radius of the circle
		[Export]
		public bool isMovingRight; //Start by moving right?
		public int MovementDirection => isMovingRight ? 1 : -1;
		[Export(PropertyHint.Range, "0, 1")]
		public float startingOffset;
		private float currentOffset;
		[Export]
		public MovementType movementType;
		public enum MovementType
		{
			Static, //Don't move
			Linear, //Move back and forth linearly
			Circle //Circle around a point
		}

		[Export]
		public Node3D root;

		private Vector3 spawnPosition;
		private const float ROTATION_SPEED = .1f;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			root.Position = Vector3.Zero; //Reset editor debug translation
			spawnPosition = GlobalPosition;

			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
			{
				UpdateEditor();
				return;
			}

			if (movementType == MovementType.Static) return;

			float speed = moveSpeed / distance;

			currentOffset += speed * PhysicsManager.physicsDelta * MovementDirection;
			root.RotateY(ROTATION_SPEED); //Constant rotation

			if (movementType == MovementType.Circle)
				currentOffset %= 1f;
			else
			{
				if (currentOffset > 1f)
				{
					currentOffset = 1f;
					isMovingRight = !isMovingRight;
				}
				else if (currentOffset < 0f)
				{
					currentOffset = 0f;
					isMovingRight = !isMovingRight;
				}
			}

			GlobalPosition = spawnPosition + GetOffset();
			ProcessCollision();
		}

		private void Respawn()
		{
			currentOffset = startingOffset;
			root.Rotation = Vector3.Zero; //Reset Rotation
		}

		private Vector3 GetOffset()
		{
			if (movementType == MovementType.Linear)
				return Vector3.Zero.Lerp(this.Right() * distance, Mathf.SmoothStep(0f, 1f, currentOffset));
			else if (movementType == MovementType.Circle)
				return this.Back().Rotated(this.Up(), Mathf.Lerp(0f, Mathf.Tau, currentOffset)).Normalized() * distance * MovementDirection;

			return Vector3.Zero;
		}

		private void UpdateEditor()
		{
			if (root == null) return;

			currentOffset = startingOffset;
			root.GlobalPosition = GlobalPosition + GetOffset();
		}
	}
}
