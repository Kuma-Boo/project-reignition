using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay;

/// <summary> Most common enemy type in Secret Rings. </summary>
[Tool]
public partial class Majin : Enemy
{
	[Signal]
	public delegate void SpinStartedEventHandler();
	[Signal]
	public delegate void WanderStartedEventHandler();

	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties =
			[
				ExtensionMethods.CreateProperty("Spawn Settings/Spawn Travel Time", Variant.Type.Float, PropertyHint.Range, "0,2,.1")
			];

		if (SpawnTravelEnabled)
		{
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Delay", Variant.Type.Float, PropertyHint.Range, "0,2,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Offset", Variant.Type.Vector3));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn In Offset", Variant.Type.Vector3));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Out Offset", Variant.Type.Vector3));
		}

		properties.Add(ExtensionMethods.CreateProperty("Spawn Interval Settings/Enable Spawn Interval", Variant.Type.Bool));
		if (SpawnIntervalEnabled)
		{
			properties.Add(ExtensionMethods.CreateProperty("Spawn Interval Settings/Spawn Delay", Variant.Type.Float, PropertyHint.Range, "0.1,10,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Interval Settings/Separate Despawn Interval", Variant.Type.Bool));
			if (SeparateDespawninterval)
				properties.Add(ExtensionMethods.CreateProperty("Spawn Interval Settings/Despawn Delay", Variant.Type.Float, PropertyHint.Range, "0.1,10,.1"));
		}

		if (attackType != AttackTypes.Spin && attackType != AttackTypes.Wander)
		{
			properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Track Player", Variant.Type.Bool));
			if (!trackPlayer)
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Rotation Time", Variant.Type.Float));
		}
		else
		{
			trackPlayer = false;
		}

		properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Attack Type", Variant.Type.Int, PropertyHint.Enum, attackType.EnumToString()));
		if (IsRedMajin) // Show relevant fire-related settings
		{
			properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Flame Active Time", Variant.Type.Float, PropertyHint.Range, "0.1,10,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Flame Inactive Time", Variant.Type.Float, PropertyHint.Range, "0,10,.1"));
			properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Flame Aggression Radius", Variant.Type.Int, PropertyHint.Range, "0,100,1"));
			properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Attack Instantly", Variant.Type.Bool));
		}

		properties.Add(ExtensionMethods.CreateProperty("Defeat Settings/Enable Enemy Launching", Variant.Type.Bool));

		if (IsDefeatLaunchEnabled)
		{
			properties.Add(ExtensionMethods.CreateProperty("Defeat Settings/Launch Time", Variant.Type.Float, PropertyHint.Range, "0.1,1,0.1"));
			properties.Add(ExtensionMethods.CreateProperty("Defeat Settings/Launch Direction", Variant.Type.Vector3));
			properties.Add(ExtensionMethods.CreateProperty("Defeat Settings/Local Transform", Variant.Type.Bool));
		}

		return properties;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
			case "Spawn Settings/Spawn Travel Time":
				return spawnTravelTime;
			case "Spawn Settings/Spawn Delay":
				return spawnDelay;
			case "Spawn Settings/Spawn Offset":
				return spawnOffset;
			case "Spawn Settings/Spawn In Offset":
				return spawnInOffset;
			case "Spawn Settings/Spawn Out Offset":
				return spawnOutOffset;
			case "Spawn Interval Settings/Enable Spawn Interval":
				return SpawnIntervalEnabled;
			case "Spawn Interval Settings/Spawn Delay":
				return spawnIntervalDelay;
			case "Spawn Interval Settings/Separate Despawn Interval":
				return SeparateDespawninterval;
			case "Spawn Interval Settings/Despawn Delay":
				return despawnIntervalDelay;

			case "Rotation Settings/Track Player":
				return trackPlayer;
			case "Rotation Settings/Rotation Time":
				return rotationTime;

			case "Attack Settings/Attack Type":
				return (int)attackType;
			case "Attack Settings/Flame Active Time":
				return flameActiveTime;
			case "Attack Settings/Flame Inactive Time":
				return flameInactiveTime;
			case "Attack Settings/Flame Aggression Radius":
				return FlameAggressionRadius;
			case "Attack Settings/Attack Instantly":
				return isInstantFlame;

			case "Defeat Settings/Enable Enemy Launching":
				return IsDefeatLaunchEnabled;
			case "Defeat Settings/Launch Time":
				return defeatLaunchTime;
			case "Defeat Settings/Launch Direction":
				return defeatLaunchDirection;
			case "Defeat Settings/Local Transform":
				return isDefeatLocalTransform;
		}

		return base._Get(property);
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Spawn Settings/Spawn Travel Time":
				bool toggle = SpawnTravelEnabled == Mathf.IsZeroApprox((float)value);
				spawnTravelTime = (float)value;

				if (toggle)
					NotifyPropertyListChanged();
				break;
			case "Spawn Settings/Spawn Delay":
				spawnDelay = (float)value;
				break;
			case "Spawn Settings/Spawn Offset":
				spawnOffset = (Vector3)value;
				break;
			case "Spawn Settings/Spawn In Offset":
				spawnInOffset = (Vector3)value;
				break;
			case "Spawn Settings/Spawn Out Offset":
				spawnOutOffset = (Vector3)value;
				break;
			case "Spawn Interval Settings/Enable Spawn Interval":
				spawnIntervalDelay = (bool)value ? 1 : 0;
				NotifyPropertyListChanged();
				break;
			case "Spawn Interval Settings/Spawn Delay":
				spawnIntervalDelay = (float)value;
				break;
			case "Spawn Interval Settings/Separate Despawn Interval":
				despawnIntervalDelay = (bool)value ? 1 : 0;
				NotifyPropertyListChanged();
				break;
			case "Spawn Interval Settings/Despawn Delay":
				despawnIntervalDelay = (float)value;
				break;

			case "Rotation Settings/Track Player":
				trackPlayer = (bool)value;
				NotifyPropertyListChanged();
				break;
			case "Rotation Settings/Rotation Time":
				rotationTime = Mathf.RoundToInt((float)value * 10.0f) * .1f;
				break;

			case "Attack Settings/Attack Type":
				attackType = (AttackTypes)(int)value;
				NotifyPropertyListChanged();
				break;
			case "Attack Settings/Flame Active Time":
				flameActiveTime = (float)value;
				break;
			case "Attack Settings/Flame Inactive Time":
				flameInactiveTime = (float)value;
				break;
			case "Attack Settings/Flame Aggression Radius":
				FlameAggressionRadius = (int)value;
				break;
			case "Attack Settings/Attack Instantly":
				isInstantFlame = (bool)value;
				break;

			case "Defeat Settings/Enable Enemy Launching":
				IsDefeatLaunchEnabled = (bool)value;
				NotifyPropertyListChanged();
				break;
			case "Defeat Settings/Launch Time":
				defeatLaunchTime = (float)value;
				break;
			case "Defeat Settings/Launch Direction":
				defeatLaunchDirection = (Vector3)value;
				break;
			case "Defeat Settings/Local Transform":
				isDefeatLocalTransform = (bool)value;
				break;

			default:
				return false;
		}

		return true;
	}
	#endregion

	[Export(PropertyHint.NodeType, "Node3D")]
	private NodePath flameRoot;
	private Node3D FlameRoot { get; set; }
	[Export(PropertyHint.NodeType, "Node3D")]
	private NodePath hitboxRoot;
	private Node3D HitboxRoot { get; set; }
	[Export(PropertyHint.NodeType, "BoneAttachment3D")]
	private NodePath hitboxAttachment;
	private BoneAttachment3D HitboxAttachment { get; set; }
	private AnimationNodeBlendTree animatorRoot;
	private AnimationNodeTransition moveTransition;
	private AnimationNodeTransition stateTransition;
	private AnimationNodeStateMachinePlayback spinState;
	private AnimationNodeStateMachinePlayback fireState;

	/// <summary> True when either the majin is spawning in, or despawning out from a spawn interval. </summary>
	private bool isSpawning;
	private float currentTravelRatio;
	/// <summary> How long does spawn traveling take? Set to 0 to spawn instantly. </summary>
	private float spawnTravelTime;
	/// <summary> How long should spawning be delayed? </summary>
	private float spawnDelay;
	/// <summary> Where to spawn from (Added with OriginalPosition) </summary>
	private Vector3 spawnOffset;
	/// <summary> In handle for spawn traveling. Use this to shape the general curve. </summary>
	private Vector3 spawnInOffset;
	/// <summary> Out handle for spawn traveling. Use this to add a final bit of rotation easing. </summary>
	private Vector3 spawnOutOffset;
	/// <summary> Should the majin spawn/despawn on loop? </summary>
	private bool SpawnIntervalEnabled => !Mathf.IsZeroApprox(spawnIntervalDelay);
	/// <summary> Should the majin have a unique despawn interval? </summary>
	private bool SeparateDespawninterval => !Mathf.IsZeroApprox(despawnIntervalDelay);
	/// <summary> How long should spawning be delayed? </summary>
	private float spawnIntervalDelay;
	/// <summary> How long should spawning be delayed? </summary>
	private float despawnIntervalDelay;
	private bool isFinishedTraveling;
	public bool SpawnTravelEnabled => !Mathf.IsZeroApprox(spawnTravelTime);
	public Basis CalculationBasis => Engine.IsEditorHint() ? GlobalBasis : GetParent<Node3D>().GlobalBasis.Inverse() * calculationBasis;
	private Basis calculationBasis;
	/// <summary> Local Position to be after spawning is complete. </summary>
	public Vector3 OriginalPosition => Engine.IsEditorHint() ? GlobalPosition : SpawnData.spawnTransform.Origin;
	public Vector3 SpawnPosition => OriginalPosition + (CalculationBasis * spawnOffset);
	public Vector3 InHandle => SpawnPosition + (CalculationBasis * spawnInOffset);
	public Vector3 OutHandle => OriginalPosition + (CalculationBasis * spawnOutOffset);

	/// <summary> Use this to launch the enemy when defeated. </summary>
	public bool IsDefeatLaunchEnabled { get; private set; }
	/// <summary> Use local transform for launch direction? </summary>
	private bool isDefeatLocalTransform = true;
	/// <summary> How long should the enemy be launched?  </summary>
	private float defeatLaunchTime = .5f;
	/// <summary> Direction to launch. Leave at Vector3.Zero to automatically calculate. </summary>
	private Vector3 defeatLaunchDirection;
	public Vector3 CalculateLaunchPosition()
	{
		Vector3 launchVector = defeatLaunchDirection;
		if (launchVector.IsEqualApprox(Vector3.Zero) && !Engine.IsEditorHint()) // Calculate launch direction
		{
			launchVector = (Player.Animator.Back() + (Player.Animator.Up() * .2f)).Normalized();
			launchVector = launchVector.Normalized() * Mathf.Clamp(Player.MoveSpeed, 5, 20);
		}
		else if (isDefeatLocalTransform)
		{
			launchVector = GlobalTransform.Basis * launchVector;
		}

		launchVector = launchVector.Rotated(Vector3.Up, Mathf.Pi); // Fix forward direction

		return GlobalPosition + launchVector;
	}

	/// <summary> Responsible for handling tweens (i.e. Spawning/Default launching) </summary>
	private Tween tweener;
	/// <summary> Responsible for handling spawn toggling. </summary>
	private float timer = -1;

	/// <summary> Should this majin rotate to face the player? </summary>
	private bool trackPlayer = true;
	/// <summary> How long to complete a rotation cycle when trackPlayer is false. </summary>
	private float rotationTime;
	private float rotationAmount;

	private AttackTypes attackType;
	private enum AttackTypes
	{
		Disabled, // Just idle
		Spin, // Spin like a top
		Fire, // Spit fire out
		Wander, // Stay in a "moving forward" state. Use a pathfollower if you need a consistent movement pattern.
	}

	public bool IsRedMajin => attackType == AttackTypes.Fire;
	/// <summary> Only become aggressive within a certain radius? </summary>
	public int FlameAggressionRadius { get; private set; }
	/// <summary> Is the flame attack currently active? </summary>
	private bool isFlameActive;
	private float flameActiveTime = 1.0f;
	private float flameInactiveTime;
	/// <summary> Should the fire majin attack instantly when its range is entered? </summary>
	private bool isInstantFlame;
	/// <summary> Timer to keep track of flame cycles. </summary>
	private float flameTimer;

	/// <summary> Timer to keep track of stagger length. </summary>
	private float staggerTimer;
	private const float StaggerLength = 1.2f;

	// Animation parameters
	private readonly StringName IdleState = "idle";
	private readonly StringName SpinState = "spin";
	private readonly StringName FireState = "fire";
	private readonly StringName StateRequestParameter = "parameters/state_transition/transition_request";

	private readonly StringName EnabledState = "enabled";
	private readonly StringName DisabledState = "disabled";
	private readonly StringName MoveTransition = "parameters/move_transition/transition_request";
	private readonly StringName MoveBlend = "parameters/move_blend/blend_position";
	private readonly StringName SpawnTrigger = "parameters/spawn_trigger/request";
	private readonly StringName DespawnTrigger = "parameters/despawn_trigger/request";
	private readonly StringName HitTransition = "parameters/hit_transition/transition_request";
	private readonly StringName StaggerState = "stagger";
	private readonly StringName BoopState = "boop";

	private const float MovementTransitionLength = .4f;

	protected override void SetUp()
	{
		if (Engine.IsEditorHint()) return; // In Editor

		FlameRoot = GetNodeOrNull<Node3D>(flameRoot);
		HitboxRoot = GetNodeOrNull<Node3D>(hitboxRoot);
		HitboxAttachment = GetNodeOrNull<BoneAttachment3D>(hitboxAttachment);
		calculationBasis = GlobalBasis; // Cache GlobalBasis for calculations
		base.SetUp();

		AnimationTree.Active = true;
		animatorRoot = AnimationTree.TreeRoot as AnimationNodeBlendTree;
		moveTransition = animatorRoot.GetNode("move_transition") as AnimationNodeTransition;
		stateTransition = animatorRoot.GetNode("state_transition") as AnimationNodeTransition;
		fireState = (AnimationNodeStateMachinePlayback)AnimationTree.Get("parameters/fire_state/playback");
		spinState = (AnimationNodeStateMachinePlayback)AnimationTree.Get("parameters/spin_state/playback");

		// Skeleton3D node is always 3 nodes away from the root node
		HitboxAttachment.SetExternalSkeleton(HitboxAttachment.GetPathTo(Root.GetChild(0).GetChild(0).GetChild(0)));
		HitboxAttachment.BoneIdx = 0;

		if (attackType == AttackTypes.Wander)
			wanderHomePosition = GlobalPosition;
	}

	public override void Respawn()
	{
		// Kill any active tweens
		tweener?.Kill();
		timer = -1;

		isSpawning = false;
		isFinishedTraveling = false;

		AnimationPlayer.Play("RESET");
		AnimationPlayer.Advance(0);

		// Reset rotation
		if (!trackPlayer && !Mathf.IsZeroApprox(rotationTime)) // Precalculate rotation amount
			rotationAmount = Mathf.Tau / rotationTime;

		rotationVelocity = 0;
		currentRotation = 0;
		ApplyRotation();

		// Reset idle movement
		idleFactorVelocity = 0;
		stateTransition.XfadeTime = 0f;
		AnimationTree.Set(IdleFactorParameter, 0);
		AnimationTree.Set(DefeatTransitionParameter, DisabledState);
		AnimationTree.Set(StateRequestParameter, IdleState);

		// Reset timers
		staggerTimer = 0;
		wanderTimer = 0;

		// Reset flame attack
		if (IsRedMajin)
		{
			isFlameActive = false;
			flameTimer = (FlameAggressionRadius != 0 || isInstantFlame) ? flameInactiveTime : 0;
		}

		base.Respawn();

		if (!isSpawning) // Start hidden
		{
			Visible = false;
			SetHitboxStatus(false);
		}
	}

	public override void TakeDamage(int amount = -1)
	{
		Stagger();
		base.TakeDamage(amount);
	}

	public void DisableFlameAttack()
	{
		if (isFlameActive)
			ToggleFlameAttack();
	}

	private void Stagger()
	{
		staggerTimer = StaggerLength;
		DisableFlameAttack();

		if (Player.AttackState == PlayerController.AttackStates.None)
		{
			AnimationTree.Set(HitTransition, BoopState);
		}
		else
		{
			AnimationTree.Set(HitTransition, StaggerState);
			AnimationPlayer.Play("stagger");
		}
		AnimationTree.Set(HitTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	protected override void Defeat()
	{
		if (isSpawning) // Prevent signals from killing majin during spawn intervals
			return;

		SetHitboxStatus(false, IsDefeatLaunchEnabled);

		AnimationPlayer.Play("defeat");
		if (!IsSpeedbreakDefeat && IsDefeatLaunchEnabled && !Mathf.IsZeroApprox(defeatLaunchTime))
		{
			// Kill any existing tween
			tweener?.Kill();

			AnimationTree.Set(DefeatTransitionParameter, EnabledState);
			AnimationTree.Set(HitTrigger, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);

			Vector3 targetRotation = Vector3.Up * Mathf.Tau * 2.0f;
			if (Runtime.randomNumberGenerator.Randf() > .5f)
				targetRotation *= -1;

			targetRotation += Vector3.Left * Mathf.Pi * .25f;

			// Get knocked back
			tweener = CreateTween().SetParallel();
			tweener.TweenProperty(this, "global_position", CalculateLaunchPosition(), defeatLaunchTime);
			tweener.TweenProperty(this, "rotation", Rotation + targetRotation, defeatLaunchTime * 2.0f).SetEase(Tween.EaseType.In);
			tweener.TweenCallback(Callable.From(() => AnimationPlayer.Play("launch-end"))).SetDelay(defeatLaunchTime * .5f);
			tweener.TweenCallback(Callable.From(() => SetHitboxStatus(false, false))).SetDelay(defeatLaunchTime);
		}
		else
		{
			AnimationPlayer.Advance(0.0);
			AnimationPlayer.Play("explode");

			if (IsSpeedbreakDefeat) // Skip defeat animation and explode instantly
				AnimationPlayer.Advance(0.5);
		}

		base.Defeat();
	}

	protected override void UpdateEnemy()
	{
		if (Engine.IsEditorHint()) return; // In Editor

		// Update hitbox transforms manually to avoid JoltPhysics scaling errors
		HitboxRoot.GlobalPosition = HitboxAttachment.GlobalPosition;
		HitboxRoot.GlobalRotation = HitboxAttachment.GlobalRotation;

		if (SpawnIntervalEnabled)
			UpdateSpawnInterval();

		if (isSpawning)
		{
			UpdateTravel();
			return;
		}

		if (!IsActive) return; // Hasn't spawned yet
		if (IsDefeated)
		{
			if (IsSpeedbreakDefeat) // Cheat particle effects to stay onscreen
				GlobalPosition += Player.GetPositionDelta();

			return; // Already defeated
		}

		if (staggerTimer != 0)
		{
			staggerTimer = Mathf.MoveToward(staggerTimer, 0, PhysicsManager.physicsDelta);
			return;
		}

		if (IsRedMajin)
			UpdateFlameAttack();

		UpdateRotation();
		UpdateFidgets();

		if (attackType == AttackTypes.Wander)
			UpdateWandering();
	}

	private float wanderTimer;
	/// <summary> The position the majin considers as "home." </summary>
	private Vector3 wanderHomePosition;
	/// <summary> The position where the majin is trying to move. </summary>
	private Vector3 wanderPosition;
	/// <summary> How far the majin can move from their home position. </summary>
	private readonly float WanderRadius = 5f;
	/// <summary> How fast the majin moves while wandering. </summary>
	private readonly float WanderSpeed = 2f;
	private readonly float WanderRotationSmoothing = 50f;
	/// <summary> The minimum amount of time a majin should wander in a single direction. </summary>
	private readonly float MinWanderLength = 3f;
	/// <summary> The maximum amount of time a majin should wander in a single direction. </summary>
	private readonly float MaxWanderLength = 5f;
	private void UpdateWandering()
	{
		ProcessRotation(wanderPosition, WanderRotationSmoothing);

		GlobalPosition += Root.Forward() * WanderSpeed * PhysicsManager.physicsDelta;

		if (GlobalPosition.DistanceSquaredTo(wanderPosition) < 1f)
		{
			// Reached target position; calculate new wandering position
			UpdateWanderingPosition();
			return;
		}

		wanderTimer = Mathf.MoveToward(wanderTimer, 0, PhysicsManager.physicsDelta);
		if (Mathf.IsZeroApprox(wanderTimer))
		{
			// Been travelling in a single direction for too long -- choose a new position
			UpdateWanderingPosition();
			return;
		}

		// Calculate a new wandering position if we hit a wall
		Vector3 castDirection = wanderPosition - GlobalPosition;
		RaycastHit hit = this.CastRay(GlobalPosition, castDirection, Runtime.Instance.environmentMask);
		DebugManager.DrawRay(GlobalPosition, castDirection, Colors.Red);
		if (hit && hit.collidedObject.IsInGroup("wall")) // Find a new target wandering position
			UpdateWanderingPosition();
	}

	/// <summary>
	/// Chooses a random point within the wander radius around the majin's original position.
	/// </summary>
	private void UpdateWanderingPosition()
	{
		float wanderRotation = (Runtime.randomNumberGenerator.Randf() - 0.5f) * Mathf.Tau;
		float wanderRadius = Runtime.randomNumberGenerator.Randf() * WanderRadius;
		wanderTimer = Runtime.randomNumberGenerator.RandfRange(MinWanderLength, MaxWanderLength);
		wanderPosition = wanderHomePosition + Vector3.Forward.Rotated(Vector3.Up, wanderRotation) * wanderRadius;
	}

	private void UpdateRotation()
	{
		bool OutsideFlameAggression = IsRedMajin && !isFlameActive && !IsInFlameAggressionRange();
		if (OutsideFlameAggression || !IsInRange)
		{
			currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, 0, ref rotationVelocity, TrackingSmoothing * PhysicsManager.physicsDelta);
			if (OutsideFlameAggression && !isFlameActive)
				flameTimer = flameInactiveTime;
			return;
		}

		if (trackPlayer && (attackType == AttackTypes.Disabled || attackType == AttackTypes.Fire)) // Rotate to face player
		{
			ProcessRotation(Player.GlobalPosition);
			return;
		}

		if (!Mathf.IsZeroApprox(rotationTime))
		{
			rotationVelocity = Mathf.Lerp(rotationVelocity, rotationAmount, TrackingSmoothing * PhysicsManager.physicsDelta);
			currentRotation = ExtensionMethods.ModAngle(currentRotation + (PhysicsManager.physicsDelta * rotationVelocity));
			ApplyRotation();
		}
	}

	protected override void ApplyRotation()
	{
		base.ApplyRotation();
		FlameRoot.Rotation = Vector3.Up * currentRotation;
	}

	private void UpdateSpawnInterval()
	{
		if (Mathf.IsEqualApprox(timer, -1)) // Spawn intervals haven't started yet
			return;

		if (IsRedMajin && ((IsActive && IsInFlameAggressionRange()) || isFlameActive)) // Don't allow spawn intervals during a flame attack
			return;

		timer = Mathf.MoveToward(timer, 0, PhysicsManager.physicsDelta);
		if (!Mathf.IsZeroApprox(timer))
			return;

		if (Player.IsHomingAttacking)
			return;

		timer = -1;
		ToggleSpawnState();
	}

	private float idleFactorVelocity;
	private const float IDLE_FACTOR_SMOOTHING = 30.0f; // Idle movement strength smoothing

	private bool isFidgetActive;
	private int fidgetIndex; // Index of the current fidget animation.
	private float fidgetTimer; // Keeps track of how long it's been since the last fidget

	private readonly StringName[] FidgetAnimations = [
			"flip",
			"fight",
			"survey",
		];

	private readonly StringName HitTrigger = "parameters/hit_trigger/request";
	private readonly StringName DefeatTransitionParameter = "parameters/defeat_transition/transition_request";

	private readonly StringName IdleFactorParameter = "parameters/idle_movement_factor/add_amount";
	private readonly StringName FidgetTransition = "parameters/fidget_transition/transition_request"; // Sets the fidget animation
	private readonly StringName FidgetTrigger = "parameters/fidget_trigger/request"; // Currently fidgeting? Set StringName
	private readonly StringName FidgetTriggerState = "parameters/fidget_trigger/active"; // Get StringName
	private const float FidgetFrequency = 3f; // How often to fidget

	/// <summary> Updates fidgets and idle movement. </summary>
	private void UpdateFidgets()
	{
		if (attackType == AttackTypes.Spin) return; // No need to process fidgets when in AttackTypes.Spin

		float targetIdleFactor = 1.0f;
		if (isFidgetActive) // Adjust movement strength based on fidget
		{
			if (fidgetIndex == 0)
				targetIdleFactor = 0.0f;
			else if (fidgetIndex == 1)
				targetIdleFactor = 0.5f;

			isFidgetActive = (bool)AnimationTree.Get(FidgetTriggerState);
		}
		else if (attackType != AttackTypes.Fire || !IsInRange) // Wait for fidget to start
		{
			fidgetTimer += PhysicsManager.physicsDelta;
			if (fidgetTimer > FidgetFrequency) // Start fidget
			{
				fidgetTimer = 0; // Reset timer
				fidgetIndex = Runtime.randomNumberGenerator.RandiRange(0, FidgetAnimations.Length - 1);
				AnimationTree.Set(FidgetTransition, FidgetAnimations[fidgetIndex]);
				AnimationTree.Set(FidgetTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				isFidgetActive = true;
			}
		}

		float idleFactor = (float)AnimationTree.Get(IdleFactorParameter);
		idleFactor = ExtensionMethods.SmoothDamp(idleFactor, targetIdleFactor, ref idleFactorVelocity, IDLE_FACTOR_SMOOTHING * PhysicsManager.physicsDelta);
		AnimationTree.Set(IdleFactorParameter, idleFactor);
	}

	private void UpdateFlameAttack()
	{
		if (!IsHitboxEnabled)
			return;

		if (!IsInRange || isFidgetActive) // Out of range or fidget is active
		{
			DisableFlameAttack();
			return;
		}

		if (Mathf.IsZeroApprox(flameInactiveTime) && isFlameActive) return; // Always activated

		if (isFlameActive)
		{
			flameTimer = Mathf.MoveToward(flameTimer, flameActiveTime, PhysicsManager.physicsDelta);
			if (Mathf.IsEqualApprox(flameTimer, flameActiveTime)) // Switch off
				ToggleFlameAttack();
		}
		else
		{
			// Return early if the player is outside of the majin's aggression radius
			if (!Mathf.IsZeroApprox(FlameAggressionRadius) && !IsInFlameAggressionRange())
				return;

			flameTimer = Mathf.MoveToward(flameTimer, flameInactiveTime, PhysicsManager.physicsDelta);
			if (Mathf.IsEqualApprox(flameTimer, flameInactiveTime)) // Switch on
				ToggleFlameAttack();
		}
	}

	private bool IsInFlameAggressionRange()
	{
		if (FlameAggressionRadius == 0)
			return true;

		float distance = (GlobalPosition - Player.GlobalPosition).Flatten().LengthSquared();
		// Because raising to the 2nd power is better than taking a square root...
		return distance < Mathf.Pow(FlameAggressionRadius, 2);
	}

	private void ToggleFlameAttack()
	{
		isFlameActive = !isFlameActive;

		if (isFlameActive) // Start fire attack
		{
			AnimationPlayer.Play("fire-start");
			fireState.Travel("attack-fire-start");
			stateTransition.XfadeTime = 0.1;
			AnimationTree.Set(StateRequestParameter, FireState);
		}
		else // Stop flame attack
		{
			fireState.Travel("attack-fire-end");
			AnimationPlayer.Play("fire-end");
			stateTransition.XfadeTime = 0.2;
			AnimationPlayer.Advance(0.0);
		}

		flameTimer = 0; // Reset timer
	}

	protected override void Spawn()
	{
		if (isSpawning || IsActive || IsDefeated) return;

		isSpawning = true;
		SetDeferred("visible", true);

		tweener?.Kill();
		tweener = CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics);

		AnimationTree.Set(StateRequestParameter, IdleState); // Idle

		if (SpawnTravelEnabled && !isFinishedTraveling) // Travel
		{
			currentTravelRatio = 0;
			Position = SpawnPosition;

			AnimationPlayer.Play("travel");
			tweener.TweenProperty(this, nameof(currentTravelRatio), 1, spawnTravelTime).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut).SetDelay(spawnDelay);
			tweener.TweenCallback(new Callable(this, MethodName.FinishSpawning));

			moveTransition.XfadeTime = 0;
			AnimationTree.Set(MoveTransition, EnabledState); // Travel animation
		}
		else // Spawn instantly
		{
			AnimationPlayer.Play("spawn");
			AnimationTree.Set(SpawnTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
			tweener.TweenCallback(new Callable(this, MethodName.FinishSpawning)).SetDelay(.5f); // Delay by length of teleport animation
		}

		// Reset flame attack
		if (IsRedMajin)
		{
			isFlameActive = false;
			flameTimer = (FlameAggressionRadius != 0 && isInstantFlame) ? flameInactiveTime : 0;
		}

		AnimationPlayer.Advance(0.0);
		base.Spawn();
	}

	public override void Despawn()
	{
		if (IsDefeated) // Remove from the scene tree
		{
			timer = -1;
			base.Despawn();
			return;
		}

		tweener?.Kill();
		tweener = CreateTween().SetProcessMode(Tween.TweenProcessMode.Physics);

		isSpawning = true;
		SetHitboxStatus(false);
		AnimationPlayer.Play("despawn");
		AnimationTree.Set(DespawnTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		tweener.TweenCallback(new Callable(this, MethodName.FinishDespawning)).SetDelay(.5f); // Delay by length of teleport animation
		EmitSignal(SignalName.Despawned);
	}

	protected override void StartUhuBounce()
	{
		AnimationTree.Set(HitTransition, BoopState);
		AnimationTree.Set(HitTrigger, (int)AnimationNodeOneShot.OneShotRequest.Fire);
	}

	public void ToggleSpawnState()
	{
		if (IsDefeated)
			return;

		if (IsActive)
			Despawn();
		else
			Spawn();
	}

	private void UpdateTravel()
	{
		// Calculate tangent (rotation)
		Vector3 velocity = CalculationBasis.Inverse() * CalculateTravelVelocity(currentTravelRatio).Normalized();
		if (Mathf.Abs(velocity.Dot(Vector3.Up)) < .9f && !velocity.IsZeroApprox())
			currentRotation = -velocity.SignedAngleTo(Vector3.Back, Vector3.Up);

		Position = CalculateTravelPosition(currentTravelRatio);
		ApplyRotation();

		if (Mathf.IsEqualApprox(currentTravelRatio, 1f))
			return;

		Vector3 acceleration = CalculationBasis.Inverse() * CalculateTravelAcceleration(currentTravelRatio).Normalized();
		Vector2 moveBlend = new(acceleration.X, Mathf.Clamp(velocity.Y, 0, 1));
		if (Mathf.IsZeroApprox(acceleration.X))
			moveBlend.Y = velocity.Y;
		AnimationTree.Set(MoveBlend, moveBlend);
	}

	/// <summary> Use Bezier interpolation to get the majin's position. </summary>
	public Vector3 CalculateTravelPosition(float t) => SpawnPosition.BezierInterpolate(InHandle, OutHandle, OriginalPosition, t);
	/// <summary> The derivative is the majin's velocity. </summary>
	public Vector3 CalculateTravelVelocity(float t) => SpawnPosition.BezierDerivative(InHandle, OutHandle, OriginalPosition, t);
	/// <summary> The derivative of velocity is the majin's acceleration. </summary>
	public Vector3 CalculateTravelAcceleration(float t) => CalculateTravelVelocity(t).BezierDerivative(InHandle, OutHandle, OriginalPosition, t);

	private void FinishSpawning()
	{
		IsActive = true;
		isSpawning = false;
		isFinishedTraveling = true;
		SetHitboxStatus(true);

		if (SpawnTravelEnabled && attackType != AttackTypes.Wander)
		{
			moveTransition.XfadeTime = MovementTransitionLength;
			AnimationTree.Set(MoveTransition, DisabledState); // Stopped moving
		}

		if (attackType == AttackTypes.Spin)
		{
			spinState.Travel("attack-spin-start");
			stateTransition.XfadeTime = 0.2f;
			AnimationTree.Set(StateRequestParameter, SpinState);

			tweener?.Kill();
			tweener = CreateTween();
			tweener.TweenCallback(new Callable(this, MethodName.OnSpinActivated)).SetDelay(0.3f); // Delay spin activation by windup animation length
		}
		else if (attackType == AttackTypes.Wander)
		{
			AnimationTree.Set(MoveTransition, EnabledState); // Keep moving
			EmitSignal(SignalName.WanderStarted);
		}

		if (SpawnIntervalEnabled)
			timer = SeparateDespawninterval ? despawnIntervalDelay : spawnIntervalDelay;
	}

	private void FinishDespawning()
	{
		IsActive = false;
		isSpawning = false;

		if (SpawnIntervalEnabled)
			timer = spawnIntervalDelay;
	}

	/// <summary> Called after spin attack's windup animation. </summary>
	public void OnSpinActivated()
	{
		if (IsDefeated)
			return;

		AnimationPlayer.Play("spin");
		EmitSignal(SignalName.SpinStarted);
	}
}
