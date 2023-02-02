using Godot;

namespace Project.Core
{
	public partial class RuntimeConstants : Node
	{
		public static RuntimeConstants Instance;

		public static RandomNumberGenerator randomNumberGenerator = new RandomNumberGenerator();

		public static readonly Vector2I SCREEN_SIZE = new Vector2I(1920, 1080); //Working resolution is 1080p
		public static readonly Vector2I HALF_SCREEN_SIZE = (Vector2I)((Vector2)SCREEN_SIZE * .5f);

		public override void _EnterTree() => Instance = this;
		public override void _Process(double _) => UpdateShaderTime();

		[Export(PropertyHint.Layers3DPhysics)]
		public uint environmentMask;
		[Export(PropertyHint.Layers3DPhysics)]
		public uint particleCollisionLayer; //Collision layer for destructable particle effects
		[Export(PropertyHint.Layers3DPhysics)]
		public uint particleCollisionMask; //Collision mask for destructable particle effects

		public static readonly float GRAVITY = 28.0f;
		public static readonly float MAX_GRAVITY = -48.0f;
		public static float GetJumpPower(float height) => Mathf.Sqrt(2 * RuntimeConstants.GRAVITY * height);

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

		//Pearl stuff
		public SphereShape3D PearlCollisionShape = new SphereShape3D();
		public SphereShape3D RichPearlCollisionShape = new SphereShape3D();
		[Export]
		public AnimatedTexture pearlTexture;
		[Export]
		public AnimatedTexture richPearlTexture;

		private const float PEARL_NORMAL_COLLISION = .4f;
		private const float RICH_PEARL_NORMAL_COLLISION = .6f;
		public void UpdatePearlCollisionShapes(float sizeMultiplier = 1f)
		{
			PearlCollisionShape.Radius = PEARL_NORMAL_COLLISION * sizeMultiplier;
			RichPearlCollisionShape.Radius = RICH_PEARL_NORMAL_COLLISION * sizeMultiplier;
		}
	}
}
