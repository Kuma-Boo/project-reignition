using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards
{
	[Tool]
	public class SpikeBall : Hazard
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
		public NodePath mesh;
		public Spatial _mesh;

		private Vector3 spawnPosition;
		private const float ROTATION_SPEED = .1f;

		public override void _Ready()
		{
			if (Engine.EditorHint) return;

			_mesh = GetNode<Spatial>(mesh);
			_mesh.Translation = Vector3.Zero; //Reset editor debug translation

			spawnPosition = GlobalTranslation;

			StageSettings.instance.RegisterRespawnableObject(this);
			Respawn();
		}

		public override void _PhysicsProcess(float _)
		{
			if (Engine.EditorHint)
			{
				UpdateEditor();
				return;
			}

			if (movementType == MovementType.Static) return;

			float speed = moveSpeed / distance;

			currentOffset += speed * PhysicsManager.physicsDelta * MovementDirection;
			_mesh.RotateY(ROTATION_SPEED); //Constant rotation

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

			GlobalTranslation = spawnPosition + GetOffset();
		}

		private void Respawn()
		{
			currentOffset = startingOffset;
			_mesh.Rotation = Vector3.Zero; //Reset Rotation
		}

		private Vector3 GetOffset()
		{
			if(movementType == MovementType.Linear)
				return Vector3.Zero.LinearInterpolate(this.Right() * distance, Mathf.SmoothStep(0f, 1f, currentOffset));
			else if (movementType == MovementType.Circle)
				return this.Forward().Rotated(this.Up(), Mathf.Lerp(0f, Mathf.Tau, currentOffset)).Normalized() * distance * MovementDirection;

			return Vector3.Zero;
		}

		private void UpdateEditor()
		{
			if(_mesh == null)
			{
				if (mesh == null) return;

				_mesh = GetNode<Spatial>(mesh);
			}

			currentOffset = startingOffset;
			_mesh.GlobalTranslation = GlobalTranslation + GetOffset();
		}
	}
}
