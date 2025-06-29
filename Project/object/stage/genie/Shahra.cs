using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

public partial class Shahra : Node3D
{
	[Export]
	private PlayerPathController PathController { get; set; }

	[Export]
	private Node3D root;
	[Export]
	private Curve travelCurve;
	[Export]
	private Vector3 travelSpeed;
	[Export]
	private Vector3 travelMagnitude;
	private Vector3 curveSample;
	private ShahraState currentState;
	private enum ShahraState
	{
		Hidden,
		Active,
		Inactive,
	}
	private float scaleAmount;
	private readonly float ScaleSpeed = 1.0f;

	public override void _Ready()
	{
		SoundManager.instance.Connect(SoundManager.SignalName.ShahraSpeechStart, new(this, MethodName.StartShahra));
		SoundManager.instance.Connect(SoundManager.SignalName.ShahraSpeechEnd, new(this, MethodName.StopShahra));
	}

	public override void _PhysicsProcess(double _)
	{
		if (currentState == ShahraState.Hidden)
			return;

		UpdateState();
	}

	private void UpdateState()
	{
		Rotation = Vector3.Up * PathController.ForwardAngle;

		scaleAmount = Mathf.MoveToward(scaleAmount, currentState == ShahraState.Inactive ? 0 : 1, ScaleSpeed * PhysicsManager.physicsDelta);
		float interpolationAmount = Mathf.SmoothStep(0, 1, scaleAmount);
		Scale = Vector3.One * Mathf.Max(0.01f, interpolationAmount);

		Visible = !Mathf.IsZeroApprox(scaleAmount);
		if (!Visible && currentState == ShahraState.Inactive) // Switch to inactive
		{
			currentState = ShahraState.Hidden;
			return;
		}

		// Update movement
		curveSample += travelSpeed * PhysicsManager.physicsDelta;
		curveSample.X %= 1.0f;
		curveSample.Y %= 1.0f;
		curveSample.Z %= 1.0f;
		Vector3 targetPosition = new
		(
			travelCurve.Sample(curveSample.X),
			travelCurve.Sample(curveSample.Y),
			travelCurve.Sample(curveSample.Z)
		);

		targetPosition *= travelMagnitude;
		root.Position = targetPosition + (Vector3.Up * .5f);
	}

	private void StartShahra()
	{
		// TODO allow different speeds
		currentState = ShahraState.Active;
	}

	private void StopShahra()
	{
		currentState = ShahraState.Inactive;
	}
}
