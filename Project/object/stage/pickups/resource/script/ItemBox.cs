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

			properties.Add(ExtensionMethods.CreateProperty("Auto Collect", Variant.Type.Bool));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Amount", Variant.Type.Int, PropertyHint.Range, "1,100"));
			properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Travel Time", Variant.Type.Float, PropertyHint.Range, "0.2,2,0.1"));

			if (!autoCollect)
			{
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Arc Height", Variant.Type.Float, PropertyHint.Range, "0,20,0.1"));
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Position", Variant.Type.Vector3));

				//if (spawnAmount > 1)
				//properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Time Offset", Variant.Type.Float, PropertyHint.Range, "0,2,0.1"));
			}

			if (spawnAmount > 1)
				properties.Add(ExtensionMethods.CreateProperty("Spawn Settings/Radius", Variant.Type.Float, PropertyHint.Range, "0,2,0.1"));

			return properties;
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Auto Collect":
					autoCollect = (bool)value;
					NotifyPropertyListChanged();
					break;
				case "Spawn Settings/Amount":
					spawnAmount = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Spawn Settings/Travel Time":
					travelTime = (float)value;
					break;
				case "Spawn Settings/Arc Height":
					arcHeight = (float)value;
					break;
				case "Spawn Settings/Position":
					spawnPosition = (Vector3)value;
					break;
				case "Spawn Settings/Radius":
					spawnRadius = (float)value;
					break;
				//case "Spawn Settings/Time Offset":
				//timeOffset = (float)value;
				//break;

				default:
					return false;
			}

			return true;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Auto Collect":
					return autoCollect;
				case "Spawn Settings/Amount":
					return spawnAmount;
				case "Spawn Settings/Travel Time":
					return travelTime;
				case "Spawn Settings/Arc Height":
					return arcHeight;
				case "Spawn Settings/Position":
					return spawnPosition;
				case "Spawn Settings/Radius":
					return spawnRadius;
					//case "Spawn Settings/Time Offset":
					//return timeOffset;
			}
			return base._Get(property);
		}
		#endregion

		[Export]
		private PackedScene targetObject;
		public bool autoCollect; //For pearls
		public int spawnAmount = 1; //How many objects to spawn
		private float travelTime = 1;
		private float arcHeight;
		private Vector3 spawnPosition; //Relative to ItemBox's orientation

		private Vector3 LaunchPosition => GlobalPosition + Vector3.Up * .5f;
		public Vector3 EndPosition => LaunchPosition + GlobalTransform.basis * spawnPosition;

		public LaunchData GetLaunchData() => LaunchData.Create(LaunchPosition, EndPosition, arcHeight);

		//Only effective if spawnAmount is greater than 1
		//private float timeOffset; //How much to offset each object when traveling
		public float spawnRadius; //How wide of a ring to create
		private float rotationInterval;
		private Vector3 GetSpawnOffset(int i)
		{
			if (spawnAmount == 1) return Vector3.Zero;
			return this.Forward().Rotated(this.Up(), i * rotationInterval);
		}

		[ExportGroup("Item Box Settings")]
		[Export]
		private bool isFlying;
		[Export]
		private Node3D rootPosition;
		[Export]
		private Node3D wings;
		[Export]
		private Node3D chest;
		[Export]
		private AnimationPlayer animator;

		private bool isOpened;
		private bool isMovingObjects;
		private float currentTravelTime;

		//Godot doesn't support listing custom structs, so System.Collections.Generic.List is used instead.
		private readonly List<Pickup> objectPool = new List<Pickup>();
		private readonly List<LaunchData> objectLaunchData = new List<LaunchData>();

		protected override void SetUp()
		{
			if (Engine.IsEditorHint()) return;

			wings.Visible = isFlying;
			rotationInterval = Mathf.Tau / spawnAmount;

			if (targetObject != null)
			{
				//Pool objects
				for (int i = 0; i < spawnAmount; i++)
				{
					Pickup pickup = targetObject.Instantiate<Pickup>();

					pickup.DisableAutoRespawning = true;
					objectPool.Add(pickup);
					objectLaunchData.Add(LaunchData.Create(LaunchPosition, EndPosition + GetSpawnOffset(i), arcHeight));
				}
			}
			else
				GD.PrintErr("Item box can't spawn anything!");

			base.SetUp();
		}

		public override void Respawn()
		{
			base.Respawn();

			isOpened = false;
			isMovingObjects = false;
			currentTravelTime = 0;
			animator.Play("RESET");

			for (int i = 0; i < objectPool.Count; i++)
				objectPool[i].Respawn();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Engine.IsEditorHint() || !IsVisibleInTree()) return;

			if (isFlying)
				chest.GlobalPosition = rootPosition.GlobalPosition;

			if (isOpened && isMovingObjects) //Interpolate objects
			{
				currentTravelTime += PhysicsManager.physicsDelta;
				float t = Mathf.Clamp(currentTravelTime, 0, travelTime) / travelTime;
				isMovingObjects = t < 1f;

				for (int i = 0; i < objectPool.Count; i++)
				{
					if (!objectPool[i].IsInsideTree()) continue; //Already collected?

					objectPool[i].GlobalPosition = objectLaunchData[i].InterpolatePositionRatio(t);
					objectPool[i].Scale = Vector3.One * t;
					objectPool[i].Monitoring = objectPool[i].Monitorable = t >= 1f;

					if (autoCollect && t >= 1f)
						objectPool[i].CallDeferred(Pickup.MethodName.Collect);
				}
			}
		}

		protected override void Collect()
		{
			if (Character.ActionState == CharacterController.ActionStates.JumpDash)
				Character.Lockon.StartBounce();

			animator.Play("open");
			isOpened = true;
			isMovingObjects = true;
			currentTravelTime = 0;

			//Spawn objects
			for (int i = 0; i < objectPool.Count; i++)
			{
				AddChild(objectPool[i]);
				objectPool[i].Monitoring = objectPool[i].Monitorable = false; //Disable collision temporarily
				objectPool[i].SetDeferred("global_position", LaunchPosition);
			}

			base.Collect();
		}
	}
}
