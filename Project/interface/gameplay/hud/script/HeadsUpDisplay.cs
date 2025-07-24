using Godot;
using Project.Core;

namespace Project.Gameplay;

/// <summary>
/// Displays game data to the player. Only handles the graphics.
/// </summary>
public partial class HeadsUpDisplay : Control
{
	public static HeadsUpDisplay Instance;
	private StageSettings Stage => StageSettings.Instance;

	public override void _EnterTree() => Instance = this;

	public override void _Ready()
	{
		InitializeRankPreviewer();
		InitializeRings();
		InitializeObjectives();
		InitializeSoulGauge();
		InitializeRace();
		InitializePrompts();


		if (Stage != null) // Decouple from level settings
		{
			Stage.Connect(nameof(StageSettings.RingChanged), new Callable(this, MethodName.UpdateRingCount));
			Stage.Connect(nameof(StageSettings.TimeChanged), new Callable(this, MethodName.UpdateTime));
			Stage.Connect(nameof(StageSettings.ScoreChanged), new Callable(this, MethodName.UpdateScore));
			Stage.Connect(nameof(StageSettings.LevelCompleted), new Callable(this, MethodName.OnLevelCompleted)); // Hide interface
		}
	}

	#region Rings
	[Export] Rings rings;

	public void CollectFireSoul()
	{
		rings.CollectFireSoul();
	}

	private void InitializeRings()
	{
		// Initialize ring counter

		rings.InitializeRings();
	}


	private void UpdateRingCount(int amount, bool disableAnimations)
	{
		rings.UpdateRingCount(amount, disableAnimations);
	}

	#endregion

	#region Time and Score

	[Export]
	private Score score;
	private void InitializeRankPreviewer() => score.InitializeRankPreviewer();

	private void UpdateTime() => score.UpdateTime();

	private void UpdateScore() => score.UpdateScore();

	#endregion

	#region Objectives

	[Export]
	private Objectives objectives;
	private void InitializeObjectives() => objectives.InitializeObjectives();


	private void UpdateObjective() => objectives.UpdateObjective();


	public void PlayObjectiveAnimation(StringName animation) => objectives.PlayObjectiveAnimation(animation, 0);


	public void PlayObjectiveAnimation(StringName animation, int index) => objectives.PlayObjectiveAnimation(animation, index);

	#endregion

	#region Soul Gauge

	[Export]
	private SoulGauge soulGaugeVert;

	[Export]
	private SoulGauge soulGaugeHori;


	private void InitializeSoulGauge()
	{
		soulGaugeVert.Visible = false;
		soulGaugeHori.Visible = false;

		if (!SaveManager.Config.rotateSoulGauge)
		{
			soulGaugeVert.Visible = true;
			soulGaugeVert.InitializeSoulGauge();
		}
		else
		{
			soulGaugeHori.Visible = true;
			soulGaugeHori.InitializeSoulGauge();
		}
	}

	public SoulGauge GetCurrentSoulGauge()
	{
		if (soulGaugeVert.Visible)
			return soulGaugeVert;
		else
			return soulGaugeHori;
	}

	#endregion

	#region Race

	[Export]
	private Race race;
	private void InitializeRace() => race.InitializeRace();

	public void UpdateRace(float playerRatio, float uhuRatio) => race.UpdateRace(playerRatio, uhuRatio);

	#endregion

	#region Prompts
	[Signal]
	public delegate void InputPromptsChangedEventHandler();

	[ExportGroup("Prompts")]
	[Export]
	private Interface.NavigationButton[] buttons;
	[Export]
	private AnimationPlayer promptAnimator;
	private void InitializePrompts()
	{
		for (int i = 0; i < buttons.Length; i++)
			Connect(SignalName.InputPromptsChanged, new(buttons[i], Interface.NavigationButton.MethodName.Redraw));
	}

	private bool isPromptsVisible;
	public void SetPrompt(StringName label, int index) => buttons[index].ActionKey = label;
	public void ShowPrompts()
	{
		if (!SaveManager.Config.useActionPrompts)
			return;

		promptAnimator.Play(promptAnimator.CurrentAnimation == "show" ? "change" : "show");
		isPromptsVisible = true;
	}

	public void HidePrompts()
	{
		if (!isPromptsVisible)
			return;

		isPromptsVisible = false;
		promptAnimator.Play("hide");
	}

	private void RedrawPrompts()
	{
		for (int i = 0; i < buttons.Length; i++)
			buttons[i].Visible = buttons[i].ActionKey?.IsEmpty == false;

		EmitSignal(SignalName.InputPromptsChanged);
	}
	#endregion

	public void OnLevelCompleted() => SetVisibility(false); // Ignore parameter
	public void SetVisibility(bool value)
	{
		if (OS.IsDebugBuild() && DebugManager.Instance.DisableHUD)
		{
			Visible = false;
			return;
		}

		Visible = value;
	}
}