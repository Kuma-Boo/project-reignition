using Godot;
using Godot.Collections;
using Project.Core;

/// <summary>
/// Makes Erazor's personal hallway stretch out to infinity
/// </summary>
namespace Project.Gameplay.Bosses
{
	public partial class InfiniteHallway : Node3D
	{
		[Export]
		public NodePath hallRoot;
		private Node3D _hallRoot;
		[Export]
		public NodePath sky;
		private Node3D _sky;

		[Export]
		public NodePath reflection;
		private Node3D _reflection; //High quality reflection

		[Export]
		public NodePath itemBundle; //Respawn the same object multiple times since only one item bundle is ever present
		[Export]
		public Array<int> itemBundleLocations;
		private int itemBundleCounter;
		private Node3D _itemBundle;

		[Export]
		public NodePath collisionPiece;
		private Node3D _collisionPiece;
		[Export]
		public NodePath collisionPiece2;
		private Node3D _collisionPiece2;

		private CharacterController Character => CharacterController.instance;
		private const float COLLISION_PIECE_SPACING = -87f;
		private const float COLLISION_PIECE_ROTATION = 1;

		[Signal]
		public delegate void ResetHallEventHandler(); //Called when a duel attack ends. Resets positions and respawns objects.

		public override void _Ready()
		{
			_hallRoot = GetNode<Node3D>(hallRoot);
			_reflection = GetNode<Node3D>(reflection);

			_sky = GetNode<Node3D>(sky);

			_itemBundle = GetNode<Node3D>(itemBundle);
			_collisionPiece = GetNode<Node3D>(collisionPiece);
			_collisionPiece2 = GetNode<Node3D>(collisionPiece2);
		}

		public override void _PhysicsProcess(double _)
		{
			float extraRotation = Character.PathFollower.ProgressRatio * Mathf.Pi;
			_sky.Rotation = Vector3.Up * (Mathf.DegToRad(-65) + extraRotation);
			_reflection.GlobalPosition = Character.PathFollower.GlobalPosition;

			Debug.DrawRay(_reflection.GlobalPosition, _reflection.Up() * 10, Colors.Green);
			Debug.DrawRay(_reflection.GlobalPosition, Character.PathFollower.Forward() * 10, Colors.Blue);
			_reflection.Rotation = new Vector3(-Mathf.Pi * .5f, Character.PathFollower.Forward().SignedAngleTo(Vector3.Forward, Vector3.Down), 0);
		}

		public void ResetHallway()
		{
			_hallRoot.GlobalTransform = Transform3D.Identity; //Reset hallway
			_collisionPiece.GlobalTransform = Transform3D.Identity;
			_collisionPiece2.GlobalTransform = Transform3D.Identity;
			MovePiece(_collisionPiece2);

			//TODO Reset item bundle
		}

		//Advances the visuals of the hallway to create the illusion of infinity
		public void AdvanceHall()
		{
			itemBundleCounter--;

			if (itemBundleCounter <= 0)
			{
				//Item bundle object is configured to respawn whenever the visibility is changed
				_itemBundle.Visible = false;
				_itemBundle.Visible = true;

				itemBundleCounter = itemBundleLocations[Boss.instance.CurrentPattern];

				if (itemBundleCounter == 0) //Don't spawn item bundle anymore
				{
					_itemBundle.GlobalPosition = Vector3.Down * 100;
					return;
				}

				_itemBundle.GlobalTransform = _hallRoot.GlobalTransform;
				for (int i = 0; i < itemBundleCounter; i++)
					MovePiece(_itemBundle);
			}

			MovePiece(_hallRoot);
		}

		//Collision is advanced separately since teleporting a collision piece the player is standing on causing jittering for a single frame
		public void AdvanceCollision(bool teleportFirstPiece)
		{
			Node3D targetPiece = teleportFirstPiece ? _collisionPiece : _collisionPiece2;
			for (int i = 0; i < 2; i++) //Perform twice to skip over the current collision piece
				MovePiece(targetPiece);
		}

		private void MovePiece(Node3D piece)
		{
			piece.GlobalPosition += piece.Forward() * COLLISION_PIECE_SPACING;
			piece.GlobalRotate(Vector3.Up, Mathf.DegToRad(COLLISION_PIECE_ROTATION));
		}
	}
}

