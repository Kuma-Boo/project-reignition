using Godot;
using Godot.Collections;
using Project.Gameplay;

namespace Project.Core;

public partial class Runtime : Node
{
	public static Runtime Instance;

	public static readonly RandomNumberGenerator randomNumberGenerator = new();
	public static readonly Vector2I ScreenSize = new(1920, 1080); // Working resolution is 1080p
	public static readonly Vector2I HalfScreenSize = (Vector2I)((Vector2)ScreenSize * .5f);

	public override void _EnterTree()
	{
		Instance = this;
		ActiveController = -1; // Default to keyboard
		Interface.Menus.Menu.SetUpMemory();
	}

	public override void _Ready() => TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, Callable.From(ClearPearls));

	public override void _Process(double _)
	{
		UpdateShaderTime();

		if (SaveManager.ActiveSaveSlotIndex == -1)
			return;

		SaveManager.ActiveGameData.playTime = Mathf.MoveToward(SaveManager.ActiveGameData.playTime,
			SaveManager.MaxPlayTime, PhysicsManager.normalDelta);
	}

	/// <summary> Collision layer for the environment. </summary>
	[Export(PropertyHint.Layers3DPhysics)]
	public uint environmentMask;

	/// <summary> Collision layer for destructable particle effects. </summary>
	[Export(PropertyHint.Layers3DPhysics)]
	public uint particleCollisionLayer;

	/// <summary> Collision mask for destructable particle effects. </summary>
	[Export(PropertyHint.Layers3DPhysics)]
	public uint particleCollisionMask;

	/// <summary> The default lockout used when a stage is finished. </summary>
	[Export]
	public LockoutResource DefaultCompletionLockout { get; private set; }

	[Export]
	/// <summary> Reference to the complete skill list. </summary>
	public SkillListResource SkillList { get; private set; }

	public static readonly float Gravity = 28.0f;
	public static readonly float MaxGravity = -48.0f;
	public static float CalculateJumpPower(float height) => Mathf.Sqrt(2 * Gravity * height);

	private float shaderTime;
	private const float ShaderTimeRollover = 3600f;
	private readonly StringName ShaderTimeParameter = "time";

	private void UpdateShaderTime()
	{
		if (GetTree().Paused)
			return;

		shaderTime += PhysicsManager.normalDelta * (float)Engine.TimeScale;
		if (shaderTime > ShaderTimeRollover)
			shaderTime -= ShaderTimeRollover; // Copied from original shader time's rollover
		RenderingServer.GlobalShaderParameterSet(ShaderTimeParameter, shaderTime);
	}

	#region Pearl Stuff
	public SphereShape3D PearlCollisionShape = new();
	public SphereShape3D RichPearlCollisionShape = new();
	[Export]
	public PackedScene pearlScene;

	/// <summary> Pool of auto-collected pearls used whenever enemies are defeated or itemboxes are opened. </summary>
	private readonly Array<Gameplay.Objects.Pearl> pearlPool = [];
	private readonly Array<Gameplay.Objects.Pearl> tweeningPearls = [];
	private readonly Array<Tween> pearlTweens = [];

	private const float PearlCollisionSize = .4f;
	private const float RichPearlCollisionSize = .6f;

	public void UpdatePearlCollisionShapes(float sizeMultiplier = 1f)
	{
		PearlCollisionShape.Radius = PearlCollisionSize * sizeMultiplier;
		RichPearlCollisionShape.Radius = RichPearlCollisionSize * sizeMultiplier;
	}

	private const float PearlMinTravelTime = .2f;
	private const float PearlMaxTravelTime = .4f;

	/// <summary> Maximum random delay used to prevent pearls from "clumping."  </summary>
	private const float PearlDelayRange = .4f;

	public void SpawnPearls(int amount, Vector3 spawnPosition, Vector2 radius, float heightOffset = 0)
	{
		Tween tween = CreateTween().SetParallel(true).SetTrans(Tween.TransitionType.Cubic);

		for (int i = 0; i < amount; i++)
		{
			Gameplay.Objects.Pearl pearl;

			if (pearlPool.Count != 0) // Reuse pearls if possible.
			{
				pearl = pearlPool[0];
				pearlPool.RemoveAt(0);
			}
			else // Otherwise create a new pearl.
			{
				pearl = pearlScene.Instantiate<Gameplay.Objects.Pearl>();
				pearl.DisableAutoRespawning = true; // Don't auto-respawn
				pearl.Monitoring = pearl.Monitorable = false; // Unlike normal pearls, these are automatically collected
				pearl.Connect(Gameplay.Objects.Pearl.SignalName.Despawned, Callable.From(() => RepoolPearl(pearl)));
			}

			AddChild(pearl);
			tweeningPearls.Add(pearl);
			pearl.Respawn();

			Vector3 spawnOffset = new(randomNumberGenerator.RandfRange(-radius.X, radius.X),
				randomNumberGenerator.RandfRange(-radius.Y, radius.Y),
				randomNumberGenerator.RandfRange(-radius.X, radius.X));
			spawnOffset.Y += heightOffset;

			float travelTime = randomNumberGenerator.RandfRange(PearlMinTravelTime, PearlMaxTravelTime);
			float delay = randomNumberGenerator.RandfRange(0, PearlDelayRange);
			tween.TweenProperty(pearl, "global_position", spawnPosition + spawnOffset, travelTime).From(spawnPosition)
				.SetDelay(delay);
			tween.TweenCallback(new Callable(pearl, Gameplay.Objects.Pickup.MethodName.Collect))
				.SetDelay(travelTime + delay);
		}

		pearlTweens.Add(tween);

		tween.Play();
		tween.Connect(Tween.SignalName.Finished, Callable.From(() => KillPearlTween(tween))); // Kill tween after completing
	}

	private void RepoolPearl(Gameplay.Objects.Pearl pearl)
	{
		if (!pearlPool.Contains(pearl))
			pearlPool.Add(pearl);

		tweeningPearls.Remove(pearl);
	}

	private void ClearPearls()
	{
		for (int i = pearlTweens.Count - 1; i >= 0; i--)
			KillPearlTween(pearlTweens[i]);

		for (int i = tweeningPearls.Count - 1; i >= 0; i--)
			tweeningPearls[i].Despawn();

		SoundManager.instance.StopAllPearlSFX();
	}

	private void KillPearlTween(Tween tween)
	{
		if (tween?.IsValid() == true)
			tween.Kill();

		pearlTweens.Remove(tween);
	}
	#endregion

	/// <summary> Emitted when the active controller changes. </summary>
	[Signal]
	public delegate void ControllerChangedEventHandler(int controllerIndex);

	[Signal]
	public delegate void EventInputedEventHandler(InputEvent e);

	public bool IsUsingController => ActiveController != -1;
	public int ActiveController { get; private set; }

	/// <summary> Gets the ControllerType of the active controller. </summary>
	public SaveManager.ControllerType GetActiveControllerType()
	{
		if (SaveManager.Config.controllerType != SaveManager.ControllerType.Automatic)
			return SaveManager.Config.controllerType;

		string controllerName = Input.GetJoyName(ActiveController);
		if (controllerName.Contains("PS")) // PlayStation
			return SaveManager.ControllerType.PlayStation;
		if (controllerName.Contains("Nintendo"))
			return SaveManager.ControllerType.Nintendo;
		if (controllerName.Contains("Steam"))
			return SaveManager.ControllerType.Steam;

		return SaveManager.ControllerType.Xbox; // Default to XBox
	}

	public int ControllerAxisToIndex(InputEventJoypadMotion motion)
	{
		int axis = (int)motion.Axis;
		if (axis <= (int)JoyAxis.RightY)
			return (axis * 2) + Mathf.Clamp(Mathf.RoundToInt(motion.AxisValue), 0, 1);
		else
			return axis + 4;
	}

	public override void _Input(InputEvent e)
	{
		EmitSignal(SignalName.EventInputed, e);

		if (e is not InputEventKey && e is not InputEventJoypadButton && e is not InputEventJoypadMotion) return;

		var targetController = -1;
		switch (e)
		{
			case InputEventJoypadButton:
				targetController = e.Device; // Gamepad
				break;
			case InputEventJoypadMotion motion:
				if (Mathf.Abs(motion.AxisValue) < SaveManager.Config.deadZone)
					return;

				targetController = motion.Device;
				break;
		}

		if (targetController == ActiveController)
			return;

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

		return key switch
		{
			// Typical keys
			Key.Escape => "ESC",
			Key.Quoteleft => "`",
			Key.Minus => "-",
			Key.Equal => "=",
			Key.Backspace => "BK\bSPC",
			Key.Bracketleft => "[",
			Key.Bracketright => "]",
			Key.Backslash => "\\",
			Key.Capslock => "CAPS",
			Key.Semicolon => ";",
			Key.Apostrophe => "'",
			Key.Comma => ",",
			Key.Period => ".",
			Key.Slash => "/",
			Key.Menu => "☰",
			Key.Left => "🡸",
			Key.Right => "🡺",
			Key.Up => "🡹",
			Key.Down => "🡻",
			// Side keys
			Key.Print => "PRTSC",
			Key.Scrolllock => "SCRLK",
			Key.Insert => "INS",
			Key.Pageup => "PG UP",
			Key.Pagedown => "PG DN",
			Key.Delete => "DEL",
			// Numpad
			Key.KpAdd => "NUM +",
			Key.KpDivide => "NUM /",
			Key.KpSubtract => "NUM -",
			Key.KpMultiply => "NUM *",
			Key.KpPeriod => "NUM .",
			Key.KpEnter => "NUM ENTER",
			_ => returnString
		};
	}
}