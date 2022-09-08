using Godot;
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
		public PackedScene itemBundle; //Respawn the same object multiple times since only one item bundle is ever present
		private Spatial _itemBundle;

		[Export]
		public NodePath collisionPiece;
		private Spatial _collisionPiece;
		[Export]
		public NodePath collisionPiece2;
		private Spatial _collisionPiece2;
		private const float COLLISION_PIECE_SPACING = -87f;
		private const float COLLISION_PIECE_ROTATION = 1;

		[Signal]
		public delegate void ResetPosition(); //Called when a duel attack ends. Resets positions and respawns objects.

		public override void _Ready()
		{
			_hallRoot = GetNode<Spatial>(hallRoot);

			_sky = GetNode<Spatial>(sky);

			_itemBundle = itemBundle.Instance<Spatial>();
			_collisionPiece = GetNode<Spatial>(collisionPiece);
			_collisionPiece2 = GetNode<Spatial>(collisionPiece2);
		}

		public override void _PhysicsProcess(float _)
		{
			float extraRotation = CharacterController.instance.PathFollower.UnitOffset * 180;
			_sky.RotationDegrees = Vector3.Up * (-65 + extraRotation);
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
		public void AdvanceHall() => MovePiece(_hallRoot);

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

