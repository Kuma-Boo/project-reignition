using System;
using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Zipline : PathFollow3D
{
	[Signal]
	public delegate void ActivatedEventHandler();

	[Export] public float ZiplineSpeed { get; private set; }
	public float CurrentSpeed { get; private set; }
	public void SetSpeed(float targetSpeed, bool snapSpeed = false)
	{
		if (snapSpeed)
		{
			CurrentSpeed = targetSpeed;
			return;
		}

		CurrentSpeed = Mathf.MoveToward(CurrentSpeed, targetSpeed, NaturalAcceleration * PhysicsManager.physicsDelta);
	}

	private float startingProgress;

	private bool isFullSwingActive;

	private float currentRotation;
	private float targetRotation;
	private float rotationVelocity;
	private float rotationSmoothing;
	private readonly float FastRotationSmoothing = 15.0f;
	private readonly float SlowRotationSmoothing = 20.0f;
	private readonly float NormalRotationLimit = Mathf.Pi * .4f;
	private readonly float ReverseSwingRotationLimit = Mathf.Pi * .8f;
	private readonly float NaturalAcceleration = 10.0f;

	private float inputValue;
	private bool isDoubleTapping;
	private float tapBuffer;
	private readonly float TapTimer = .2f;
	public void SetInput(float input)
	{
		inputValue = input;

		if (Mathf.Abs(inputValue) <= SaveManager.Config.deadZone && !Mathf.IsZeroApprox(tapBuffer))
			isDoubleTapping = true;
	}

	[ExportGroup("Components")]
	[Export] public Node3D Root { get; private set; }
	[Export] public Node3D FollowObject { get; private set; }
	[Export] public CollisionShape3D Collider { get; private set; }
	[Export] public GpuParticles3D SparkFX { get; private set; }

	public override void _Ready()
	{
		StageSettings.Instance.Respawned += Respawn;
		startingProgress = Progress;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public override void _PhysicsProcess(double _delta)
	{
		if (StageSettings.Player.IsZiplineActive)
			return;

		// Attempts to reset rotation, then disables zipline node
		CurrentSpeed = Mathf.MoveToward(CurrentSpeed, 0, NaturalAcceleration * PhysicsManager.physicsDelta);
		ProcessZipline();

		if (Mathf.Abs(currentRotation) < Mathf.Pi * .01f && Mathf.IsZeroApprox(CurrentSpeed))
		{
			currentRotation = 0;
			Root.Rotation = Vector3.Forward * currentRotation;
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}

	public void Respawn()
	{
		Progress = startingProgress;
		Root.Rotation = Vector3.Zero;
		CurrentSpeed = 0;
		currentRotation = targetRotation = 0;
		rotationVelocity = rotationSmoothing = 0;
		isFullSwingActive = false;
		Collider.Disabled = false;

		SparkFX.Visible = false;
	}

	public void ProcessZipline()
	{
		Progress += CurrentSpeed * PhysicsManager.physicsDelta; // Move forward
		ProcessRotation();
	}

	public void StopZipline()
	{
		SetInput(0);
		SparkFX.Emitting = false;
	}

	private void ProcessRotation()
	{
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

		// Allow switching directions mid full-swing
		if (Math.Abs(currentRotation) > ReverseSwingRotationLimit && isHoldingDirection)
			isFullSwingActive = true;

		if (isFullSwingActive)
		{
			if (!isHoldingDirection || (!isSignAligned && Mathf.Abs(currentRotation) > NormalRotationLimit))
				isFullSwingActive = false;

			return;
		}

		tapBuffer = Mathf.MoveToward(tapBuffer, 0, PhysicsManager.physicsDelta);

		if (isSignAligned)
		{
			isDoubleTapping = false;
			return;
		}

		if (!isHoldingDirection)
			return;

		if (isDoubleTapping)
		{
			isFullSwingActive = true;
			isDoubleTapping = false;
			tapBuffer = 0;
			return;
		}

		if (Mathf.Abs(clampedRotation) < NormalRotationLimit * .5f) // Full Swing must start from near the normal rotation limit
			return;

		tapBuffer = TapTimer;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		StageSettings.Player.StartZipline(this);

		SetSpeed(ZiplineSpeed, true);
		ProcessMode = ProcessModeEnum.Inherit;
		Collider.Disabled = true;
		SparkFX.Restart();
		SparkFX.Visible = true;

		EmitSignal(SignalName.Activated);
	}
}
