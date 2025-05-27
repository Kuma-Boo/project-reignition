using Godot;

namespace Project.Gameplay.Objects;

/// <summary> Handles the logic of the waves in Pirate Storm Act 1. </summary>
public partial class Wave : Node3D
{
	[Export(PropertyHint.Range, "1, 10")] public int requiredSpeedBoosts = 1;
	[Export] private AnimationPlayer animator;
	[Export] public Path3D Path { get; private set; }

	private Surfboard surfboard;
	public bool IsWaveCleared { get; private set; }
	private readonly float BaseWaveHeight = 75f;

	public override void _Ready()
	{
		StageSettings.Instance.Respawned += Respawn;
		Respawn();
	}

	private void Respawn()
	{
		animator.Play("hide");
		IsWaveCleared = false;
	}

	public void ClearWave() => IsWaveCleared = true;

	public float CalculateMovementRatio(Vector3 surfboardPosition) => (surfboardPosition.Y - GlobalPosition.Y) / (BaseWaveHeight * GlobalBasis.Scale.Y);

	/// <summary> Call this from a StageTrigger. </summary>
	public void OnWaveApproached() => animator.Play("show");

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("surfboard"))
			return;

		surfboard = a.GetParentOrNull<Surfboard>();
		if (surfboard == null)
		{
			GD.PushError("Expected a surfboard as the direct parent of the Surfboard's Area3D node!");
			return;
		}

		surfboard.SetCurrentWave(this);
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("surfboard"))
			return;

		if (surfboard == null)
			return;

		surfboard.SetCurrentWave(null);
	}

	public void OnJumpEntered(Area3D a)
	{
		if (!a.IsInGroup("surfboard"))
			return;

		if (surfboard == null)
			return;

		ClearWave();
		surfboard.StartJump();
	}
}
