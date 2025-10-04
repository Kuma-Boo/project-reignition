using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Zipline : PathFollow3D
{
	[Signal]
	public delegate void ActivatedEventHandler();

	[Export] public float ZiplineSpeed { get; private set; }

	/// <summary> How fast the Zipline is currently moving. </summary>
	public float CurrentSpeed { get; private set; }

	/// <summary> The player's current rotation in radians, clamped from -Mathf.Pi to Mathf.Pi. </summary>
	public float CurrentRotation { get; private set; }
	/// <summary> The side the zipline is currently on. </summary>
	public float SwingSide => -Mathf.Sign(CurrentRotation);
	public float SwingVelocitySide => Mathf.Sign(rotationVelocity);

	private float startingProgress;
	private float rotationVelocity;

	private readonly float ZiplineAcceleration = 5.0f;
	private readonly float ZiplineDecceleration = 20.0f;

	[ExportGroup("Components")]
	[Export] private Node3D root;
	[Export] public Node3D FollowObject { get; private set; }
	[Export] private CollisionShape3D collider;
	[Export] private GpuParticles3D sparkFX;
	[Export] private AudioStreamPlayer3D ziplineSfx;

	public override void _Ready()
	{
		StageSettings.Instance.Respawned += Respawn;
		startingProgress = Progress;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public override void _PhysicsProcess(double _delta)
	{
		// Is control being handled by ZiplineState?
		if (StageSettings.Player.IsZiplineActive)
			return;

		bool isZiplineStopped = Mathf.Abs(CurrentRotation) < Mathf.Pi * .01f && Mathf.IsZeroApprox(CurrentSpeed);

		// Attempts to reset rotation, then disables zipline node
		UpdateSpeed(0f);
		UpdateRotation(0f, isZiplineStopped ? 0f : ZiplineAcceleration);
		ziplineSfx.VolumeLinear = CurrentSpeed / ZiplineSpeed;

		// Deactivate Zipline when it comes to a stop
		if (isZiplineStopped)
		{
			ziplineSfx.Stop();
			ProcessMode = ProcessModeEnum.Disabled;
		}
	}

	public void Respawn()
	{
		ziplineSfx.VolumeLinear = 1f;
		ziplineSfx.Stop();
		Progress = startingProgress;
		UpdateSpeed(0f, true);
		UpdateRotation(0f, 0f);

		collider.Disabled = false;
		sparkFX.Visible = false;
	}

	public void UpdateSpeed(float targetSpeed, bool snapSpeed = false)
	{
		if (snapSpeed)
			CurrentSpeed = targetSpeed;
		else if (StageSettings.Player.IsZiplineActive)
			CurrentSpeed = Mathf.MoveToward(CurrentSpeed, targetSpeed, ZiplineAcceleration * PhysicsManager.physicsDelta);
		else
			CurrentSpeed = Mathf.MoveToward(CurrentSpeed, targetSpeed, ZiplineDecceleration * PhysicsManager.physicsDelta);

		// Move the Zipline forward by its current speed
		Progress += CurrentSpeed * PhysicsManager.physicsDelta;
	}

	public void UpdateRotation(float targetRotation, float rotationSmoothing)
	{
		if (Mathf.IsZeroApprox(rotationSmoothing))
		{
			CurrentRotation = targetRotation;
			rotationVelocity = 0;
		}
		else
		{
			CurrentRotation = ExtensionMethods.SmoothDampAngle(CurrentRotation, targetRotation, ref rotationVelocity, rotationSmoothing * PhysicsManager.physicsDelta);

			// Ensure rotation is between -Mathf.Pi & Mathf.Pi
			CurrentRotation = ExtensionMethods.ModAngle(CurrentRotation);
			if (CurrentRotation > Mathf.Pi)
				CurrentRotation -= Mathf.Tau;
		}

		// Apply rotation
		root.Rotation = Vector3.Forward * CurrentRotation;
	}

	public void StopZipline() => sparkFX.Emitting = false;

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		StageSettings.Player.StartZipline(this);

		UpdateSpeed(ZiplineSpeed, true);
		ProcessMode = ProcessModeEnum.Inherit;
		collider.Disabled = true;
		sparkFX.Restart();
		sparkFX.Visible = true;
		ziplineSfx.Play();

		EmitSignal(SignalName.Activated);
	}
}
