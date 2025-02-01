using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Zipline : PathFollow3D
{
	[Signal]
	public delegate void ActivatedEventHandler();

	[Export] public Node3D Root { get; set; }
	[Export] public Node3D FollowObject { get; set; }

	[Export] private float ziplineSpeed = 10.0f;

	private float startingProgress;

	private bool isFullSwingActive;

	private float currentRotation;
	private float targetRotation;
	private float rotationVelocity;
	private float rotationSmoothing;
	private readonly float FastRotationSmoothing = 15.0f;
	private readonly float SlowRotationSmoothing = 20.0f;
	private readonly float NormalRotationLimit = Mathf.Pi * .4f;

	private float inputValue;
	public void SetInput(float input) => inputValue = input;

	public override void _Ready()
	{
		StageSettings.Instance.ConnectRespawnSignal(this);
		startingProgress = Progress;
	}

	public void Respawn()
	{
		Progress = startingProgress;
		Root.Rotation = Vector3.Zero;
		currentRotation = targetRotation = 0;
	}

	public void ProcessZipline()
	{
		Progress += ziplineSpeed * PhysicsManager.physicsDelta; // Move forward

		// Ensure rotation is between -Mathf.Pi & Mathf.Pi
		float clampedRotation = ExtensionMethods.ModAngle(currentRotation);
		if (clampedRotation > Mathf.Pi)
			clampedRotation -= Mathf.Tau;

		CheckFullSwing(clampedRotation);
		CalculateTargetRotation(clampedRotation);
		currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, targetRotation, ref rotationVelocity, rotationSmoothing * PhysicsManager.physicsDelta);
		Root.Rotation = Vector3.Forward * currentRotation;
	}

	private void CalculateTargetRotation(float clampedRotation)
	{
		rotationSmoothing = FastRotationSmoothing;

		bool isOverRotated = Mathf.Abs(currentRotation) > NormalRotationLimit;
		bool isFalling = Mathf.Sign(clampedRotation) != Mathf.Sign(inputValue);

		if (isOverRotated && isFalling) // Force player back to normal rotation
		{
			float leadBlend = (Mathf.Abs(clampedRotation) - NormalRotationLimit) / (Mathf.Pi - NormalRotationLimit);
			float leadAmount = Mathf.Lerp(Mathf.Pi * .9f, NormalRotationLimit, leadBlend);
			targetRotation = currentRotation - (Mathf.Sign(clampedRotation) * leadAmount);
			return;
		}

		if (isFullSwingActive)
		{
			if (isOverRotated) // Force player to make it around
			{
				targetRotation = currentRotation + (Mathf.Sign(inputValue) * NormalRotationLimit);
				rotationSmoothing = SlowRotationSmoothing;
				return;
			}

			if (!isFalling)
			{
				targetRotation = Mathf.Sign(inputValue) * Mathf.Pi;
				rotationSmoothing = FastRotationSmoothing;
				return;
			}
		}

		if (isOverRotated) // Abitrary logic to deal with direction changes
		{
			float inputInfluence = 1f - ((Mathf.Abs(clampedRotation) - NormalRotationLimit) / (Mathf.Pi - NormalRotationLimit));
			inputInfluence *= 3f * Mathf.Sign(rotationVelocity);
			inputInfluence += inputValue;
			targetRotation = currentRotation + (inputInfluence * NormalRotationLimit);
			rotationSmoothing = SlowRotationSmoothing;
			return;
		}

		targetRotation = inputValue * NormalRotationLimit;
		rotationSmoothing = Mathf.Lerp(SlowRotationSmoothing, FastRotationSmoothing, Mathf.Abs(inputValue));
	}

	private void CheckFullSwing(float clampedRotation)
	{
		bool isSignAligned = Mathf.Sign(clampedRotation) == Mathf.Sign(inputValue);
		bool isHoldingDirection = Mathf.Abs(inputValue) > .5f;

		if (isFullSwingActive)
		{
			if (!isHoldingDirection)
				isFullSwingActive = false;

			return;
		}

		if (isSignAligned || !isHoldingDirection)
			return;

		if (Mathf.Abs(clampedRotation) < NormalRotationLimit * .6f) // Full Swing must start from near the normal rotation limit
			return;

		isFullSwingActive = true;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		StageSettings.Player.StartZipline(this);
		EmitSignal(SignalName.Activated);
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;
	}
}
