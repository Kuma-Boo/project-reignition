using Godot;

namespace Project.Core
{
	public partial class RuntimeConstants : Node
	{
		public static RuntimeConstants Instance;
		public override void _Ready() => Instance = this;
		public override void _Process(double _)
		{
			UpdateShaderTime();
		}

		public static RandomNumberGenerator randomNumberGenerator = new RandomNumberGenerator();

		public const float GRAVITY = 28.0f;
		public const float MAX_GRAVITY = -48.0f;
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

		//Pearl collision shapes
		public static SphereShape3D PearlCollisionShape = new SphereShape3D();
		public static SphereShape3D RichPearlCollisionShape = new SphereShape3D();
		private const float PEARL_NORMAL_COLLISION = .4f;
		private const float RICH_PEARL_NORMAL_COLLISION = .6f;
		public static void UpdatePearlCollisionShapes(float sizeMultiplier = 1f)
		{
			PearlCollisionShape.Radius = PEARL_NORMAL_COLLISION * sizeMultiplier;
			RichPearlCollisionShape.Radius = RICH_PEARL_NORMAL_COLLISION * sizeMultiplier;
		}
	}
}
