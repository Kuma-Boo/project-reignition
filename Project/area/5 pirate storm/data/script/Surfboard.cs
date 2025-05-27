using Godot;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary>
/// Controls the surfboard in Pirate Storm Act 1.
/// </summary>
public partial class Surfboard : PathTraveller
{
	[Signal] public delegate void WaveStartedEventHandler();
	[Signal] public delegate void HighJumpStartedEventHandler();
	[Signal] public delegate void HighJumpFinishedEventHandler();
	[Signal] public delegate void MediumJumpStartedEventHandler();
	[Signal] public delegate void LowJumpStartedEventHandler();
	[Signal] public delegate void WaveFinishedEventHandler();

	[Export] private float initialSpeed;
	[Export] private float initialTurnSpeed;
	private readonly float MinimumSpeed = 2.0f;

	[Export] private AudioStreamPlayer3D[] waveSFX;

	private int waveIndex;
	private int currentSpeedIndex;
	private float speedFactor;
	private readonly float MaxSpeedIndex = 10;

	private Wave currentWave;
	private Path3D originalPath;

	private bool isCrouching;
	private float highJumpCameraTimer;
	private readonly float HighJumpCameraLength = 1.5f;

	protected override float GetCurrentMaxSpeed
	{
		get
		{
			float speed = Mathf.Lerp(initialSpeed, MaxSpeed, speedFactor);
			if (currentWave?.IsWaveCleared == false)
			{
				float waveRatio = currentWave.CalculateMovementRatio(GlobalPosition);
				float speedLoss = currentWave.requiredSpeedBoosts / (currentSpeedIndex + 0.5f);
				speed -= Mathf.Lerp(initialSpeed, speed, speedLoss) * waveRatio;
				if (currentSpeedIndex >= currentWave.requiredSpeedBoosts)
					speed = Mathf.Max(MinimumSpeed, speed);
			}

			return speed;
		}
	}

	protected override float GetCurrentTurnSpeed => Mathf.Lerp(initialTurnSpeed, TurnSpeed, GetCurrentMaxSpeed / MaxSpeed);

	protected override void SetUp()
	{
		base.SetUp();
		originalPath = path;
	}

	protected override void Respawn()
	{
		waveIndex = 0;
		speedFactor = 0;
		currentSpeedIndex = 0;

		isCrouching = false;
		highJumpCameraTimer = 0;
		base.Respawn();
	}

	public override void ProcessPathTraveller()
	{
		base.ProcessPathTraveller();

		if (!Mathf.IsZeroApprox(highJumpCameraTimer)) // Don't allow turning during a high jump
		{
			CurrentTurnAmount = Vector2.Zero;
			turnVelocity = Vector2.Zero;
		}

		Player.Animator.UpdateBalanceCrouch(isCrouching);

		if (Mathf.IsZeroApprox(highJumpCameraTimer))
			return;

		highJumpCameraTimer = Mathf.MoveToward(highJumpCameraTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(highJumpCameraTimer))
			EmitSignal(SignalName.HighJumpFinished);
	}

	protected override void Accelerate()
	{
		if (currentWave?.IsWaveCleared == false)
		{
			CurrentSpeed = GetCurrentMaxSpeed;

			if (CurrentSpeed < MinimumSpeed) // Going too slow! Fall off the board
				CallDeferred(MethodName.EmitSignal, SignalName.Damaged);

			return;
		}

		base.Accelerate();
	}

	public void SetCurrentWave(Wave wave)
	{
		// Update wave sound effects
		if (wave == null)
		{
			for (int i = 0; i < waveIndex; i++)
				waveSFX[i].Stop();
		}
		else
		{
			waveIndex++;
			for (int i = 0; i < waveIndex; i++)
				waveSFX[i].Play();

			Player.Camera.StartCameraShake(new()
			{
				magnitude = Vector3.One * 0.5f,
				duration = waveIndex,
			});
		}

		currentWave = wave;
		path = wave != null ? wave.Path : originalPath;
		PathFollower.GetParent().RemoveChild(PathFollower);
		path.AddChild(PathFollower);
		PathFollower.Progress = path.Curve.GetClosestOffset(GlobalPosition - path.GlobalPosition);
		EmitSignal(wave != null ? SignalName.WaveStarted : SignalName.WaveFinished);
	}

	public void StartJump()
	{
		isCrouching = false;
		if (currentSpeedIndex >= 8)
		{
			Player.Animator.StartBalanceTrick("high");
			highJumpCameraTimer = HighJumpCameraLength;
			EmitSignal(SignalName.HighJumpStarted);
		}
		else if (currentSpeedIndex >= 4)
		{
			Player.Animator.StartBalanceTrick("medium");
			EmitSignal(SignalName.MediumJumpStarted);
		}
		else
		{
			Player.Animator.StartBalanceTrick("low");
			EmitSignal(SignalName.LowJumpStarted);
		}

		// TODO Add trick bonuses?
	}

	private void UpdateSpeedIndex(int amount)
	{
		currentSpeedIndex = (int)Mathf.Clamp(currentSpeedIndex + amount, 0, MaxSpeedIndex);
		speedFactor = currentSpeedIndex / MaxSpeedIndex;
	}

	protected override void Stagger()
	{
		UpdateSpeedIndex(-1);
		isCrouching = false;
		base.Stagger();
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("surfboard speedboost"))
			return;

		// Vroom Vroom
		UpdateSpeedIndex(1);
		isCrouching = true;
		CurrentSpeed = GetCurrentMaxSpeed;
		animator.Play("speedboost");
		animator.Seek(0.0);
	}
}
