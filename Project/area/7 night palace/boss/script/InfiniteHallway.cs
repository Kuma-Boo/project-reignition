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
		private Node3D hallRoot;
		[Export]
		private Node3D sky;

		[Export]
		private Node3D itemBundle; //Respawn the same object multiple times since only one item bundle is ever present
		[Export]
		public Array<int> itemBundleLocations;
		private int itemBundleCounter;

		[Export]
		private Node3D primaryCollision;
		[Export]
		private Node3D secondaryCollision;

		private PlayerController Player => StageSettings.Player;
		private const float COLLISION_PIECE_SPACING = -87f;
		private const float COLLISION_PIECE_ROTATION = 1;

		[Signal]
		public delegate void ResetHallEventHandler(); //Called when a duel attack ends. Resets positions and respawns objects.
		[Signal]
		public delegate void RespawnItemBundleEventHandler(); //Called when item bundle is respawned.

		public override void _PhysicsProcess(double _)
		{
			float extraRotation = Player.PathFollower.ProgressRatio * Mathf.Pi;
			sky.Rotation = Vector3.Up * (Mathf.DegToRad(-65) + extraRotation);
		}

		public void ResetHallway()
		{
			hallRoot.GlobalTransform = Transform3D.Identity; //Reset hallway
			primaryCollision.GlobalTransform = Transform3D.Identity;
			secondaryCollision.GlobalTransform = Transform3D.Identity;
			MoveNode(secondaryCollision);

			//TODO Reset item bundle
		}

		/// <summary>
		/// Called from a signal. 
		/// Advances the visuals of the hallway to create the illusion of infinity.
		/// </summary>
		public void AdvanceHall()
		{
			itemBundleCounter--;
			if (itemBundleCounter <= 0)
			{
				itemBundleCounter = itemBundleLocations[Erazor.CurrentPattern];

				if (itemBundleCounter == 0) //Don't spawn item bundle anymore
				{
					itemBundle.GlobalPosition = Vector3.Down * 100;
					return;
				}

				EmitSignal(SignalName.RespawnItemBundle);

				itemBundle.GlobalTransform = hallRoot.GlobalTransform;
				for (int i = 0; i < itemBundleCounter; i++) //Move item bundle the correct distance away
					MoveNode(itemBundle); //Each iteration moves the item bundle one chunk
			}

			MoveNode(hallRoot);
		}

		/// <summary>
		/// Called from a signal. 
		/// Collision only advances after player stops colliding with it to avoid jittering.
		/// </summary>
		public void AdvanceCollision(bool isPrimaryPiece)
		{
			Node3D targetPiece = isPrimaryPiece ? primaryCollision : secondaryCollision;
			for (int i = 0; i < 2; i++) //Perform twice to skip over the current collision piece
				MoveNode(targetPiece);
		}

		/// <summary>
		/// Moves the given node one chunk forward.
		/// </summary>
		private void MoveNode(Node3D node)
		{
			node.GlobalPosition += node.Forward() * COLLISION_PIECE_SPACING;
			node.Rotation += Vector3.Up * Mathf.DegToRad(COLLISION_PIECE_ROTATION);
		}
	}
}

