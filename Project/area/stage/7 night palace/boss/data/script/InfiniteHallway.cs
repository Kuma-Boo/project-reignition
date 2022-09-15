using Godot;
using Godot.Collections;
using Project.Core;

/// <summary>
/// Makes Erazor's personal hallway stretch out to infinity
/// </summary>
namespace Project.Gameplay.Bosses
{
    public class InfiniteHallway : Spatial
	{
		[Export]
		public NodePath hallRoot;
		private Spatial _hallRoot;
		[Export]
		public NodePath sky;
		private Spatial _sky;

		[Export]
		public NodePath reflection;
		private Spatial _reflection; //High quality reflection

		[Export]
		public NodePath itemBundle; //Respawn the same object multiple times since only one item bundle is ever present
		[Export]
		public Array<int> itemBundleLocations;
		private int itemBundleCounter;
		private Spatial _itemBundle;

		[Export]
		public NodePath collisionPiece;
		private Spatial _collisionPiece;
		[Export]
		public NodePath collisionPiece2;
		private Spatial _collisionPiece2;

		private CharacterController Character => CharacterController.instance;
		private const float COLLISION_PIECE_SPACING = -87f;
		private const float COLLISION_PIECE_ROTATION = 1;

		[Signal]
		public delegate void ResetPosition(); //Called when a duel attack ends. Resets positions and respawns objects.

		public override void _Ready()
		{
			_hallRoot = GetNode<Spatial>(hallRoot);
			_reflection = GetNode<Spatial>(reflection);

			_sky = GetNode<Spatial>(sky);

			_itemBundle = GetNode<Spatial>(itemBundle);
			_collisionPiece = GetNode<Spatial>(collisionPiece);
			_collisionPiece2 = GetNode<Spatial>(collisionPiece2);
		}

		public override void _PhysicsProcess(float _)
		{
			float extraRotation = Character.PathFollower.UnitOffset * 180;
			_sky.RotationDegrees = Vector3.Up * (-65 + extraRotation);
			_reflection.GlobalTranslation = Character.PathFollower.GlobalTranslation;

			Debug.DrawRay(_reflection.GlobalTranslation, _reflection.Up() * 10, Colors.Green);
			Debug.DrawRay(_reflection.GlobalTranslation, Character.PathFollower.Forward() * 10, Colors.Blue);
			_reflection.Rotation = new Vector3(-Mathf.Pi * .5f, Character.PathFollower.Forward().SignedAngleTo(Vector3.Forward, Vector3.Down), 0);
		}

		public void ResetHallway()
		{
			_hallRoot.GlobalTransform = Transform.Identity; //Reset hallway
			_collisionPiece.GlobalTransform = Transform.Identity;
			_collisionPiece2.GlobalTransform = Transform.Identity;
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
					_itemBundle.GlobalTranslation = Vector3.Down * 100;
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
			Spatial targetPiece = teleportFirstPiece ? _collisionPiece : _collisionPiece2;
			for (int i = 0; i < 2; i++) //Perform twice to skip over the current collision piece
				MovePiece(targetPiece);
		}

		private void MovePiece(Spatial piece)
		{
			piece.GlobalTranslation += piece.Forward() * COLLISION_PIECE_SPACING;
			piece.GlobalRotate(Vector3.Up, Mathf.Deg2Rad(COLLISION_PIECE_ROTATION));
		}
	}
}

