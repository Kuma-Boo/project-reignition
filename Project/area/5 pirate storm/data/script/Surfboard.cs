using Godot;

namespace Project.Gameplay.Objects;

/// <summary>
/// Controls the surfboard in Pirate Storm Act 1.
/// </summary>
public partial class Surfboard : PathTraveller
{
	[Signal] public delegate void WaveStartedEventHandler();
	[Signal] public delegate void JumpStartedEventHandler();
	[Signal] public delegate void WaveFinishedEventHandler();

	[Export] private float initialSpeed;
	[Export] private float initialTurnSpeed;

	[Export] private AudioStreamPlayer3D[] waveSFX;

	private int waveIndex;
	private int currentSpeedIndex;
	private float speedFactor;
	private readonly float MaxSpeedIndex = 10;

	private Wave currentWave;
	private Path3D originalPath;

	private readonly float SlopeFactor = 80.0f;
	private readonly float SlopeResetSpeed = 5.0f;

	protected override float GetCurrentMaxSpeed
	{
		get
		{
			float defaultSpeed = Mathf.Lerp(initialSpeed, MaxSpeed, speedFactor);
			if (currentWave?.IsWaveCleared == false)
			{
				float movementRatio = currentWave.CalculateMovementRatio(GlobalPosition);
				float speedLossRatio = currentWave.requiredSpeedBoosts / MaxSpeedIndex;
				defaultSpeed -= Mathf.Lerp(initialSpeed, MaxSpeed, speedLossRatio) * movementRatio;
				GD.PrintT(defaultSpeed, speedLossRatio, movementRatio);
			}

			return defaultSpeed;
		}
	}

	protected override void Accelerate()
	{
		if (currentWave?.IsWaveCleared == false)
		{
			CurrentSpeed = GetCurrentMaxSpeed;

			if (CurrentSpeed < 1f) // Going too slow! Fall off the board
				EmitSignal(SignalName.Damaged);

			return;
		}

		base.Accelerate();
	}

	protected override float GetCurrentTurnSpeed => Mathf.Lerp(initialTurnSpeed, TurnSpeed, GetCurrentMaxSpeed / MaxSpeed);

	protected override void SetUp()
	{
		base.SetUp();
		originalPath = path;
	}

	protected override void Respawn()
	{
		currentSpeedIndex = 0;
		speedFactor = 0;
		waveIndex = 0;
		base.Respawn();
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
		GD.Print("TODO -- Perform Jumps");
		EmitSignal(SignalName.JumpStarted);
	}

	private void UpdateSpeedIndex(int amount)
	{
		currentSpeedIndex = (int)Mathf.Clamp(currentSpeedIndex + amount, 0, MaxSpeedIndex);
		speedFactor = currentSpeedIndex / MaxSpeedIndex;
	}

	protected override void Stagger()
	{
		UpdateSpeedIndex(-1);
		base.Stagger();
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("surfboard speedboost"))
			return;

		// Vroom Vroom
		UpdateSpeedIndex(1);
		CurrentSpeed = GetCurrentMaxSpeed;
		animator.Play("speedboost");
		animator.Seek(0.0);
	}
}
