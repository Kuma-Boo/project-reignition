using Godot;
using Project.Gameplay.Objects;
using Project.Core;

namespace Project.Gameplay;

public partial class GargoyleSkyroad : PathFollow3D
{
	private PlayerController Player => StageSettings.Player;

	private SpawnData spawnData;

	[Export] private Node3D root;
	private Vector3 velocity;

	[Export] private SkyRoad activeRoad;
	/// <summary> The sequence of paths this Gargoyle will travel (in order). </summary>
	[Export] private Path3D[] paths;
	private Path3D CurrentPath => paths[currentPathIndex];

	// Alternative variables because we're working with multiple paths
	private float traveledDistance; // Distance we finished based on completed paths' baked length 
	private float totalDistance;
	private int currentPathIndex;

	private readonly float PositionSmoothing = 0.2f;
	private readonly float MinDistanceToPlayer = 20.0f;
	private readonly float BaseMovementSpeed = 20.0f;

	public override void _Ready()
	{
		Visible = false;
		InitializePathLength();

		spawnData = new(GetParent(), Transform);
		StageSettings.Instance.ConnectRespawnSignal(this);

		ProcessMode = ProcessModeEnum.Disabled;
	}

	public void Respawn()
	{
		currentPathIndex = 0;
		velocity = Vector3.Zero;
		spawnData.Respawn(this);
	}

	public override void _PhysicsProcess(double _)
	{
		if (activeRoad == null)
			return;

		// Smoothly move the local position to the correct distance
		root.Position = root.Position.SmoothDamp(Vector3.Back * 2.0f, ref velocity, PositionSmoothing);

		// Move gargoyle along the path
		float movementDelta = BaseMovementSpeed * PhysicsManager.physicsDelta;
		Progress += movementDelta;
		if (Mathf.IsEqualApprox(ProgressRatio, 1.0f))
			IncrementPathIndex();

		// Ensure we're a set distance away from the player
		float playerProgress = CurrentPath.Curve.GetClosestOffset(Player.PathFollower.GlobalPosition - CurrentPath.GlobalPosition);
		float delta = Progress - playerProgress;
		if (delta < MinDistanceToPlayer && !Mathf.IsZeroApprox(playerProgress))
			Progress = playerProgress + MinDistanceToPlayer;

		activeRoad.SetPathRatio((traveledDistance + Progress) / totalDistance); // Update visuals
	}

	public void IncrementPathIndex()
	{
		traveledDistance += CurrentPath.Curve.GetBakedLength(); // Add the current path's length to traveled distance

		currentPathIndex++;
		if (currentPathIndex == paths.Length) // Finished all the paths
		{
			PlayExitAnimation();
			return;
		}

		ReparentToPath(); // Reparent to new path and start again
	}

	// Get the length of all our paths and store for later
	private void InitializePathLength()
	{
		foreach (Path3D path in paths)
			totalDistance += path.Curve.GetBakedLength();
	}

	public void Activate()
	{
		if (!Visible)
			PlayEntryAnimation();

		ReparentToPath();
	}

	private void ReparentToPath()
	{
		Vector3 offset = root.GlobalPosition;
		GetParent().RemoveChild(this);
		CurrentPath.AddChild(this);
		Progress = 0;
		root.GlobalPosition = offset;
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
