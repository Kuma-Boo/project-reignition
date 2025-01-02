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
	/// <summary> How long the race path is -- used to calculate the race positions. </summary>
	[Export]
	private float maxProgress;
	private float startingProgress;
	private Path3D path;
	[Export]
	private Trail3D trail;
	private StageSettings Stage => StageSettings.Instance;

	private Vector2 targetPosition;
	private Vector2 currentPosition;
	private Vector2 positionVelocity;
	private readonly float PositionSmoothing = 50f;
	private readonly float PositionDeadzone = .5f;
	private readonly float IdlePositionRadius = 2f;

	public override void _Ready()
	{
		startingProgress = Progress;
		path = GetParent<Path3D>();
		Countdown.Instance.Connect(Countdown.SignalName.CountdownFinished, new(this, MethodName.StartRace));
	}

	public override void _PhysicsProcess(double _)
	{
		if ((currentPosition - targetPosition).LengthSquared() < PositionDeadzone)
			CalculateNewPosition();

		currentPosition = currentPosition.SmoothDamp(targetPosition, ref positionVelocity, PositionSmoothing * PhysicsManager.physicsDelta);
		root.Position = new(currentPosition.X, currentPosition.Y, 0);

		UpdateRubberBanding();
		UpdateRacePositions();
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

	private float currentRubberBandRatio;
	private float rubberBandVelocity;
	private readonly float MaxRubberBandDistance = 30.0f;
	private readonly float RubberBandFactor = 1f;
	private readonly float RubberBandDeadZone = 10.0f;
	private readonly float RubberBandEndDeadZone = 20.0f;
	private readonly float RubberBandSmoothAmount = 0.4f;
	private void UpdateRubberBanding()
	{
		float targetRubberBandingRatio = CalculateRubberBandingRatio();
		currentRubberBandRatio = ExtensionMethods.SmoothDamp(currentRubberBandRatio, targetRubberBandingRatio, ref rubberBandVelocity, RubberBandSmoothAmount);
		animator.SpeedScale = currentRubberBandRatio;
	}

	private float CalculateRubberBandingRatio()
	{
		if (!Stage.IsRaceActive)
			return 1f;

		if ((maxProgress - Progress) < RubberBandEndDeadZone) // Too close to the end; allow the player to catch up
			return 1f;

		float playerProgress = path.Curve.GetClosestOffset(path.ToLocal(StageSettings.Player.GlobalPosition));
		float deltaProgress = Progress - playerProgress;
		if (Mathf.Abs(deltaProgress) <= RubberBandDeadZone) // Too close to the player; disable rubber banding
			return 1f;

		if (playerProgress < Progress) // Uhu is ahead; don't slow down
			return 1f;

		// Uhu is playing catchup
		return 1f - (RubberBandFactor * Mathf.Clamp(deltaProgress / MaxRubberBandDistance, -1f, 1f));
	}

	private void UpdateRacePositions()
	{
		float uhuRatio = (Progress - startingProgress) / (maxProgress - startingProgress);
		float playerRatio = path.Curve.GetClosestOffset(path.ToLocal(StageSettings.Player.GlobalPosition));
		playerRatio = (playerRatio - startingProgress) / (maxProgress - startingProgress);
		HeadsUpDisplay.Instance.UpdateRace(playerRatio, uhuRatio);
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
		Stage.IsRaceActive = false;
		trail.IsEmitting = false;
	}
}
