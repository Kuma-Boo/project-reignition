using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards
{
	[Tool]
	public partial class SpikeBall : Hazard
	{
		[Export]
		private float moveSpeed;
		[Export]
		public float distance; //How far to travel, or the radius of the circle
		[Export]
		public Vector3 movementAxis = new Vector3(0, 1, 0); //Movement axis, in LOCAL SPACE.
		[Export]
		public Vector3 rotationAxis = new Vector3(0, 1, 0); //Rotation axis in LOCAL SPACE. Used for circle movements.
		private bool isMovingForward;
		private int MovementDirection => (isMovingForward ? 1 : -1) * Mathf.Sign(moveSpeed);
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
		private NodePath root;
		private Node3D _root;

		private Vector3 spawnPosition;
		private const float ROTATION_SPEED = .1f;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;

			_root = GetNode<Node3D>(root);
			_root.Position = Vector3.Zero; //Reset editor debug translation
			spawnPosition = GlobalPosition;

			StageSettings.instance.ConnectRespawnSignal(this);
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

			currentOffset += Mathf.Abs(moveSpeed) * PhysicsManager.physicsDelta * MovementDirection;
			_root.RotateY(ROTATION_SPEED); //Constant rotation

			if (movementType == MovementType.Circle)
				currentOffset %= 1f;
			else
			{
				if (currentOffset > 1f)
				{
					currentOffset = 1f;
					isMovingForward = !isMovingForward;
				}
				else if (currentOffset < 0f)
				{
					currentOffset = 0f;
					isMovingForward = !isMovingForward;
				}
			}

			GlobalPosition = spawnPosition + GlobalTransform.basis * GetOffset();
			ProcessCollision();
		}

		private void Respawn()
		{
			currentOffset = startingOffset;
			_root.Rotation = Vector3.Zero; //Reset Rotation
		}

		private Vector3 GetOffset()
		{
			if (movementType == MovementType.Linear)
				return Vector3.Zero.Lerp(movementAxis * distance, Mathf.SmoothStep(0f, 1f, currentOffset));
			else if (movementType == MovementType.Circle)
			{
				if (movementAxis.Dot(rotationAxis) >= 1f)
				{
					GD.PrintErr("MovementAxis and RotationAxis cannot be the same.");
					return Vector3.Zero;
				}

				return movementAxis.Rotated(rotationAxis, Mathf.Lerp(0f, Mathf.Tau, currentOffset)).Normalized() * distance * MovementDirection;
			}

			return Vector3.Zero;
		}

		private void UpdateEditor()
		{
			_root = GetNode<Node3D>(root);
			if (_root == null) return;

			currentOffset = startingOffset;
			_root.GlobalPosition = GlobalPosition + GlobalTransform.basis * GetOffset();
		}
	}
}
