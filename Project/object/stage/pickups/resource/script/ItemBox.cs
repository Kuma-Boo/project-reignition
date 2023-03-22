using Godot;
using Godot.Collections;
using System.Collections.Generic;
using Project.Core;

namespace Project.Gameplay.Objects
{
	[Tool]
	public partial class ItemBox : Pickup
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Amount", Variant.Type.Int, PropertyHint.Range, "1,100"));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Time", Variant.Type.Float, PropertyHint.Range, "0.2,2,0.1"));

			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Pearls", Variant.Type.Bool));

			if (!spawnPearls)
			{
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Height", Variant.Type.Float, PropertyHint.Range, "0,20,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Object", Variant.Type.Object, PropertyHint.ResourceType, "PackedScene"));
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Pickup Parent", Variant.Type.NodePath));

				if (spawnAmount > 1)
				{
					properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Delay", Variant.Type.Float, PropertyHint.Range, "0,2,0.1"));
					properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Spawn Radius", Variant.Type.Float, PropertyHint.Range, "0,5,0.1"));
				}
			}

			return properties;
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Spawn Settings/Spawn Pearls":
					spawnPearls = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Spawn Settings/Amount":
					spawnAmount = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Spawn Settings/Travel Time":
					travelTime = (float)value;
					break;
				case "Spawn Settings/Travel Height":
					travelHeight = (float)value;
					break;
				case "Spawn Settings/Travel Delay":
					travelDelay = (float)value;
					break;
				case "Spawn Settings/Pickup Parent":
					pickupParentPath = (NodePath)value;
					pickupParent = GetNodeOrNull<Node3D>(pickupParentPath);
					break;
				case "Spawn Settings/Spawn Radius":
					spawnRadius = (float)value;
					break;
				case "Spawn Settings/Object":
					customObject = (PackedScene)value;
					break;

				default:
					return false;
			}

			return true;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Spawn Settings/Spawn Pearls":
					return spawnPearls;
				case "Spawn Settings/Amount":
					return spawnAmount;
				case "Spawn Settings/Travel Time":
					return travelTime;
				case "Spawn Settings/Travel Height":
					return travelHeight;
				case "Spawn Settings/Travel Delay":
					return travelDelay;
				case "Spawn Settings/Pickup Parent":
					return pickupParentPath;
				case "Spawn Settings/Spawn Radius":
					return spawnRadius;
				case "Spawn Settings/Object":
					return customObject;
			}
			return base._Get(property);
		}
		#endregion

		/// <summary> Use RuntimeConstants method for pearls? </summary>
		public bool spawnPearls;
		/// <summary> Scene to spawn when spawning custom object. </summary>
		private PackedScene customObject;

		/// <summary> Position to spawn nodes. </summary>
		private NodePath pickupParentPath;
		private Node3D pickupParent;

		/// <summary> How many objects to spawn. </summary>
		public int spawnAmount = 1;
		/// <summary> How long to travel for. </summary>
		private float travelTime = 1;
		/// <summary> How high to travel. </summary>
		private float travelHeight = 2;
		/// <summary> Maximum amount to delay travel by. </summary>
		private float travelDelay;


		private Vector3 LaunchPosition => GlobalPosition + Vector3.Up * .5f;
		public Vector3 EndPosition => pickupParent == null ? GlobalPosition : pickupParent.GlobalPosition;

		public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(LaunchPosition, EndPosition, travelHeight);

		//Only effective if spawnAmount is greater than 1
		/// <summary> How wide of a ring to create. </summary>
		public float spawnRadius;
		/// <summary> Pre-calculated rotation interval. </summary>
		private float rotationInterval;
		private Vector3 GetSpawnOffset(int i)
		{
			if (spawnAmount == 1) return Vector3.Zero;
			return this.Forward().Rotated(this.Up(), i * rotationInterval) * spawnRadius;
		}

		[Export]
		private AnimationPlayer animator;

		private bool isOpened;
		private bool isMovingObjects;

		private readonly Vector2 PEARL_SPAWN_RADIUS = new Vector2(2.0f, 1.0f);

		//Godot doesn't support listing custom structs, so System.Collections.Generic.List is used instead.
		private readonly List<float> travelTimes = new List<float>();
		private readonly List<Pickup> objectPool = new List<Pickup>();
		private readonly List<LaunchSettings> objectLaunchSettings = new List<LaunchSettings>();

		protected override void SetUp()
		{
			pickupParent = GetNodeOrNull<Node3D>(pickupParentPath);

			if (Engine.IsEditorHint()) return;

			rotationInterval = Mathf.Tau / spawnAmount;

			if (!spawnPearls)
			{
				if (customObject == null)
					GD.PrintErr("Item box can't spawn anything!");

				if (pickupParent == null)
					GD.PrintErr("spawnNode is null!");

				//Pool objects
				for (int i = 0; i < spawnAmount; i++)
				{
					Pickup pickup = customObject.Instantiate<Pickup>();
					pickup.DisableAutoRespawning = true;

					travelTimes.Add(0);
					objectPool.Add(pickup);
					objectLaunchSettings.Add(new LaunchSettings());

					pickupParent.AddChild(pickup);
				}

				//Disable node parent
				pickupParent.Visible = false;
				pickupParent.ProcessMode = ProcessModeEnum.Disabled;
			}

			base.SetUp();
			Level.ConnectUnloadSignal(this);
		}

		public override void Unload() //Prevent memory leak
		{
			for (int i = objectPool.Count - 1; i >= 0; i--)
				objectPool[i].QueueFree();

			objectPool.Clear();
			objectLaunchSettings.Clear();

			base.Unload();
		}

		public override void Respawn()
		{
			base.Respawn();

			isOpened = false;
			isMovingObjects = false;

			animator.Play("RESET");
			animator.Seek(0, true);

			for (int i = 0; i < objectPool.Count; i++)
				objectPool[i].Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint() || !IsVisibleInTree()) return;

			if (spawnPearls) return;

			if (isOpened && isMovingObjects) //Interpolate objects
				UpdateObjects();
		}

		private void UpdateObjects()
		{
			bool isMovementComplete = true;

			for (int i = 0; i < objectPool.Count; i++)
			{
				travelTimes[i] += PhysicsManager.physicsDelta;
				float currentTime = Mathf.Clamp(travelTimes[i], 0, travelTime) / travelTime;
				if (currentTime < 1f)
					isMovementComplete = false; //Still moving objects

				if (!objectPool[i].IsInsideTree()) continue; //Already collected?

				objectPool[i].Visible = !Mathf.IsZeroApprox(currentTime);
				objectPool[i].Monitoring = objectPool[i].Monitorable = currentTime >= 1f;

				if (objectPool[i].Visible)
				{
					if (!objectLaunchSettings[i].IsInitialized)
						objectLaunchSettings[i] = LaunchSettings.Create(LaunchPosition, EndPosition + GetSpawnOffset(i), travelHeight);

					objectPool[i].GlobalPosition = objectLaunchSettings[i].InterpolatePositionRatio(currentTime);
					objectPool[i].Scale = Vector3.One * Mathf.Clamp(currentTime, 0.5f, 1f);
				}
			}

			isMovingObjects = !isMovementComplete;
		}

		protected override void Collect()
		{
			base.Collect();

			if (Character.ActionState == CharacterController.ActionStates.JumpDash)
				Character.Lockon.StartBounce();

			animator.Play("open");
			isOpened = true;

			if (spawnPearls)
			{
				Runtime.Instance.SpawnPearls(spawnAmount, GlobalPosition, PEARL_SPAWN_RADIUS, 2.0f);
				return;
			}

			isMovingObjects = true;

			pickupParent.Visible = true;
			pickupParent.ProcessMode = ProcessModeEnum.Inherit;

			//Spawn objects
			for (int i = 0; i < objectPool.Count; i++)
			{
				travelTimes[i] = Runtime.randomNumberGenerator.RandfRange(-travelDelay, 0);
				objectPool[i].Monitoring = objectPool[i].Monitorable = false; //Disable collision temporarily
				objectLaunchSettings[i] = new LaunchSettings(); //Set as uninitialized LaunchSettings
				CallDeferred(MethodName.UpdateObjects);
			}
		}
	}
}
