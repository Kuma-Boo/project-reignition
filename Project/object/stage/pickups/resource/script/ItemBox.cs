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
			Array<Dictionary> properties = new()
			{
				ExtensionMethods.CreateProperty("Spawn Settings/Spawn Pearls", Variant.Type.Bool)
			};

			if (!spawnPearls)
			{
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Pickup Parent", Variant.Type.NodePath));

				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Time", Variant.Type.Float, PropertyHint.Range, "0.2,2,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Delay", Variant.Type.Float, PropertyHint.Range, "0,2,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Delay Range", Variant.Type.Float, PropertyHint.Range, "0,2,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Height", Variant.Type.Float, PropertyHint.Range, "0,20,0.1"));
			}
			else
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Amount", Variant.Type.Int, PropertyHint.Range, "1,100"));

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
				case "Spawn Settings/Travel Delay Range":
					travelDelayRange = (float)value;
					break;
				case "Spawn Settings/Pickup Parent":
					pickupParentPath = (NodePath)value;
					pickupParent = GetNodeOrNull<Node3D>(pickupParentPath);
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
				case "Spawn Settings/Travel Delay Range":
					return travelDelayRange;
				case "Spawn Settings/Pickup Parent":
					return pickupParentPath;
			}
			return base._Get(property);
		}
		#endregion

		/// <summary> Use RuntimeConstants method for pearls? </summary>
		public bool spawnPearls;

		/// <summary> Position to spawn nodes. </summary>
		private NodePath pickupParentPath;
		private Node3D pickupParent;
		private void DisablePickupParent()
		{
			if (pickupParent != null) // Disable node parent
			{
				pickupParent.Visible = false;
				pickupParent.ProcessMode = ProcessModeEnum.Disabled;
			}
		}

		/// <summary> How many objects to spawn. </summary>
		public int spawnAmount = 1;
		/// <summary> How long to travel for. </summary>
		private float travelTime = 1;
		/// <summary> Travel delay for object spawning. </summary>
		private float travelDelay;
		/// <summary> Travel delay range for object spawning. </summary>
		private float travelDelayRange;
		/// <summary> How high to travel. </summary>
		private float travelHeight = 2;

		private Vector3 SpawnPosition => GlobalPosition + SPAWN_OFFSET;
		public Vector3 EndPosition => pickupParent == null ? GlobalPosition : pickupParent.GlobalPosition;

		public LaunchSettings GetLaunchSettings() => LaunchSettings.Create(SpawnPosition, EndPosition, travelHeight);


		[Export]
		private AnimationPlayer animator;

		private bool isOpened;
		private bool isMovingObjects;

		// Godot doesn't support listing custom structs, so System.Collections.Generic.List is used instead.
		private readonly List<float> travelTimes = new();
		private readonly List<Pickup> objectPool = new();
		private readonly List<LaunchSettings> objectLaunchSettings = new();

		private readonly Vector3 SPAWN_OFFSET = Vector3.Up * .5f;
		private readonly Vector2 PEARL_SPAWN_RADIUS = new(2.0f, 1.0f);

		protected override void SetUp()
		{
			pickupParent = GetNodeOrNull<Node3D>(pickupParentPath);

			if (Engine.IsEditorHint()) return;

			if (!spawnPearls)
			{
				if (pickupParent == null)
					GD.PrintErr($"Pickup parent is null on {Name}! Did you mean to turn spawnPearls on?");

				// Pool objects
				if (pickupParent is Pickup)
					PoolPickup(pickupParent as Pickup);
				else
				{
					for (int i = 0; i < pickupParent.GetChildCount(); i++)
						PoolPickup(pickupParent.GetChildOrNull<Pickup>(i));
				}
			}

			base.SetUp();
			DisablePickupParent(); // Attempt to disable the pickup parent
		}


		public override void Unload() // Prevent memory leak
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

			DisablePickupParent();

			for (int i = 0; i < objectPool.Count; i++)
				objectPool[i].Respawn();
		}


		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint() || !IsVisibleInTree()) return;

			if (spawnPearls) return;

			if (isOpened && isMovingObjects) // Interpolate objects
				UpdateObjects();
		}


		private void PoolPickup(Pickup pickup)
		{
			if (pickup == null) return;

			pickup.DisableAutoRespawning = true;
			pickup._Ready();

			travelTimes.Add(0);
			objectPool.Add(pickup);
			objectLaunchSettings.Add(new LaunchSettings());
		}


		private void UpdateObjects()
		{
			bool isMovementComplete = true;

			for (int i = 0; i < objectPool.Count; i++)
			{
				travelTimes[i] += PhysicsManager.physicsDelta;
				float currentTime = Mathf.Clamp(travelTimes[i], 0, travelTime) / travelTime;
				if (currentTime < 1f)
					isMovementComplete = false; // Still moving objects

				if (!objectPool[i].IsInsideTree()) continue; // Already collected?

				objectPool[i].Visible = !Mathf.IsZeroApprox(currentTime);
				objectPool[i].Monitoring = objectPool[i].Monitorable = currentTime >= 1f;

				if (objectPool[i].Visible)
				{
					if (!objectLaunchSettings[i].IsInitialized)
					{
						Vector3 startPosition = GlobalTransform.Basis.Inverse() * (SpawnPosition - EndPosition);
						if (objectPool[i] == pickupParent)
							startPosition = SPAWN_OFFSET;
						objectLaunchSettings[i] = LaunchSettings.Create(startPosition, objectPool[i].Position, travelHeight);
					}

					objectPool[i].Position = objectLaunchSettings[i].InterpolatePositionRatio(currentTime);
					objectPool[i].Scale = Vector3.One * Mathf.Clamp(currentTime, 0.5f, 1f);
				}
			}

			isMovingObjects = !isMovementComplete;
		}

		protected override void Collect()
		{
			base.Collect();

			if (Player.IsJumpDashOrHomingAttack)
				Player.StartBounce();

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

			// Spawn objects
			for (int i = 0; i < objectPool.Count; i++)
			{
				travelTimes[i] = Runtime.randomNumberGenerator.RandfRange(-travelDelayRange, 0) - travelDelay;
				objectPool[i].Monitoring = objectPool[i].Monitorable = false; // Disable collision temporarily
				objectLaunchSettings[i] = new(); // Set as uninitialized LaunchSettings
				CallDeferred(MethodName.UpdateObjects);
			}
		}
	}
}
