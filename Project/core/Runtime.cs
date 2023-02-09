using Godot;
using Godot.Collections;

namespace Project.Core
{
	public partial class Runtime : Node
	{
		public static Runtime Instance;

		public static RandomNumberGenerator randomNumberGenerator = new RandomNumberGenerator();

		public static readonly Vector2I SCREEN_SIZE = new Vector2I(1920, 1080); //Working resolution is 1080p
		public static readonly Vector2I HALF_SCREEN_SIZE = (Vector2I)((Vector2)SCREEN_SIZE * .5f);

		public override void _EnterTree()
		{
			Instance = this;
			Interface.Menus.Menu.SetUpMemory();
			SetUpPearls();
		}

		public override void _Process(double _)
		{
			UpdateShaderTime();

			if (SaveManager.ActiveGameData != null)
				SaveManager.ActiveGameData.playTime = Mathf.MoveToward(SaveManager.ActiveGameData.playTime, SaveManager.MAX_PLAY_TIME, PhysicsManager.normalDelta);
		}

		[Export(PropertyHint.Layers3DPhysics)]
		public uint environmentMask;
		[Export(PropertyHint.Layers3DPhysics)]
		public uint particleCollisionLayer; //Collision layer for destructable particle effects
		[Export(PropertyHint.Layers3DPhysics)]
		public uint particleCollisionMask; //Collision mask for destructable particle effects

		public static readonly float GRAVITY = 28.0f;
		public static readonly float MAX_GRAVITY = -48.0f;
		public static float GetJumpPower(float height) => Mathf.Sqrt(2 * Runtime.GRAVITY * height);

		private float shaderTime;
		private const float SHADER_ROLLOVER = 3600f;
		private readonly StringName SHADER_GLOBAL_TIME = new StringName("time");
		private void UpdateShaderTime()
		{
			shaderTime += PhysicsManager.normalDelta;
			if (shaderTime > SHADER_ROLLOVER)
				shaderTime -= SHADER_ROLLOVER; //Copied from original shader time's rollover
			RenderingServer.GlobalShaderParameterSet(SHADER_GLOBAL_TIME, shaderTime);
		}

		#region Pearl Stuff
		public SphereShape3D PearlCollisionShape = new SphereShape3D();
		public SphereShape3D RichPearlCollisionShape = new SphereShape3D();
		[Export]
		public AnimatedTexture pearlTexture;
		[Export]
		public AnimatedTexture richPearlTexture;
		[Export]
		public PackedScene pearlScene;

		/// <summary> Pool of auto-collected pearls used whenever enemies are defeated or itemboxes are opened. </summary>
		private readonly Array<Gameplay.Objects.Pearl> pearlPool = new Array<Gameplay.Objects.Pearl>();
		private int PEARL_POOL_SIZE = 100; //How many pearls to pool

		private const float PEARL_NORMAL_COLLISION = .4f;
		private const float RICH_PEARL_NORMAL_COLLISION = .6f;
		public void UpdatePearlCollisionShapes(float sizeMultiplier = 1f)
		{
			PearlCollisionShape.Radius = PEARL_NORMAL_COLLISION * sizeMultiplier;
			RichPearlCollisionShape.Radius = RICH_PEARL_NORMAL_COLLISION * sizeMultiplier;
		}

		private void SetUpPearls()
		{
			for (int i = 0; i < PEARL_POOL_SIZE; i++)
				pearlPool.Add(SpawnPearl());
		}

		private Gameplay.Objects.Pearl SpawnPearl()
		{
			Gameplay.Objects.Pearl pearl = pearlScene.Instantiate<Gameplay.Objects.Pearl>();
			pearl.DisableAutoRespawning = true; //Don't auto-respawn
			pearl.Monitoring = pearl.Monitorable = false; //Unlike normal pearls, these are automatically collected
			pearl.Connect(Gameplay.Objects.Pearl.SignalName.Despawned, Callable.From(() => pearlPool.Add(pearl)));
			return pearl;
		}

		private const float PEARL_MIN_TRAVEL_TIME = .2f;
		private const float PEARL_MAX_TRAVEL_TIME = .3f;
		public void SpawnPearls(int amount, Vector3 spawnPosition, Vector2 radius)
		{
			GD.Print($"Spawned {amount} pearls.");
			Tween tween = CreateTween().SetParallel(true);

			for (int i = 0; i < amount; i++)
			{
				Gameplay.Objects.Pearl pearl;

				if (pearlPool.Count != 0)
				{
					pearl = pearlPool[0];
					pearlPool.RemoveAt(0);
				}
				else
					pearl = SpawnPearl(); //In the rare case where pearlPool is empty

				AddChild(pearl);
				pearl.Respawn();

				Vector3 spawnOffset = new Vector3(randomNumberGenerator.RandfRange(-radius.X, radius.X),
					randomNumberGenerator.RandfRange(-radius.Y, radius.Y),
					randomNumberGenerator.RandfRange(-radius.X, radius.X));

				float travelTime = randomNumberGenerator.RandfRange(PEARL_MIN_TRAVEL_TIME, PEARL_MAX_TRAVEL_TIME);
				tween.TweenProperty(pearl, "global_position", spawnPosition + spawnOffset, travelTime).From(spawnPosition);
				tween.TweenCallback(new Callable(pearl, Gameplay.Objects.Pickup.MethodName.Collect)).SetDelay(travelTime);
			}

			tween.Play();
			tween.Connect(Tween.SignalName.Finished, Callable.From(() => tween.Kill())); //Kill tween after completing
		}
		#endregion
	}
}