using Godot;
using Project.Gameplay.Objects;
using Project.Core;

namespace Project.Gameplay;

public partial class GargoyleSkyroad : PathFollow3D
{
	private PlayerController Player => StageSettings.Player;

	[Export] private SkyRoad activeRoad;
	/// <summary> The sequence of paths this Gargoyle will travel (in order). </summary>
	[Export] private Path3D[] paths;
	private Path3D CurrentPath => paths[currentPathIndex];

	[ExportGroup("Components")]
	[Export] private Node3D root;
	[Export] private AnimationPlayer animator;
	[Export] private AnimationTree animationTree;

	private Vector3 velocity;
	private SpawnData spawnData;

	// Alternative variables because we're working with multiple paths
	private float traveledDistance; // Distance we finished based on completed paths' baked length 
	private float totalDistance;
	private int currentPathIndex;

	private bool isFastSpeed;
	private float speedBlend;
	private float speedTimer;
	private readonly float SpeedSmoothing = 5f;
	private readonly float BaseMovementSpeed = 20f;
	private readonly float FastMovementSpeed = 40f;
	private readonly string FlapSpeedParameter = "parameters/flap_speed/scale";
	private readonly string FlapTransitionParameter = "parameters/flap_transition/transition_request";
	private readonly string EnabledState = "enabled";
	private readonly string DisabledState = "disabled";

	private readonly float PositionSmoothing = 0.2f;
	private readonly float MinDistanceToPlayer = 2.0f;

	public override void _Ready()
	{
		InitializePathLength();

		spawnData = new(GetParent(), Transform);
		StageSettings.Instance.Respawned += Respawn;

		animationTree.Active = true;
		Respawn();
	}

	public void Respawn()
	{
		currentPathIndex = 0;
		velocity = Vector3.Zero;
		spawnData.Respawn(this);

		Progress = 0;
		traveledDistance = 0;
		activeRoad.SetPathRatio(0.0f);
		speedBlend = 0.0f;
		isFastSpeed = true;

		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}

	public override void _PhysicsProcess(double _)
	{
		if (currentPathIndex >= paths.Length)
			return;

		// Smoothly move the local position to the correct distance
		root.Position = root.Position.SmoothDamp(Vector3.Back * 2.0f, ref velocity, PositionSmoothing);

		float pathDelta = CalculateMovementDelta();
		ProcessSlipstream(pathDelta);
		if (pathDelta < MinDistanceToPlayer && Player.PathFollower.ActivePath == CurrentPath) // Ensure we're a set distance away from the player
			Progress = Player.PathFollower.Progress + MinDistanceToPlayer;

		// Move gargoyle along the path
		float movementDelta = CalculateMoveSpeed(pathDelta) * PhysicsManager.physicsDelta;
		float targetProgress = Progress - movementDelta;
		Progress += movementDelta;

		activeRoad.SetPathRatio((traveledDistance + Progress) / totalDistance); // Update visuals
		if (Mathf.IsEqualApprox(ProgressRatio, 1.0f))
			IncrementPathIndex(targetProgress - Progress);

		UpdateAnimations();
	}

	private void UpdateAnimations()
	{
		bool isFlappingFast = isFastSpeed || (isSlipstreamActive && Player.Skills.IsSpeedBreakActive);

		animationTree.Set(FlapTransitionParameter, isFlappingFast ? EnabledState : DisabledState);
		if (isFlappingFast) // Update flapping speed
			animationTree.Set(FlapSpeedParameter, 1.5f + (speedBlend * .2f));
	}

	private float CalculateMovementDelta()
	{
		if (Player.PathFollower.ActivePath == CurrentPath)
			return Progress - Player.PathFollower.Progress;

		// Different path
		return Mathf.Inf;
	}

	private float CalculateMoveSpeed(float pathDelta)
	{
		if (Mathf.IsZeroApprox(speedBlend) || Mathf.IsEqualApprox(speedBlend, 1.0f))
		{
			// Only update timer when movement speed isn't changing
			speedTimer = Mathf.MoveToward(speedTimer, 0.0f, PhysicsManager.physicsDelta);
			if (CanToggleSpeed(pathDelta))
				ToggleFastSpeed();
		}

		speedBlend = Mathf.MoveToward(speedBlend, isFastSpeed ? 1.0f : 0.0f, SpeedSmoothing * PhysicsManager.physicsDelta);
		return Mathf.Lerp(BaseMovementSpeed, FastMovementSpeed, speedBlend) + (Player.Stats.GroundSettings.Speed - Player.Stats.baseGroundSpeed);
	}

	private bool CanToggleSpeed(float pathDelta)
	{
		if (isSlipstreamActive)
			return false;

		if (!Mathf.IsZeroApprox(speedTimer))
			return false;

		// Prevent gargoyle from entering fast speed when flying ahead too far
		if (!isFastSpeed && pathDelta > MinDistanceToPlayer * 5f)
			return false;

		return true;
	}

	private void ToggleFastSpeed()
	{
		isFastSpeed = !isFastSpeed;

		if (isFastSpeed)
			speedTimer = Runtime.randomNumberGenerator.RandfRange(1f, 2f);
		else
			speedTimer = Runtime.randomNumberGenerator.RandfRange(3f, 5f);
	}

	/// <summary>
	/// New addition to Reignition.
	/// Increases player speed when near Gargyole to make skyroads less boring.
	/// </summary>
	private bool isSlipstreamActive;
	private int slipstreamsTriggered;
	private float slipstreamTimer;
	private readonly float SlipstreamInterval = 1f;
	private readonly float SlipstreamMultiplier = 2f;
	private readonly float SlipstreamRange = 3f;
	/// <summary> How many boosts should occur before the gargoyle flies away. </summary>
	private readonly int MaxSlipstreamCount = 4;
	private void ProcessSlipstream(float pathDelta)
	{
		if (pathDelta > MinDistanceToPlayer + SlipstreamRange) // Out of range
		{
			StopSlipstream();
			return;
		}

		if (isSlipstreamActive)
		{
			if (Player.Skills.IsSpeedBreakActive)
				return;

			slipstreamTimer = Mathf.MoveToward(slipstreamTimer, 0, PhysicsManager.physicsDelta);
			if (Mathf.IsZeroApprox(slipstreamTimer))
				ApplySlipstream();

			return;
		}

		if (isFastSpeed)
			return;

		if (Player.Skills.IsSpeedBreakActive)
		{
			StartSlipstream();
			return;
		}

		if (!isFastSpeed)
			ToggleFastSpeed(); // Too close and not performing a slipstream; start flying away instantly
	}

	private void StartSlipstream()
	{
		isSlipstreamActive = true;
		slipstreamTimer = 0f;
		slipstreamsTriggered = 0;
	}

	private void StopSlipstream()
	{
		isSlipstreamActive = false;
	}

	private void ApplySlipstream()
	{
		slipstreamTimer = SlipstreamInterval;
		Player.MoveSpeed = Mathf.Max(Player.MoveSpeed, Player.Stats.GroundSettings.Speed * SlipstreamMultiplier);
		Player.Effect.PlayWindFX();
		Player.Effect.PlayWindCrestFX();
		slipstreamsTriggered++;

		if (slipstreamsTriggered >= MaxSlipstreamCount) // Make the number of slipstreams consistent
		{
			StopSlipstream();
			ToggleFastSpeed();
		}
	}

	private void IncrementPathIndex(float remainingProgress)
	{
		traveledDistance += CurrentPath.Curve.GetBakedLength(); // Add the current path's length to traveled distance

		currentPathIndex++;
		if (currentPathIndex == paths.Length) // Finished all the paths
		{
			PlayExitAnimation();
			return;
		}

		ReparentToPath(); // Reparent to new path and start again
		Progress = Mathf.Max(remainingProgress, 0f);
	}

	// Get the length of all our paths and store for later
	private void InitializePathLength()
	{
		foreach (Path3D path in paths)
			totalDistance += path.Curve.GetBakedLength();
	}

	public void Activate()
	{
		if (Visible)
			return;

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
		Visible = true;
		ProcessMode = ProcessModeEnum.Inherit;
		animator.Play("spawn");
	}

	private void PlayExitAnimation() => animator.Play("despawn", 0.1);

	private void Deactivate()
	{
		Visible = false;
		ProcessMode = ProcessModeEnum.Disabled;
	}
}
