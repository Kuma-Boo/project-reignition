using Godot;
using Project.Core;
using Project.Interface;
using Project.CustomNodes;

namespace Project.Gameplay.Objects;

public partial class Uhu : PathFollow3D
{
	[Export]
	private AnimationPlayer animator;
	[Export]
	private Node3D root;
	[Export]
	private Trail3D trail;
	private StageSettings Stage => StageSettings.instance;

	private Vector2 targetPosition;
	private Vector2 currentPosition;
	private Vector2 positionVelocity;
	private readonly float PositionSmoothing = 50f;
	private readonly float PositionDeadzone = .5f;
	private readonly float IdlePositionRadius = 2f;

	public override void _Ready() => Countdown.Instance.Connect(Countdown.SignalName.CountdownFinished, new(this, MethodName.StartRace));

	public override void _PhysicsProcess(double _)
	{
		if ((currentPosition - targetPosition).LengthSquared() < PositionDeadzone)
			CalculateNewPosition();

		currentPosition = currentPosition.SmoothDamp(targetPosition, ref positionVelocity, PositionSmoothing * PhysicsManager.physicsDelta);
		root.Position = new(currentPosition.X, currentPosition.Y, 0);
	}

	private void CalculateNewPosition()
	{
		if (Stage.IsRaceActive)
		{
			targetPosition = Vector2.Zero;
			return;
		}

		targetPosition = Vector2.Up.Rotated(Mathf.Tau * Runtime.randomNumberGenerator.Randf());
		targetPosition *= Runtime.randomNumberGenerator.RandfRange(0, IdlePositionRadius);
	}

	private void StartRace()
	{
		Stage.IsRaceActive = true;
		animator.Play("race");
		trail.IsEmitting = true;
	}

	/// <summary> Call this from an animator when the race is finished. </summary>
	public void FinishRace()
	{
		GD.PrintT("Race finished.");
		Stage.IsRaceActive = false;
		trail.IsEmitting = false;
	}
}
