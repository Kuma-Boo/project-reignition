using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards
{
	/// <summary>
	/// Sickle object found in the skeleton dome. This script handles length adjustments.
	/// </summary>
	[Tool]
	public partial class Sickle : Node3D
	{
		[ExportGroup("Swing Settings")]
		[Export(PropertyHint.Range, "0,90,5")]
		private float rotationAmount;
		[Export(PropertyHint.Range, "0,10,.1")]
		private float cycleLength = 2;
		[Export(PropertyHint.Range, "0,1,.1")]
		private float currentRatio = .5f;
		[Export]
		private bool isSwingingRight;

		[ExportGroup("Editor")]
		[Export]
		private NodePath root;
		private Node3D rootNode;
		[Export]
		private NodePath head;
		private Node3D headNode;
		[Export]
		private GpuParticles3D sparkParticles;
		[Export]
		private PackedScene chainScene;
		[Export(PropertyHint.Range, "0,32")]
		private int chainLength;
		[Export]
		/// <summary> Set this to True to force the editor to update. </summary>
		private bool update;

		/// <summary> Length of each independant chain segment. </summary>
		private const float CHAIN_SEGMENT_LENGTH = 1.2f;
		private const float CHAIN_BASE_OFFSET = 1.3f;

		//Set up length when loading into the scene
		public override void _Ready() => UpdateLength();

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint())
			{
				if (update)
					UpdateLength();
			}
			else
			{
				float targetRatio = isSwingingRight ? 1 : 0;
				currentRatio = Mathf.MoveToward(currentRatio, targetRatio, (1.0f / cycleLength) * PhysicsManager.physicsDelta);

				if (Mathf.IsEqualApprox(currentRatio, targetRatio))
					isSwingingRight = !isSwingingRight;

				sparkParticles.Emitting = Mathf.Abs(rootNode.Rotation.Z) < Mathf.Pi * .2f;
			}

			float interpolatedRatio = (Mathf.SmoothStep(0, 1, Mathf.Abs(currentRatio)) * 2) - 1;
			rootNode.Rotation = Vector3.Back * Mathf.DegToRad(rotationAmount) * interpolatedRatio;
		}

		private void UpdateLength()
		{
			update = false;

			rootNode = GetNodeOrNull<Node3D>(root);
			headNode = GetNodeOrNull<Node3D>(head);
			if (headNode == null || rootNode == null)
			{
				GD.PrintErr("Sickle Nodes are Null.");
				return;
			}

			//Clear children
			foreach (Node child in rootNode.GetChildren())
			{
				if (child == headNode) continue;
				child.QueueFree();
			}

			//Add chain segments
			for (int i = 0; i < chainLength; i++)
			{
				Node3D chainNode = chainScene.Instantiate<Node3D>();
				rootNode.AddChild(chainNode);
				chainNode.Position = Vector3.Down * (i * CHAIN_SEGMENT_LENGTH);
			}

			headNode.Position = Vector3.Down * (chainLength * CHAIN_SEGMENT_LENGTH + CHAIN_BASE_OFFSET);
		}
	}
}
