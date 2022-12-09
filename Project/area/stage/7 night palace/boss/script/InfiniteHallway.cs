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
		private Node3D reflection; //High quality reflection

		[Export]
		private Node3D itemBundle; //Respawn the same object multiple times since only one item bundle is ever present
		[Export]
		public Array<int> itemBundleLocations;
		private int itemBundleCounter;

		[Export]
		private Node3D primaryCollision;
		[Export]
		private Node3D secondaryCollision;

		private CharacterController Character => CharacterController.instance;
		private const float COLLISION_PIECE_SPACING = -87f;
		private const float COLLISION_PIECE_ROTATION = 1;

		[Signal]
		public delegate void ResetHallEventHandler(); //Called when a duel attack ends. Resets positions and respawns objects.

		public override void _PhysicsProcess(double _)
		{
			float extraRotation = Character.PathFollower.ProgressRatio * Mathf.Pi;
			sky.Rotation = Vector3.Up * (Mathf.DegToRad(-65) + extraRotation);
			reflection.GlobalPosition = Character.PathFollower.GlobalPosition;

			Debug.DrawRay(reflection.GlobalPosition, reflection.Up() * 10, Colors.Green);
			Debug.DrawRay(reflection.GlobalPosition, Character.PathFollower.Back() * 10, Colors.Blue);
			reflection.Rotation = new Vector3(-Mathf.Pi * .5f, Character.PathFollower.Back().SignedAngleTo(Vector3.Forward, Vector3.Down), 0);
		}

		public void ResetHallway()
		{
			hallRoot.GlobalTransform = Transform3D.Identity; //Reset hallway
			primaryCollision.GlobalTransform = Transform3D.Identity;
			secondaryCollision.GlobalTransform = Transform3D.Identity;
			MovePiece(secondaryCollision);

			//TODO Reset item bundle
		}

		//Advances the visuals of the hallway to create the illusion of infinity
		public void AdvanceHall()
		{
			itemBundleCounter--;

			if (itemBundleCounter <= 0)
			{
				//Item bundle object is configured to respawn whenever the visibility is changed
				itemBundle.Visible = false;
				itemBundle.Visible = true;

				itemBundleCounter = itemBundleLocations[Erazor.CurrentPattern];

				if (itemBundleCounter == 0) //Don't spawn item bundle anymore
				{
					itemBundle.GlobalPosition = Vector3.Down * 100;
					return;
				}

				itemBundle.GlobalTransform = hallRoot.GlobalTransform;
				for (int i = 0; i < itemBundleCounter; i++)
					MovePiece(itemBundle);
			}

			MovePiece(hallRoot);
		}

		//Collision is advanced separately since teleporting a collision piece the player is standing on causing jittering for a single frame
		public void AdvanceCollision(bool teleportFirstPiece)
		{
			Node3D targetPiece = teleportFirstPiece ? primaryCollision : secondaryCollision;
			for (int i = 0; i < 2; i++) //Perform twice to skip over the current collision piece
				MovePiece(targetPiece);
		}

		private void MovePiece(Node3D piece)
		{
			piece.GlobalPosition += piece.Back() * COLLISION_PIECE_SPACING;
			piece.GlobalRotate(Vector3.Up, Mathf.DegToRad(COLLISION_PIECE_ROTATION));
		}
	}
}

