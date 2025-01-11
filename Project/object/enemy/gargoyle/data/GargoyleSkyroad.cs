using Godot;
using Project.Gameplay.Objects;
using Project.Core;

namespace Project.Gameplay;

public partial class GargoyleSkyroad : PathFollow3D
{
	private PlayerController Player => StageSettings.Player;

	[Export] private Node3D root;
	private Vector3 velocity;

	[Export] private SkyRoad activeRoad;
	private Path3D CurrentPath => activeRoad.Path;
	private readonly float PositionSmoothing = 0.2f;
	private readonly float MinDistanceToPlayer = 20.0f;
	private readonly float BaseMovementSpeed = 20.0f;

	public override void _Ready()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public override void _PhysicsProcess(double _)
	{
		if (activeRoad == null)
			return;

		Progress += BaseMovementSpeed * PhysicsManager.physicsDelta;

		root.Position = root.Position.SmoothDamp(Vector3.Back * 2.0f, ref velocity, PositionSmoothing);
		float playerProgress = CurrentPath.Curve.GetClosestOffset(Player.PathFollower.GlobalPosition - CurrentPath.GlobalPosition);
		float delta = Progress - playerProgress;
		if (delta < MinDistanceToPlayer && !Mathf.IsZeroApprox(playerProgress))
			Progress = playerProgress + MinDistanceToPlayer;

		activeRoad.SetPathRatio(ProgressRatio);

		if (Mathf.IsEqualApprox(ProgressRatio, 1.0f))
			PlayExitAnimation();
	}

	public void SetSkyRoad()
	{
		if (!Visible)
			PlayEntryAnimation();

		GD.Print(activeRoad);
		if (activeRoad == null)
			return;

		Vector3 offset = root.GlobalPosition;
		GetParent().RemoveChild(this);
		CurrentPath.AddChild(this);
		Progress = CurrentPath.Curve.GetClosestOffset(offset - CurrentPath.GlobalPosition);
		root.GlobalPosition = offset;
	}

	public void Activate()
	{
		SetSkyRoad();
	}

	private void PlayEntryAnimation()
	{
		// TODO play FX
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;
	}

	private void PlayExitAnimation()
	{
		// TODO play proper leaving FX
		activeRoad = null;
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}
}
