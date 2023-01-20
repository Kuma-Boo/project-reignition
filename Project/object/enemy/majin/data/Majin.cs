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
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Trigger Mode", Variant.Type.Int, PropertyHint.Enum, spawnMode.EnumToString()));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Travel Time", Variant.Type.Float, PropertyHint.Range, "0,2,.1"));
			if (!SpawnTravelDisabled)
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Offset", Variant.Type.Vector3));

			properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Track Player", Variant.Type.Bool));
			if (!trackPlayer)
				properties.Add(ExtensionMethods.CreateProperty("Rotation Settings/Rotation Time", Variant.Type.Float, PropertyHint.Range, "-5,5,.1"));

			properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Attack Type", Variant.Type.Int, PropertyHint.Enum, attackType.EnumToString()));
			if (attackType == AttackTypes.Fire) //Show relevant fire-related settings
			{
				properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Flame Active Time", Variant.Type.Float, PropertyHint.Range, "0.1,3,.1"));
				properties.Add(ExtensionMethods.CreateProperty("Attack Settings/Flame Inactive Time", Variant.Type.Float, PropertyHint.Range, "0,3,.1"));
			}

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Spawn Settings/Spawn Trigger Mode":
					return (int)spawnMode;
				case "Spawn Settings/Spawn Travel Time":
					return spawnTravelTime;
				case "Spawn Settings/Spawn Offset":
					return spawnOffset;

				case "Rotation Settings/Track Player":
					return trackPlayer;
				case "Rotation Settings/Rotation Time":
					return rotationTime;

				case "Attack Settings/Attack Type":
					return (int)attackType;
				case "Attack Settings/Flame Active Time":
					return (float)flameActiveTime;
				case "Attack Settings/Flame Inactive Time":
					return (float)flameInactiveTime;
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Spawn Settings/Spawn Trigger Mode":
					spawnMode = (SpawnModes)(int)value;
					NotifyPropertyListChanged();
					break;
				case "Spawn Settings/Spawn Travel Time":
					bool toggle = SpawnTravelDisabled != Mathf.IsZeroApprox((float)value);
					spawnTravelTime = (float)value;

					if (toggle)
						NotifyPropertyListChanged();
					break;
				case "Spawn Settings/Spawn Offset":
					spawnOffset = (Vector3)value;
					break;

				case "Rotation Settings/Track Player":
					trackPlayer = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Rotation Settings/Rotation Time":
					rotationTime = (float)value;
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
				default:
					return false;
			}

			return true;
		}
		#endregion

		[Export]
		private AnimationTree animationTree;
		[Export]
		private AnimationPlayer animationPlayer;
		[Export]
		private Node3D fireRoot;
		[Export]
		private Node3D root;
		private AnimationNodeBlendTree animatorRoot;
		private AnimationNodeTransition moveTransition;
		private AnimationNodeTransition stateTransition;
		private AnimationNodeStateMachinePlayback spinState;
		private AnimationNodeStateMachinePlayback fireState;

		private SpawnModes spawnMode;
		private enum SpawnModes
		{
			Internal, //Use Range trigger
			External, //External Signal
			Always, //Always spawned
		}

		private bool SpawnTravelDisabled => Mathf.IsZeroApprox(spawnTravelTime);
		/// <summary> How long does spawn traveling take? Set to 0 to spawn instantly. </summary>
		private float spawnTravelTime;
		/// <summary> Where to spawn from (Added with OriginalPosition) </summary>
		public Vector3 spawnOffset;
		/// <summary> Local Position to be after spawning is complete. </summary>
		private Vector3 OriginalPosition => SpawnData.spawnTransform.origin;
		private Vector3 SpawnPosition => OriginalPosition + Basis * spawnOffset;
		private bool isSpawned;
		private bool isSpawning;

		/// <summary> For the enemy to be launched a particular direction when defeated? </summary>
		[Export]
		private Vector3 defeatLaunchDirection;

		/// <summary> Responsible for handling tweens (i.e. Spawning/Default launching) </summary>
		private Tween tweener;

		/// <summary> Should this majin rotate to face the player? </summary>
		private bool trackPlayer = true;
		/// <summary> How long to complete a rotation cycle when trackPlayer is false. </summary>
		private float rotationTime;
		private float currentRotation;
		private float rotationVelocity;
		private const float ROTATION_SMOOTHING = .2f;

		private AttackTypes attackType;
		private enum AttackTypes
		{
			Disabled, //Just idle
			Spin, //Spin like a top
			Fire, //Spit fire out
		}

		private float flameActiveTime = 1.0f;
		private float flameInactiveTime;
		/// <summary> Timer to keep track of flame cycles. </summary>
		private float flameTimer;
		/// <summary> Is the flame attack currently active? </summary>
		private bool isFlameActive;

		/// <summary> Reference to the MovingObject.cs node being used. (Must be the direct parent of the Majin node.) </summary>
		private MovingObject movementController;

		[Export]
		private bool log;

		//Animation parameters
		private const float MOVE_TRANSITION_LENGTH = .2f;
		private readonly StringName SEEK_PARAMETER = "parameters/seek/seek_position";
		private readonly StringName STATE_TRANSITION_PARAMETER = "parameters/state_transition/current";
		private readonly StringName MOVE_TRANSITION_PARAMETER = "parameters/move_transition/current";
		private readonly StringName TELEPORT_PARAMETER = "parameters/teleport_trigger/active";

		protected override void SetUp()
		{
			if (Engine.IsEditorHint()) return; //In Editor

			animationTree.Active = true;
			animatorRoot = animationTree.TreeRoot as AnimationNodeBlendTree;
			moveTransition = animatorRoot.GetNode("move_transition") as AnimationNodeTransition;
			stateTransition = animatorRoot.GetNode("state_transition") as AnimationNodeTransition;
			fireState = (AnimationNodeStateMachinePlayback)animationTree.Get("parameters/fire_state/playback");
			spinState = (AnimationNodeStateMachinePlayback)animationTree.Get("parameters/spin_state/playback");

			root.Rotation = Vector3.Right * Mathf.Pi * .5f; //Required for animation retargeting

			if (attackType == AttackTypes.Spin) //Try to get movement controller parent
				movementController = GetParentOrNull<MovingObject>();

			base.SetUp();
		}

		public override void Respawn()
		{
			if (tweener != null) //Kill any active tweens
				tweener.Kill();

			isSpawned = false;
			isSpawning = false;
			SpawnData.Respawn(this);
			currentHealth = maxHealth;

			animationPlayer.Play("RESET");
			animationPlayer.Advance(0);

			//Reset rotation
			if (!trackPlayer && !Mathf.IsZeroApprox(rotationTime)) //Precalculate rotation velocity
				rotationVelocity = Mathf.Tau / rotationTime;
			else
				rotationVelocity = 0;

			Vector2 spawnOffsetFlat = spawnOffset.Flatten();
			if (!SpawnTravelDisabled && !spawnOffsetFlat.IsZeroApprox())
				currentRotation = spawnOffsetFlat.AngleTo(Vector2.Up);
			else
				currentRotation = 0;
			UpdateRotation();

			if (movementController != null) //Reset/Pause movement
			{
				movementController.Reset();
				movementController.TimeScale = 0;
			}

			//Reset flame attack
			flameTimer = 0;
			isFlameActive = false;

			if (spawnMode == SpawnModes.Always ||
			(spawnMode == SpawnModes.Internal && IsActivated)) //No activation trigger. Activate immediately.
				Activate();
			else //Start hidden
			{
				Visible = false;
				PhysicsMonitoring = false;
			}
		}

		protected override void Defeat()
		{
			base.Defeat();

			//TODO Create pearls

			if (!defeatLaunchDirection.IsEqualApprox(Vector3.Zero))
			{
				if (tweener != null) //Kill any existing tween
					tweener.Kill();

				//Get knocked back
				tweener = CreateTween().SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
				tweener.TweenProperty(this, "global_transform:origin", GlobalPosition + defeatLaunchDirection, .5f);
				tweener.TweenCallback(new Callable(this, MethodName.Despawn)).SetDelay(.5f);
			}
			else
				Despawn();
		}

		protected override void UpdateEnemy()
		{
			if (Engine.IsEditorHint()) return; //In Editor
			if (!isSpawned) return; //Hasn't spawned yet
			if (attackType == AttackTypes.Spin) return;

			if (attackType == AttackTypes.Fire)
				UpdateFlameAttack();

			//Update rotation
			if (trackPlayer) //Rotate to face player
			{
				float targetRotation = ExtensionMethods.Flatten(GlobalPosition - Character.GlobalPosition).AngleTo(Vector2.Up);
				targetRotation -= GlobalRotation.y; //Rotation is in local space
				currentRotation = ExtensionMethods.SmoothDampAngle(currentRotation, targetRotation, ref rotationVelocity, ROTATION_SMOOTHING);
			}
			else if (!Mathf.IsZeroApprox(rotationTime))
				currentRotation = ExtensionMethods.ModAngle(currentRotation + PhysicsManager.physicsDelta * rotationVelocity);

			UpdateRotation();

			if (!IsActivated || attackType == AttackTypes.Disabled)
			{
				//TODO Update idle animations
			}
		}

		private void UpdateRotation()
		{
			root.Rotation = new Vector3(root.Rotation.x, currentRotation, root.Rotation.z);
			fireRoot.Rotation = Vector3.Up * currentRotation;
		}

		private void UpdateFlameAttack()
		{
			if (!IsActivated) //Out of range
			{
				flameTimer = 0;
				if (isFlameActive)
					ToggleFlameAttack();

				return;
			}

			if (Mathf.IsZeroApprox(flameInactiveTime) && isFlameActive) return; //Always activated

			if (isFlameActive)
			{
				flameTimer = Mathf.MoveToward(flameTimer, flameActiveTime, PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(flameTimer, flameActiveTime)) //Switch off
					ToggleFlameAttack();
			}
			else
			{
				flameTimer = Mathf.MoveToward(flameTimer, flameInactiveTime, PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(flameTimer, flameInactiveTime)) //Switch on
					ToggleFlameAttack();
			}
		}

		private void ToggleFlameAttack()
		{
			isFlameActive = !isFlameActive;

			if (isFlameActive) //Start fire attack
			{
				animationPlayer.Play("fire-start");
				fireState.Travel("attack-fire-start");
				stateTransition.XfadeTime = 0.1;
				animationTree.Set(STATE_TRANSITION_PARAMETER, 2);
			}
			else //Stop flame attack
			{
				animationPlayer.Play("fire-end");
				fireState.Travel("attack-fire-end");
				stateTransition.XfadeTime = 0;
			}

			flameTimer = 0; //Reset timer
		}

		protected override void Activate()
		{
			if (spawnMode == SpawnModes.External) return;
			Spawn();
		}

		/// <summary>
		/// Overload function to allow using Godot's built-in Area3D.OnEntered(Area3D area) signal.
		/// </summary>
		private void Spawn(Area3D _) => Spawn();
		private void Spawn()
		{
			if (isSpawning || isSpawned) return;

			isSpawning = true;
			SetDeferred("visible", true);

			if (tweener != null)
				tweener.Kill();
			tweener = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);

			if (SpawnTravelDisabled) //Spawn instantly
			{
				animationPlayer.Play("spawn");
				animationTree.Set(TELEPORT_PARAMETER, true);

				tweener.TweenCallback(new Callable(this, MethodName.FinishSpawning)).SetDelay(1.0f); //Delay by length of teleport animation
			}
			else //Travel
			{
				GlobalPosition = SpawnPosition;
				tweener.TweenProperty(this, "transform:origin", OriginalPosition, spawnTravelTime).From(SpawnPosition);
				tweener.TweenCallback(new Callable(this, MethodName.FinishSpawning)).SetDelay(Mathf.Clamp(spawnTravelTime - MOVE_TRANSITION_LENGTH * .5f, 0, Mathf.Inf));

				moveTransition.XfadeTime = 0;
				if (!Mathf.IsZeroApprox(spawnOffset.x) || !Mathf.IsZeroApprox(spawnOffset.z))
					animationTree.Set(MOVE_TRANSITION_PARAMETER, 0); //Travel animation
				else
					animationTree.Set(MOVE_TRANSITION_PARAMETER, 1); //Immediately idle
			}
		}

		private void FinishSpawning()
		{
			isSpawning = false;
			isSpawned = true;
			PhysicsMonitoring = true;

			moveTransition.XfadeTime = MOVE_TRANSITION_LENGTH;
			animationTree.Set(MOVE_TRANSITION_PARAMETER, 1); //Stopped moving

			if (attackType == AttackTypes.Spin)
			{
				spinState.Travel("attack-spin-start");
				animationTree.Set(STATE_TRANSITION_PARAMETER, 1);

				if (tweener != null)
					tweener.Kill();

				tweener = CreateTween();
				tweener.TweenCallback(new Callable(this, MethodName.OnSpinActivated)).SetDelay(0.3f); //Delay spin activation by windup animation length
			}
			else
				animationTree.Set(STATE_TRANSITION_PARAMETER, 0); //Idle
		}

		/// <summary>
		/// Called after spin attack's windup animation.
		/// </summary>
		private void OnSpinActivated()
		{
			animationPlayer.Play("spin");

			if (movementController != null) //Start movement
				movementController.TimeScale = 1;
		}
	}
}
