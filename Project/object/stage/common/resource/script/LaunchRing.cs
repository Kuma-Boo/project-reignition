using Godot;
using Project.Core;

namespace Project.Gameplay.Objects
{
	[Tool]
	public partial class LaunchRing : Area3D
	{
		[Export]
		public float closeDistance;
		[Export]
		public float closeMidHeight;
		[Export]
		public float closeEndHeight;
		[Export]
		public float farDistance;
		[Export]
		public float farMidHeight;
		[Export]
		public float farEndHeight;
		[Export(PropertyHint.Range, "0, 1")]
		public float launchPower;
		public LaunchData GetLaunchData()
		{
			float midHeight = Mathf.Lerp(closeMidHeight, farMidHeight, launchPower);
			float endHeight = Mathf.Lerp(closeEndHeight, farEndHeight, launchPower);
			float distance = Mathf.Lerp(closeDistance, farDistance, launchPower);
			Vector3 endPoint = GlobalPosition + (this.Forward() * distance + Vector3.Up * endHeight);
			return LaunchData.Create(GlobalPosition, endPoint, midHeight);
		}

		[Export]
		private NodePath[] pieces;
		private Node3D[] _pieces;
		private readonly int PIECE_COUNT = 16;
		private readonly float RING_SIZE = 2.2f;

		[Export]
		private AnimationPlayer animator;
		private bool isActive;

		private CharacterController Character => CharacterController.instance;
		private InputManager.Controller Controller => InputManager.controller;

		public override void _Ready()
		{
			if (Engine.IsEditorHint()) return;
			InitializePieces();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
				InitializePieces();

			UpdatePieces();

			if (Engine.IsEditorHint()) return;

			if (isActive)
			{
				if (Controller.jumpButton.wasPressed) //Disable launcher
					DropPlayer();
				else if (Controller.actionButton.wasPressed)
				{
					Character.StartLauncher(GetLaunchData());
					Character.CanJumpDash = true;
				}
			}
		}

		private void DropPlayer()
		{
			isActive = false;
			Character.ResetMovementState();
		}

		private void InitializePieces()
		{
			_pieces = new Node3D[pieces.Length];

			for (int i = 0; i < pieces.Length; i++)
			{
				_pieces[i] = GetNodeOrNull<Node3D>(pieces[i]);
			}
		}

		private void UpdatePieces()
		{
			if (_pieces.Length == 0) return;

			float interval = Mathf.Tau / PIECE_COUNT;
			for (int i = 0; i < _pieces.Length; i++)
			{
				if (_pieces[i] == null) continue;

				Vector3 movementVector = -Vector3.Up.Rotated(Vector3.Forward, interval * (i + .5f)); //Offset rotation slightly, since visual model is offset
				_pieces[i].Position = movementVector * launchPower * RING_SIZE;
			}
		}

		private void OnEntered(Area3D a)
		{
			animator.Play("charge");
			Character.StartExternal();
		}

		public void DamagePlayer()
		{
			DropPlayer();
			Character.TakeDamage(this);
		}
	}
}
