using Godot;
using Godot.Collections;
using Project.Gameplay;

namespace Project.Core
{
	public partial class Runtime : Node
	{
		public static Runtime Instance;

		public static readonly RandomNumberGenerator randomNumberGenerator = new();
		public static readonly Vector2I SCREEN_SIZE = new(1920, 1080); // Working resolution is 1080p
		public static readonly Vector2I HALF_SCREEN_SIZE = (Vector2I)((Vector2)SCREEN_SIZE * .5f);


		public override void _EnterTree()
		{
			Instance = this;
			ActiveController = -1; // Default to keyboard
			Interface.Menus.Menu.SetUpMemory();
		}


		public override void _Process(double _)
		{
			UpdateShaderTime();

			if (SaveManager.ActiveGameData != null)
				SaveManager.ActiveGameData.playTime = Mathf.MoveToward(SaveManager.ActiveGameData.playTime, SaveManager.MAX_PLAY_TIME, PhysicsManager.normalDelta);
		}

		[Export(PropertyHint.Layers3DPhysics)]
		/// <summary> Collision layer for the environment. </summary>
		public uint environmentMask;
		[Export(PropertyHint.Layers3DPhysics)]
		/// <summary> Collision layer for destructable particle effects. </summary>
		public uint particleCollisionLayer;
		[Export(PropertyHint.Layers3DPhysics)]
		/// <summary> Collision mask for destructable particle effects. </summary>
		public uint particleCollisionMask;
		[Export]
		/// <summary> Lockout used for stopping the player. </summary>
		public LockoutResource StopLockout { get; private set; }

		[Export]
		public SkillListResource masterSkillList;

		public static readonly float GRAVITY = 28.0f;
		public static readonly float MAX_GRAVITY = -48.0f;
		public static float CalculateJumpPower(float height) => Mathf.Sqrt(2 * Runtime.GRAVITY * height);

		private float shaderTime;
		private const float SHADER_ROLLOVER = 3600f;
		private readonly StringName SHADER_GLOBAL_TIME = "time";
		private void UpdateShaderTime()
		{
			shaderTime += PhysicsManager.normalDelta;
			if (shaderTime > SHADER_ROLLOVER)
				shaderTime -= SHADER_ROLLOVER; //Copied from original shader time's rollover
			RenderingServer.GlobalShaderParameterSet(SHADER_GLOBAL_TIME, shaderTime);
		}

		#region Pearl Stuff
		public SphereShape3D PearlCollisionShape = new();
		public SphereShape3D RichPearlCollisionShape = new();
		[Export]
		public PackedScene pearlScene;

		/// <summary> Pool of auto-collected pearls used whenever enemies are defeated or itemboxes are opened. </summary>
		private readonly Array<Gameplay.Objects.Pearl> pearlPool = new();

		private const float PEARL_NORMAL_COLLISION = .4f;
		private const float RICH_PEARL_NORMAL_COLLISION = .6f;
		public void UpdatePearlCollisionShapes(float sizeMultiplier = 1f)
		{
			PearlCollisionShape.Radius = PEARL_NORMAL_COLLISION * sizeMultiplier;
			RichPearlCollisionShape.Radius = RICH_PEARL_NORMAL_COLLISION * sizeMultiplier;
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
		private const float PEARL_MAX_TRAVEL_TIME = .4f;
		public void SpawnPearls(int amount, Vector3 spawnPosition, Vector2 radius, float heightOffset = 0)
		{
			Tween tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic);

			for (int i = 0; i < amount; i++)
			{
				Gameplay.Objects.Pearl pearl;

				if (pearlPool.Count != 0) //Reuse pearls if possible.
				{
					pearl = pearlPool[0];
					pearlPool.RemoveAt(0);
				}
				else
					pearl = SpawnPearl(); //Otherwise create new pearls.

				AddChild(pearl);
				pearl.Respawn();

				Vector3 spawnOffset = new Vector3(randomNumberGenerator.RandfRange(-radius.X, radius.X),
					randomNumberGenerator.RandfRange(-radius.Y, radius.Y),
					randomNumberGenerator.RandfRange(-radius.X, radius.X));
				spawnOffset.Y += heightOffset;

				float travelTime = randomNumberGenerator.RandfRange(PEARL_MIN_TRAVEL_TIME, PEARL_MAX_TRAVEL_TIME);
				tween.TweenProperty(pearl, "global_position", spawnPosition + spawnOffset, travelTime).From(spawnPosition);
				tween.TweenCallback(new Callable(pearl, Gameplay.Objects.Pickup.MethodName.Collect)).SetDelay(travelTime);
			}

			tween.Play();
			tween.Connect(Tween.SignalName.Finished, Callable.From(() => tween.Kill())); //Kill tween after completing
		}
		#endregion


		/// <summary> Emitted when the active controller changes. </summary>
		[Signal]
		public delegate void ControllerChangedEventHandler(int controllerIndex);
		[Signal]
		public delegate void EventInputedEventHandler(InputEvent e);
		public bool IsUsingController => ActiveController != -1;
		public int ActiveController { get; private set; }

		public override void _Input(InputEvent e)
		{
			EmitSignal(SignalName.EventInputed, e);

			if (e is not InputEventKey && e is not InputEventJoypadButton && e is not InputEventJoypadMotion) return;

			int targetController = -1;
			if (e is InputEventJoypadButton)
				targetController = e.Device; // Gamepad
			else if (e is InputEventJoypadMotion)
			{
				if (Mathf.Abs((e as InputEventJoypadMotion).AxisValue) < SaveManager.Config.deadZone) return;
				targetController = e.Device;
			}

			if (targetController == ActiveController) return;

			ActiveController = targetController;
			EmitSignal(SignalName.ControllerChanged, ActiveController);

			e.Dispose();
		}


		public string GetKeyLabel(Key key)
		{
			string returnString = OS.GetKeycodeString(key).ToUpper();
			if (returnString.Length == 4 && returnString.StartsWith("KEY")) // Numbers
				return returnString.Remove(0, 3);
			if (returnString.Length == 3 && returnString.StartsWith("KP")) // Numpad Numbers
				return "NUM " + returnString.Remove(0, 2);

			switch (key)
			{
				// Typical keys
				case Key.Escape:
					return "ESC";
				case Key.Quoteleft:
					return "`";
				case Key.Minus:
					return "-";
				case Key.Equal:
					return "=";
				case Key.Backspace:
					return "BK\bSPC";
				case Key.Bracketleft:
					return "[";
				case Key.Bracketright:
					return "]";
				case Key.Backslash:
					return "\\";
				case Key.Capslock:
					return "CAPS";
				case Key.Semicolon:
					return ";";
				case Key.Apostrophe:
					return "'";
				case Key.Comma:
					return ",";
				case Key.Period:
					return ".";
				case Key.Slash:
					return "/";
				case Key.Menu:
					return "☰";

				case Key.Left:
					return "🡸";
				case Key.Right:
					return "🡺";
				case Key.Up:
					return "🡹";
				case Key.Down:
					return "🡻";

				// Side keys
				case Key.Print:
					return "PRTSC";
				case Key.Scrolllock:
					return "SCRLK";
				case Key.Insert:
					return "INS";
				case Key.Pageup:
					return "PG UP";
				case Key.Pagedown:
					return "PG DN";
				case Key.Delete:
					return "DEL";

				// Numpad
				case Key.KpAdd:
					return "NUM +";
				case Key.KpDivide:
					return "NUM /";
				case Key.KpSubtract:
					return "NUM -";
				case Key.KpMultiply:
					return "NUM *";
				case Key.KpPeriod:
					return "NUM .";
				case Key.KpEnter:
					return "NUM ENTER";

				default:
					return returnString;
			}
		}
	}
}