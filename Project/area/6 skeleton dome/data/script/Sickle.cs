using Godot;
using Project.Core;

namespace Project.Gameplay.Hazards;

/// <summary>
/// Sickle object found in the skeleton dome. This script handles length adjustments.
/// </summary>
[Tool]
public partial class Sickle : Node3D
{
	[ExportGroup("Swing Settings")]
	[Export(PropertyHint.Range, "0,90,5")] private float rotationAmount;
	[Export(PropertyHint.Range, "0,10,.1")] private float cycleLength = 2;
	[Export(PropertyHint.Range, "0,1,.1")] private float currentRatio = .5f;
	[Export] private bool isSwingingRight;
	[Export] private bool enableSparkParticles = true;

	[ExportGroup("Editor")]
	[ExportToolButton("Regenerate Sickle")]
	public Callable GenerateSickleGroup => Callable.From(GenerateSickle);
	[Export] private NodePath root;
	private Node3D rootNode;
	[Export] private NodePath head;
	private Node3D headNode;
	[Export] private GpuParticles3D sparkParticles;
	[Export] private AudioStreamPlayer3D directionChangeSfx;
	[Export] private AudioStreamPlayer3D lowPointSfx;
	/// <summary> Were sparks emitted this rotation? </summary>
	private bool emittedSparks;
	[Export] private PackedScene chainScene;
	[Export(PropertyHint.Range, "0,32")] private int chainLength;

	/// <summary> Length of each independant chain segment. </summary>
	private readonly float ChainSegmentLength = 1.2f;
	private readonly float ChainBaseOffset = 1.3f;

	// Set up length when loading into the scene
	public override void _Ready() => GenerateSickle();

	private void GenerateSickle()
	{
		UpdateLength();
		UpdateHeadPosition();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Engine.IsEditorHint())
		{
			UpdateHeadPosition();
			return;
		}

		float targetRatio = isSwingingRight ? 1 : 0;
		currentRatio = Mathf.MoveToward(currentRatio, targetRatio, (2.0f / cycleLength) * PhysicsManager.physicsDelta);

		if (Mathf.IsEqualApprox(currentRatio, targetRatio))
		{
			emittedSparks = false;
			isSwingingRight = !isSwingingRight;
			directionChangeSfx.Play();
		}

		if (!emittedSparks)
		{
			if (Mathf.Abs(rootNode.Rotation.Z) < Mathf.Pi * .05f &&
			Mathf.Sign(targetRatio - .5f) != Mathf.Sign(currentRatio - .5f))
			{
				if (enableSparkParticles)
				{
					sparkParticles.Rotation = Vector3.Up * Mathf.Pi * targetRatio;
					sparkParticles.Restart();
				}

				lowPointSfx.Play();
				emittedSparks = true;
			}
		}

		UpdateHeadPosition();
	}

	private void UpdateHeadPosition()
	{
		float interpolatedRatio = (Mathf.SmoothStep(0, 1, Mathf.Abs(currentRatio)) * 2) - 1;
		rootNode.Rotation = Vector3.Back * Mathf.DegToRad(rotationAmount) * interpolatedRatio;
	}

	private void UpdateLength()
	{
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
			chainNode.Position = Vector3.Down * (i * ChainSegmentLength);
		}

		headNode.Position = Vector3.Down * ((chainLength * ChainSegmentLength) + ChainBaseOffset);
	}
}