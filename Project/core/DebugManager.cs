using Godot;
using Project.Gameplay;
using Project.Gameplay.Triggers;
using System.Collections.Generic;

namespace Project.Core;

public partial class DebugManager : Node2D
{
	public static DebugManager Instance { get; set; }

	[Signal]
	public delegate void FullscreenToggledEventHandler();

	[Export]
	private Control debugMenuRoot;

	private bool isAdvancingFrame;
	private bool isAttemptingPause;
	private bool IsPaused => GetTree().Paused;

	private enum Properties
	{
		HSpeed,
		VSpeed,
		Grounded,
		Charge,
		PropertyCount
	}

	public override void _EnterTree()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		UseDemoSave = true; // Be sure to DISABLE this in the FINAL version of the game
		IsStageCullingEnabled = true;
		UnlockAllStages = UseDemoSave;

		if (OS.IsDebugBuild()) // Editor Debug
			SkipCountdown = true;

		SetUpSkills();
	}

	public override void _PhysicsProcess(double _)
	{
		if (Input.IsActionJustPressed("toggle_fullscreen")) // Global shortcut used in final build as well
		{
			SaveManager.Config.useFullscreen = !SaveManager.Config.useFullscreen;
			SaveManager.ApplyConfig();
			EmitSignal(SignalName.FullscreenToggled);
		}

		if (!OS.IsDebugBuild()) //Don't do anything in real build
			return;

		if (isAdvancingFrame)
		{
			GetTree().Paused = true;
			isAttemptingPause = true;
			isAdvancingFrame = false;
		}

		if (Input.IsActionJustPressed("debug_menu"))
			debugMenuRoot.Visible = !debugMenuRoot.Visible;

		if (Input.IsActionJustPressed("debug_turbo"))
			Engine.TimeScale = 2.5f;
		else if (Input.IsActionJustReleased("debug_turbo"))
			Engine.TimeScale = 1f;

		if (Input.IsActionJustPressed("debug_pause"))
		{
			if (IsPaused)
			{
				GetTree().Paused = false;
				isAttemptingPause = false;
			}
			else
			{
				DebugPause();
			}
		}

		if (!IsPaused)
			isAttemptingPause = false;

		if (Input.IsActionJustPressed("debug_window_small"))
		{
			SaveManager.Config.windowSize = 0;
			SaveManager.ApplyConfig();
		}

		if (Input.IsActionJustPressed("debug_window_large"))
		{
			SaveManager.Config.windowSize = 3;
			SaveManager.ApplyConfig();
		}

		if (Input.IsActionJustPressed("debug_step"))
			DebugPause();

		if (Input.IsActionJustPressed("debug_restart"))
		{
			if (!Input.IsKeyPressed(Key.Shift) && IsInstanceValid(StageSettings.Player))
			{
				StageSettings.Player.StartRespawn();
			}
			else
			{
				TransitionManager.QueueSceneChange(string.Empty);
				TransitionManager.StartTransition(new());
			}
		}
	}

	public override void _Process(double _)
	{
		if (IsDebugRaysEnabled) // Queue Raycast Redraw
			QueueRedraw();
	}

	private void DebugPause()
	{
		GetTree().Paused = false;
		isAttemptingPause = true;
		isAdvancingFrame = true;

		line2d.Clear();
		line3d.Clear();
	}

	#region Raycast Debug Code
	public override void _Draw()
	{
		if (!IsDebugRaysEnabled)
		{
			line2d.Clear();
			line3d.Clear();
			return;
		}

		for (int i = line2d.Count - 1; i >= 0; i--)
		{
			DrawLine(line2d[i].start, line2d[i].end, line3d[i].color, 1.0f, true);
			if (!isAttemptingPause)
				line2d.RemoveAt(i);
		}

		Camera3D cam = GetViewport().GetCamera3D();
		if (cam == null)
		{
			line3d.Clear();
			return; // NO CAMERA
		}

		for (int i = line3d.Count - 1; i >= 0; i--)
		{
			if (cam.IsPositionBehind(line3d[i].start) || cam.IsPositionBehind(line3d[i].end))
			{
				if (!isAttemptingPause)
					line3d.RemoveAt(i);

				continue;
			}

			Vector2 startPos = cam.UnprojectPosition(line3d[i].start);
			Vector2 endPos = cam.UnprojectPosition(line3d[i].end);

			DrawLine(startPos, endPos, line3d[i].color, 1.0f, true);

			if (!isAttemptingPause)
				line3d.RemoveAt(i);
		}
	}

	public struct Line3D(Vector3 s, Vector3 e, Color c)
	{
		public Vector3 start = s;
		public Vector3 end = e;
		public Color color = c;
	}

	public struct Line2D(Vector2 s, Vector2 e, Color c)
	{
		public Vector2 start = s;
		public Vector2 end = e;
		public Color color = c;
	}

	private static readonly List<Line3D> line3d = [];
	private static readonly List<Line2D> line2d = [];

	public static void DrawLn(Vector3 s, Vector3 e, Color c)
	{
		if (!Instance.IsDebugRaysEnabled)
			return;

		line3d.Add(new Line3D(s, e, c));
	}
	public static void DrawRay(Vector3 s, Vector3 r, Color c)
	{
		if (!Instance.IsDebugRaysEnabled)
			return;

		line3d.Add(new Line3D(s, s + r, c));
	}

	public static void DrawLn(Vector2 s, Vector2 e, Color c)
	{
		if (!Instance.IsDebugRaysEnabled)
			return;

		line2d.Add(new Line2D(s, e, c));
	}
	public static void DrawRay(Vector2 s, Vector2 r, Color c)
	{
		if (!Instance.IsDebugRaysEnabled)
			return;

		line2d.Add(new Line2D(s, s + r, c));
	}
	#endregion

	#region Debug Cheats
	/// <summary> Draw debug rays? </summary>
	private bool IsDebugRaysEnabled { get; set; }
	public void ToggleRays(bool enabled) => IsDebugRaysEnabled = enabled;

	[Signal]
	public delegate void StageCullingToggledEventHandler();
	public static bool IsStageCullingEnabled { get; private set; }
	private void ToggleStageCulling(bool enabled)
	{
		IsStageCullingEnabled = enabled;
		EmitSignal(SignalName.StageCullingToggled);
	}

	/// <summary> Have all worlds/stages unlocked. </summary>
	public bool UnlockAllStages { get; private set; }
	[Signal]
	public delegate void UnlockStagesToggledEventHandler();
	private void ToggleUnlockStages(bool enabled)
	{
		UnlockAllStages = UseDemoSave || enabled;
		EmitSignal(SignalName.UnlockStagesToggled);
	}

	public bool DrawDebugCam { get; private set; }
	[Signal]
	public delegate void CameraVisibilityToggledEventHandler();
	public void ToggleCamera(bool enabled)
	{
		DrawDebugCam = enabled;
		EmitSignal(SignalName.CameraVisibilityToggled);
	}

	/// <summary> Use a custom save. </summary>
	public bool UseDemoSave { get; private set; }
	#endregion

	#region Gameplay Cheats
	/// <summary> Infinite soul gauge. </summary>
	public bool InfiniteSoulGauge { get; private set; }
	private void ToggleInfiniteSoul(bool enabled) => InfiniteSoulGauge = enabled;
	/// <summary> Infinite rings. </summary>
	public bool InfiniteRings { get; private set; }
	private void ToggleInfiniteRings(bool enabled)
	{
		InfiniteRings = enabled;
		StageSettings.Instance?.UpdateRingCount(0, StageSettings.MathModeEnum.Replace, true);
	}
	/// <summary> Skip countdowns for faster debugging. </summary>
	public bool SkipCountdown { get; private set; }
	private void ToggleCountdown(bool enabled) => SkipCountdown = enabled;

	[Export]
	private OptionButton skillSelectButton;
	[Export]
	private Button skillToggleButton;
	[Export]
	private Slider skillAugmentSlider;
	private void SetUpSkills()
	{
		for (int i = 0; i < (int)SkillKey.Max; i++)
		{
			skillSelectButton.AddItem(((SkillKey)i).ToString());
		}

		skillSelectButton.Select(0);
	}

	public void OnSkillSelected(int skillIndex)
	{
		if (skillIndex == -1)
			skillIndex = skillSelectButton.Selected;

		SkillKey key = (SkillKey)skillIndex;
		skillToggleButton.ButtonPressed = SaveManager.ActiveSkillRing.EquippedSkills.Contains(key);

		SkillResource skill = Runtime.Instance.SkillList.GetSkill(key);

		if (!skill.HasAugments)
		{
			skillAugmentSlider.Editable = false;
			skillAugmentSlider.Value = 0;
			return;
		}

		skillAugmentSlider.Editable = true;
		skillAugmentSlider.MinValue = skill.Augments[0].AugmentIndex < 0 ? skill.Augments[0].AugmentIndex : 0;
		skillAugmentSlider.MaxValue = Mathf.Max(skill.Augments[^1].AugmentIndex, 0);
		skillAugmentSlider.TickCount = skill.Augments.Count + 1;
		if (SaveManager.ActiveSkillRing.EquippedAugments.TryGetValue(key, out int value))
			skillAugmentSlider.Value = value;
		else
			skillAugmentSlider.Value = 0;
	}

	private void OnSkillToggled(bool toggled)
	{
		SkillKey key = (SkillKey)skillSelectButton.Selected;

		if (toggled)
		{
			SaveManager.ActiveSkillRing.EquipSkill(key, 0, true);
			return;
		}

		SaveManager.ActiveSkillRing.UnequipSkill(key);
	}

	private void OnSkillAugmentChanged(float value)
	{
		int augmentValue = Mathf.RoundToInt(value);
		SaveManager.ActiveSkillRing.EquipSkill((SkillKey)skillSelectButton.Selected, augmentValue, true);
	}
	#endregion

	#region Checkpoint Cheats
	[Signal] public delegate void TriggeredDebugCheckpointEventHandler();
	public CheckpointTrigger DebugCheckpoint { get; private set; }

	private void SaveCustomCheckpoint()
	{
		if (!IsInstanceValid(StageSettings.Instance) || !IsInstanceValid(StageSettings.Player)) return;

		if (!IsInstanceValid(DebugCheckpoint))
		{
			DebugCheckpoint = new();
			StageSettings.Instance.AddChild(DebugCheckpoint);
		}

		DebugCheckpoint.GlobalTransform = StageSettings.Player.GlobalTransform;
		DebugCheckpoint.SaveCheckpointData();
		GD.Print("Checkpoint created at ", StageSettings.Player.GlobalPosition);

		EmitSignal(SignalName.TriggeredDebugCheckpoint);
	}

	private void LoadCustomCheckpoint()
	{
		if (!IsInstanceValid(StageSettings.Instance) || !IsInstanceValid(StageSettings.Player)) return;

		if (!IsInstanceValid(DebugCheckpoint))
		{
			GD.Print("No custom checkpoint to load.");
			return;
		}

		StageSettings.Player.StartRespawn(true);
	}
	#endregion

	#region Promo Settings
	public bool DisableHUD { get; private set; }
	public void ToggleHUD(bool enabled)
	{
		DisableHUD = enabled;

		if (!IsInstanceValid(HeadsUpDisplay.Instance)) return;
		HeadsUpDisplay.Instance.Visible = !enabled;
	}

	public bool DisableReticle { get; private set; }
	public void ToggleReticle(bool enabled)
	{
		DisableReticle = enabled;

		if (!IsInstanceValid(StageSettings.Player)) return;
		StageSettings.Player.Lockon.IsReticleVisible = !enabled;
	}

	/// <summary> Hide countdown for recording. </summary>
	public bool HideCountdown { get; private set; }
	private void ToggleCountdownVisibility(bool enabled) => HideCountdown = enabled;

	public bool DisableDialog { get; private set; }
	public void ToggleDialog(bool enabled) => DisableDialog = enabled;

	[Export]
	private LineEdit[] freeCamData;
	public void RedrawCamData(Vector3 position, Vector3 rotation)
	{
		for (int i = 0; i < freeCamData.Length; i++)
		{
			if (freeCamData[i].HasFocus())
				return;
		}

		freeCamData[0].Text = position.X.ToString();
		freeCamData[1].Text = position.Y.ToString();
		freeCamData[2].Text = position.Z.ToString();

		freeCamData[3].Text = rotation.X.ToString();
		freeCamData[4].Text = rotation.Y.ToString();
		freeCamData[5].Text = rotation.Z.ToString();
	}

	private void UpdateCamData(string newData)
	{
		if (!newData.IsValidFloat()) return;
		if (!IsInstanceValid(StageSettings.Player)) return;
		PlayerCameraController cam = StageSettings.Player.Camera;

		Vector3 pos = new(freeCamData[0].Text.ToFloat(), freeCamData[1].Text.ToFloat(), freeCamData[2].Text.ToFloat());
		Vector3 rot = new(freeCamData[3].Text.ToFloat(), freeCamData[4].Text.ToFloat(), freeCamData[5].Text.ToFloat());

		cam.UpdateFreeCamData(pos, rot);
	}
	#endregion
}
