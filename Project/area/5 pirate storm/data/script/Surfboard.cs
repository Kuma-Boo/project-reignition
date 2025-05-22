using Godot;

namespace Project.Gameplay.Objects;

/// <summary>
/// Controls the surfboard in Pirate Storm Act 1.
/// </summary>
public partial class Surfboard : PathTraveller
{
	[Export] private float initialSpeed;
	[Export] private float initialTurnSpeed;
	private int currentSpeedIndex;
	private readonly float MaxSpeedIndex = 10;
	private float speedFactor;

	protected override float GetCurrentMaxSpeed => Mathf.Lerp(initialSpeed, MaxSpeed, speedFactor);
	protected override float GetCurrentTurnSpeed => Mathf.Lerp(initialTurnSpeed, TurnSpeed, speedFactor);

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("surfboard speedboost"))
			return;

		// Vroom Vroom
		UpdateSpeedIndex(1);
		CurrentSpeed = GetCurrentMaxSpeed;
	}

	private void UpdateSpeedIndex(int amount)
	{
		currentSpeedIndex = (int)Mathf.Clamp(currentSpeedIndex + amount, 0, MaxSpeedIndex);
		speedFactor = currentSpeedIndex / MaxSpeedIndex;
		GD.Print(speedFactor);
	}

	protected override void Stagger()
	{
		UpdateSpeedIndex(-1);
		base.Stagger();
	}
}
