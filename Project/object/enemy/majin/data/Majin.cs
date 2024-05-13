using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// Most common enemy type in Secret Rings.
	/// </summary>
	[Tool]
	public partial class Majin : Enemy
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new();

			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Travel Time", Variant.Type.Float, PropertyHint.Range, "0,2,.1"));
			if (!SpawnTravelDisabled)
			{
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Delay", Variant.Type.Float, PropertyHint.Range, "0,2,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Offset", Variant.Type.Vector3));
			}

			properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Track Player", Variant.Type.Bool));
			if (!trackPlayer)
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Rotation Time", Variant.Type.Float));

			properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Attack Type", Variant.Type.Int, PropertyHint.Enum, attackType.EnumToString()));
			if (attackType == AttackTypes.Fire) // Show relevant fire-related settings
			{
				properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Flame Active Time", Variant.Type.Float, PropertyHint.Range, "0.1,10,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Flame Inactive Time", Variant.Type.Float, PropertyHint.Range, "0,10,.1"));
			}

			properties.Add(ExtensionMethods.CreateProperty("Defeat Settings/Enable Enemy Launching", Variant.Type.Bool));

			if (isDefeatLaunchEnabled)
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

				case "Defeat Settings/Enable Enemy Launching":
					return isDefeatLaunchEnabled;
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
					bool toggle = SpawnTravelDisabled != Mathf.IsZeroApprox((float)value);
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

				case "Defeat Settings/Enable Enemy Launching":
					isDefeatLaunchEnabled = (bool)value;
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

		[Export]
		private Node3D fireRoot;
		private AnimationNodeBlendTree animatorRoot;
		private AnimationNodeTransition moveTransition;
		private AnimationNodeTransition stateTransition;
		private AnimationNodeStateMachinePlayback spinState;
		private AnimationNodeStateMachinePlayback fireState;

		private bool SpawnTravelDisabled => Mathf.IsZeroApprox(spawnTravelTime);
		/// <summary> How long does spawn traveling take? Set to 0 to spawn instantly. </summary>
		private float spawnTravelTime;
		/// <summary> How long should spawning be delayed? </summary>
		private float spawnDelay;
		public Vector3 SpawnOffset => GlobalBasis * spawnOffset;
		/// <summary> Where to spawn from (Added with OriginalPosition) </summary>
		private Vector3 spawnOffset;
		/// <summary> Local Position to be after spawning is complete. </summary>
		private Vector3 OriginalPosition => SpawnData.spawnTransform.Origin;
		private bool isSpawning;

		/// <summary> Use this to launch the enemy when defeated. </summary>
		private bool isDefeatLaunchEnabled;
		/// <summary> Use local transform for launch direction? </summary>
		private bool isDefeatLocalTransform = true;
		/// <summary> How long should the enemy be launched?  </summary>
		private float defeatLaunchTime = .5f;
		/// <summary> Direction to launch. Leave at Vector3.Zero to automatically calculate. </summary>
		private Vector3 defeatLaunchDirection;

		/// <summary> Responsible for handling tweens (i.e. Spawning/Default launching) </summary>
		private Tween tweener;

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
		}

		private float flameActiveTime = 1.0f;
		private float flameInactiveTime;
		/// <summary> Timer to keep track of flame cycles. </summary>
		private float flameTimer;
		/// <summary> Is the flame attack currently active? </summary>
		private bool isFlameActive;
		/// <summary> Timer to keep track of stagger length. </summary>
		private float staggerTimer;
		private const float STAGGER_LENGTH = 1.2f;

		/// <summary> Reference to the MovingObject.cs node being used. (Must be the direct parent of the Majin node.) </summary>
		private MovingObject movementController;

		// Animation parameters
		private readonly StringName IDLE_STATE = "idle";
		private readonly StringName SPIN_STATE = "spin";
		private readonly StringName FIRE_STATE = "fire";
		private readonly StringName STATE_REQUEST_PARAMETER = "parameters/state_transition/transition_request";

		private readonly StringName ENABLED_STATE = "enabled";
		private readonly StringName DISABLED_STATE = "disabled";
		private readonly StringName MOVE_TRANSITION_PARAMETER = "parameters/move_transition/transition_request";
		private readonly StringName TELEPORT_PARAMETER = "parameters/teleport_trigger/request";

		private const float MOVE_TRANSITION_LENGTH = .2f;

		protected override void SetUp()
		{
			if (Engine.IsEditorHint()) return; // In Editor

			animationTree.Active = true;
			animatorRoot = animationTree.TreeRoot as AnimationNodeBlendTree;
			moveTransition = animatorRoot.GetNode("move_transition") as AnimationNodeTransition;
			stateTransition = animatorRoot.GetNode("state_transition") as AnimationNodeTransition;
			fireState = (AnimationNodeStateMachinePlayback)animationTree.Get("parameters/fire_state/playback");
			spinState = (AnimationNodeStateMachinePlayback)animationTree.Get("parameters/spin_state/playback");

			if (attackType == AttackTypes.Spin) // Try to get movement controller parent
				movementController = GetParentOrNull<MovingObject>();

			base.SetUp();
		}

		public override void Respawn()
		{
			if (tweener != null) // Kill any active tweens
				tweener.Kill();

			base.Respawn();
			isSpawning = false;

			animationPlayer.Play("RESET");
			animationPlayer.Advance(0);

			// Reset rotation
			if (!trackPlayer && !Mathf.IsZeroApprox(rotationTime)) // Precalculate rotation amount
				rotationAmount = Mathf.Tau / rotationTime;

			rotationVelocity = 0;
			Vector2 spawnOffsetFlat = spawnOffset.Flatten();
			if (!SpawnTravelDisabled && !spawnOffsetFlat.IsZeroApprox())
				currentRotation = spawnOffsetFlat.AngleTo(Vector2.Up);
			else
				currentRotation = 0;
			UpdateRotation();

			// Reset idle movement
			idleFactorVelocity = 0;
			animationTree.Set(IDLE_FACTOR_PARAMETER, 0);
			animationTree.Set(DEFEAT_TRANSITION_PARAMETER, DISABLED_STATE);

			if (movementController != null) // Reset/Pause movement
			{
				movementController.Reset();
				movementController.TimeScale = 0;
			}

			// Reset stagger
			staggerTimer = 0;

			// Reset flame attack
			flameTimer = 0;
			isFlameActive = false;

			if (spawnMode == SpawnModes.Always ||
			(spawnMode == SpawnModes.Range && IsInRange)) // No activation trigger. Activate immediately.
				EnterRange();
			else // Start hidden
			{
				Visible = false;
				SetHitboxStatus(false);
			}
		}


		public override void TakePlayerDamage()
		{
			TakeDamage();
			base.TakePlayerDamage();

			if (!IsDefeated)
				animationPlayer.Play("stagger");
		}


		public override void TakeExternalDamage(int amount = -1)
		{
			TakeDamage();
			base.TakeExternalDamage(amount);
		}


		private void TakeDamage()
		{
			staggerTimer = STAGGER_LENGTH;

			if (isFlameActive)
				ToggleFlameAttack();

			animationTree.Set(HIT_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
		}


		protected override void Defeat()
		{
			base.Defeat();

			animationPlayer.Play("strike");
			if (isDefeatLaunchEnabled && !Mathf.IsZeroApprox(defeatLaunchTime))
			{
				if (tweener != null) // Kill any existing tween
					tweener.Kill();

				animationTree.Set(DEFEAT_TRANSITION_PARAMETER, ENABLED_STATE);
				animationTree.Set(HIT_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.FadeOut);

				Vector3 launchDirection = defeatLaunchDirection;
				if (launchDirection.IsEqualApprox(Vector3.Zero)) // Calculate launch direction
					launchDirection = (Character.Animator.Back() + Character.Animator.Up() * .2f).Normalized();
				else if (isDefeatLocalTransform)
					launchDirection = GlobalTransform.Basis * launchDirection;

				launchDirection = launchDirection.Rotated(Vector3.Up, Mathf.Pi); // Fix forward direction
				launchDirection = launchDirection.Normalized() * Mathf.Clamp(Character.MoveSpeed, 5, 20);

				Vector3 targetRotation = Vector3.Up * Mathf.Tau * 2.0f;
				if (Runtime.randomNumberGenerator.Randf() > .5f)
					targetRotation *= -1;

				targetRotation += Vector3.Left * Mathf.Pi * .25f;

				// Get knocked back
				tweener = CreateTween().SetParallel();
				tweener.TweenProperty(this, "global_position", GlobalPosition + launchDirection, defeatLaunchTime);
				tweener.TweenProperty(this, "rotation", Rotation + targetRotation, defeatLaunchTime * 2.0f).SetEase(Tween.EaseType.In);
				tweener.TweenCallback(Callable.From(() => animationPlayer.Play("despawn"))).SetDelay(defeatLaunchTime * .5f);
			}
			else
			{
				animationPlayer.Advance(0.0);
				animationPlayer.Play("explode");
			}
		}

		protected override void UpdateEnemy()
		{
			if (Engine.IsEditorHint()) return; // In Editor
			if (!IsActive) return; // Hasn't spawned yet
			if (IsDefeated) return; // Already defeated

			if (staggerTimer != 0)
			{
				staggerTimer = Mathf.MoveToward(staggerTimer, 0, PhysicsManager.physicsDelta);
				return;
			}

			if (attackType == AttackTypes.Fire)
				UpdateFlameAttack();

			// Update rotation
			if (IsInRange)
			{
				if (trackPlayer) // Rotate to face player
					TrackPlayer();
				else if (!Mathf.IsZeroApprox(rotationTime))
				{
					rotationVelocity = Mathf.Lerp(rotationVelocity, rotationAmount, TRACKING_SMOOTHING);
					currentRotation = ExtensionMethods.ModAngle(currentRotation + PhysicsManager.physicsDelta * rotationVelocity);
				}
			}
			else
				currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, 0, ref rotationVelocity, TRACKING_SMOOTHING);

			UpdateRotation();
			UpdateFidgets();
		}

		private float idleFactorVelocity;
		private const float IDLE_FACTOR_SMOOTHING = 30.0f; // Idle movement strength smoothing


		private bool isFidgetActive;
		private int fidgetIndex; // Index of the current fidget animation.
		private float fidgetTimer; // Keeps track of how long it's been since the last fidget

		private readonly StringName[] FIDGET_ANIMATIONS = {
			"flip",
			"fight"
		};

		private readonly StringName HIT_TRIGGER_PARAMETER = "parameters/hit_trigger/request";
		private readonly StringName DEFEAT_TRANSITION_PARAMETER = "parameters/defeat_transition/transition_request";

		private readonly StringName IDLE_FACTOR_PARAMETER = "parameters/idle_movement_factor/add_amount";
		private readonly StringName FIDGET_TRANSITION_PARAMETER = "parameters/fidget_transition/transition_request"; // Sets the fidget animation
		private readonly StringName FIDGET_TRIGGER_PARAMETER = "parameters/fidget_trigger/request"; // Currently fidgeting? Set StringName
		private readonly StringName FIDGET_TRIGGER_STATE_PARAMETER = "parameters/fidget_trigger/active"; // Get StringName
		private const float FIDGET_FREQUENCY = 3f; // How often to fidget

		/// <summary>
		/// Updates fidgets and idle movement.
		/// </summary>
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

				isFidgetActive = (bool)animationTree.Get(FIDGET_TRIGGER_STATE_PARAMETER);
			}
			else if (attackType != AttackTypes.Fire || !IsInRange) // Wait for fidget to start
			{
				fidgetTimer += PhysicsManager.physicsDelta;
				if (fidgetTimer > FIDGET_FREQUENCY) // Start fidget
				{
					fidgetTimer = 0; // Reset timer
					fidgetIndex = Runtime.randomNumberGenerator.RandiRange(0, FIDGET_ANIMATIONS.Length - 1);
					animationTree.Set(FIDGET_TRANSITION_PARAMETER, FIDGET_ANIMATIONS[fidgetIndex]);
					animationTree.Set(FIDGET_TRIGGER_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
					isFidgetActive = true;
				}
			}

			float idleFactor = (float)animationTree.Get(IDLE_FACTOR_PARAMETER);
			idleFactor = ExtensionMethods.SmoothDamp(idleFactor, targetIdleFactor, ref idleFactorVelocity, IDLE_FACTOR_SMOOTHING * PhysicsManager.physicsDelta);
			animationTree.Set(IDLE_FACTOR_PARAMETER, idleFactor);
		}

		private void UpdateRotation()
		{
			root.Rotation = new Vector3(root.Rotation.X, currentRotation, root.Rotation.Z);
			fireRoot.Rotation = Vector3.Up * currentRotation;
		}

		private void UpdateFlameAttack()
		{
			if (!IsInRange || isFidgetActive) // Out of range or fidget is active
			{
				if (isFlameActive)
					ToggleFlameAttack();

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
				flameTimer = Mathf.MoveToward(flameTimer, flameInactiveTime, PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(flameTimer, flameInactiveTime)) // Switch on
					ToggleFlameAttack();
			}
		}

		private void ToggleFlameAttack()
		{
			isFlameActive = !isFlameActive;

			if (isFlameActive) // Start fire attack
			{
				animationPlayer.Play("fire-start");
				fireState.Travel("attack-fire-start");
				stateTransition.XfadeTime = 0.1;
				animationTree.Set(STATE_REQUEST_PARAMETER, FIRE_STATE);
			}
			else // Stop flame attack
			{
				animationPlayer.Play("fire-end");
				animationPlayer.Advance(0.0);
				fireState.Travel("attack-fire-end");
				stateTransition.XfadeTime = 0.4;
				animationTree.Set(STATE_REQUEST_PARAMETER, IDLE_STATE);
			}

			flameTimer = 0; // Reset timer
		}

		protected override void EnterRange()
		{
			if (spawnMode == SpawnModes.Signal) return;
			Spawn();
		}

		protected override void Spawn()
		{
			if (isSpawning || IsActive) return;

			isSpawning = true;
			SetDeferred("visible", true);

			if (tweener != null)
				tweener.Kill();
			tweener = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

			animationTree.Set(STATE_REQUEST_PARAMETER, IDLE_STATE); // Idle

			if (SpawnTravelDisabled) // Spawn instantly
			{
				animationPlayer.Play("spawn");
				animationTree.Set(TELEPORT_PARAMETER, (int)AnimationNodeOneShot.OneShotRequest.Fire);
				tweener.TweenCallback(new Callable(this, MethodName.FinishSpawning)).SetDelay(.5f); // Delay by length of teleport animation
			}
			else // Travel
			{
				animationPlayer.Play("travel");
				GlobalPosition = OriginalPosition + SpawnOffset;
				tweener.TweenProperty(this, "position", OriginalPosition, spawnTravelTime).SetDelay(spawnDelay).From(GlobalPosition);
				tweener.TweenCallback(new Callable(this, MethodName.FinishSpawning)).SetDelay(spawnDelay + Mathf.Clamp(spawnTravelTime - MOVE_TRANSITION_LENGTH * .5f, 0, Mathf.Inf));

				moveTransition.XfadeTime = 0;
				if (!Mathf.IsZeroApprox(spawnOffset.X) || !Mathf.IsZeroApprox(spawnOffset.Z))
					animationTree.Set(MOVE_TRANSITION_PARAMETER, ENABLED_STATE); // Travel animation
				else
					animationTree.Set(MOVE_TRANSITION_PARAMETER, DISABLED_STATE); // Immediately idle
			}

			base.Spawn();
		}

		private void FinishSpawning()
		{
			IsActive = true;
			isSpawning = false;
			SetHitboxStatus(true);

			if (!SpawnTravelDisabled)
			{
				moveTransition.XfadeTime = MOVE_TRANSITION_LENGTH;
				animationTree.Set(MOVE_TRANSITION_PARAMETER, DISABLED_STATE); // Stopped moving
			}

			if (attackType == AttackTypes.Spin)
			{
				spinState.Travel("attack-spin-start");
				animationTree.Set(STATE_REQUEST_PARAMETER, SPIN_STATE);

				if (tweener != null)
					tweener.Kill();

				tweener = CreateTween();
				tweener.TweenCallback(new Callable(this, MethodName.OnSpinActivated)).SetDelay(0.3f); // Delay spin activation by windup animation length
			}
		}


		/// <summary>
		/// Called after spin attack's windup animation.
		/// </summary>
		private void OnSpinActivated()
		{
			animationPlayer.Play("spin");

			if (movementController != null) // Start movement
				movementController.TimeScale = 1;
		}
	}
}
