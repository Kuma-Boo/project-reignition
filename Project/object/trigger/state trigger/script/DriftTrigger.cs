using Godot;
using Project.Core;

namespace Project.Gameplay.Triggers;

/// <summary>
/// Makes the player do a 90 degree turn when entering fast enough.
/// </summary>
[Tool]
public partial class DriftTrigger : Area3D
{
	[Signal]
	public delegate void DriftStartedEventHandler();
	[Signal]
	public delegate void DriftCompletedEventHandler();

	[Export]
	private bool isRightTurn; // Which way is the corner?
	public bool IsRightTurn => isRightTurn;
	/// <summary> How far to slide. </summary>
	[Export(PropertyHint.Range, "1, 10")]
	private int slideDistance = 10;

	private PlayerController Player => StageSettings.Player;
	public Vector3 EndPosition => MiddlePosition + (ExitDirection * slideDistance);
	public Vector3 MiddlePosition => GlobalPosition + (this.Back() * slideDistance);
	public Vector3 ExitDirection => this.Right() * (isRightTurn ? 1 : -1);

	[ExportGroup("Components")]
	[Export]
	private AudioStreamPlayer sfx;
	private float startingVolume;
	private bool isFadingSFX;
	private float MinStartingVolume = -6f; // SFX volume when player enters slowly

	private bool isInteractingWithPlayer;
	/// <summary> Entrance speed (ratio) required to start a drift. </summary>
	private readonly float EntranceSpeedRatio = .9f;

	public override void _PhysicsProcess(double _)
	{
		if (isInteractingWithPlayer)
		{
			AttemptDrift();
			return;
		}

		if (!isFadingSFX)
			return;

		isFadingSFX = SoundManager.FadeAudioPlayer(sfx);
	}

	/// <summary> Checks whether the player is in a state where a drift is possible. </summary>
	private void AttemptDrift()
	{
		if (Player.IsDrifting)
			return;

		if (!Player.IsOnGround)
			return;

		if (Player.IsMovingBackward)
			return;

		if (Player.PathFollower.Progress > Player.PathFollower.GetProgress(GlobalPosition))
			return;

		if (Player.Stats.GroundSettings.GetSpeedRatio(Player.MoveSpeed) < EntranceSpeedRatio)
			return;

		if (Player.ExternalController != null)
			return; // Player is already busy

		// Check for any obstructions
		RaycastHit hit = Player.CastRay(Player.CollisionPosition, Player.PathFollower.Forward() * slideDistance, Runtime.Instance.environmentMask);
		if (hit && !hit.collidedObject.IsInGroup("level wall"))
			return;

		Player.StartDrift(this);
	}

	public void UpdateSfxVolume(float distance)
	{
		float volume = distance / slideDistance;
		sfx.VolumeDb = Mathf.SmoothStep(startingVolume, -80f, volume);
	}
	public void FadeSfx() => isFadingSFX = true; // Fade sound effect

	public void Activate()
	{
		EmitSignal(SignalName.DriftStarted);

		float volumeRatio = Player.Stats.GroundSettings.GetSpeedRatioClamped(Player.MoveSpeed) - (EntranceSpeedRatio / (1 - EntranceSpeedRatio));
		startingVolume = Mathf.Lerp(MinStartingVolume, 0, volumeRatio);
		isFadingSFX = false;
		sfx.VolumeDb = startingVolume;
		sfx.Play();
	}

	public void Deactivate() => EmitSignal(SignalName.DriftCompleted);

	/// <summary> Tracks whether drift bonus was already applied. </summary>
	private bool wasBonusApplied;
	public void ApplyBonus(bool isDriftSuccessful)
	{
		if (wasBonusApplied) return; // Bonus was already applied

		if (isDriftSuccessful)
			BonusManager.instance.QueueBonus(new(BonusType.Drift));

		wasBonusApplied = true;
	}

	public void OnEntered(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = true;
	}

	public void OnExited(Area3D a)
	{
		if (!a.IsInGroup("player detection"))
			return;

		isInteractingWithPlayer = false;
		if (!Player.IsDrifting)
			ApplyBonus(false); // Invalid drift, skip bonus (if possible)

		/*
		{
			driftStatus = DriftStatus.Inactive; // Reset to inactive state
		}
		*/
	}
}