using System;
using Godot;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class ExperienceResult : Control
{
	[Signal]
	public delegate void FinishedEventHandler();

	[Export]
	private Control expFill;
	[Export]
	private Label expLabel;
	[Export]
	private Label scoreLabel;
	/// <summary> Label for the skill bonus. </summary>
	[Export]
	private Label skillLabel;
	/// <summary> Label for the first clear bonus. </summary>
	[Export]
	private Label missionLabel;
	/// <summary> Label for the player's current level. </summary>
	[Export]
	private Label levelLabel;
	[Export]
	private Label levelGainLabel;
	[Export]
	private Label skillPointLabel;
	[Export]
	private Label skillPointGainLabel;
	[Export]
	private Label soulLabel;
	[Export]
	private Label soulGainLabel;
	[Export]
	private BGMPlayer bgm;
	[Export]
	private AnimationPlayer animator;
	private bool isSkippingLevel;
	private bool isSkippingResults;
	private readonly string IncreaseAnimation = "increase";
	private readonly string LevelUpAnimation = "level-up";
	private readonly string ShowLevelUpAnimation = "show-level-up";

	private bool isFadingBgm;
	private bool isProcessing;
	/// <summary> Amount of experience the player will have after tallying is complete. </summary>
	private int targetExp;
	/// <summary> Amount of experience when tallying started. </summary>
	private int startingExp;
	private float expInterpolation;
	private int interpolatedExp;
	/// <summary> Lerp smoothing to use when gaining large amounts of exp. </summary>
	private readonly float ExpSmoothing = 0.5f;

	/// <summary> Number to draw on the score label. </summary>
	private int scoreExp;
	/// <summary> Number to draw on the skill label. </summary>
	private int skillExp;
	/// <summary> Number to draw on the mission label. </summary>
	private int missionExp;
	/// <summary> Is the first clear bonus being added? </summary>
	private bool useMissionExp;

	/// <summary> Is level up data already being shown? </summary>
	private bool isLevelUpShown;
	/// <summary> Experience points needed to reach the next level. </summary>
	private int levelupRequirement;
	/// <summary> Experience points needed for the previous level up. </summary>
	private int previousLevelupRequirement;
	/// <summary> More exp is granted in PR, so the levelup requirements are higher than the original game. </summary>
	private static int CalculateLevelUpRequirement(int level) => level == MaxLevel ? 99999999 : (LevelInterval * level) + (LevelInterval * (level / 10));
	/// <summary> Converts exp amount to level amount. Mostly used to fix corrupt save data. </summary>
	private static int CalculateLevel(int exp)
	{
		// There's a way to do this mathematically, but I haven't taken discrete mathematics yet so here's a while loop instead
		int currentLevel = 1;
		while (exp >= CalculateLevelUpRequirement(currentLevel))
			currentLevel++;

		return currentLevel;
	}
	private const int MaxLevel = 99;
	private const int LevelInterval = 10000;

	private StageSettings Stage => StageSettings.instance;

	public override void _Ready()
	{
		SaveManager.ActiveGameData.level = CalculateLevel(SaveManager.ActiveGameData.exp); // Update from old save data, just in case
		SaveManager.ActiveGameData.level = Mathf.Min(SaveManager.ActiveGameData.level, MaxLevel);
		useMissionExp = SaveManager.ActiveGameData.GetClearStatus(Stage.Data.LevelID) != SaveManager.GameData.LevelStatus.Cleared;
	}

	public override void _PhysicsProcess(double _)
	{
		if (!isProcessing)
		{
			if (isFadingBgm)
				isFadingBgm = SoundManager.FadeAudioPlayer(bgm, 2.0f);
			return;
		}

		if (Input.IsActionJustPressed("button_jump") || Input.IsActionJustPressed("button_action"))
		{
			if (isSkippingLevel)
			{
				isSkippingResults = true;
				SkipResults();
			}
			else
			{
				isSkippingLevel = true;
				SkipAnimations();
			}
		}

		if (SaveManager.ActiveGameData.exp == targetExp)
		{
			if (animator.CurrentAnimation == IncreaseAnimation)
				animator.Stop();

			SaveManager.SaveGameData();
			isProcessing = false;

			if (isSkippingResults)
				FinishMenu();
			else
				GetTree().CreateTimer(.5).Connect(SceneTreeTimer.SignalName.Timeout, new Callable(this, MethodName.FinishMenu));

			return;
		}

		UpdateExp();
	}

	private void UpdateExp()
	{
		if (animator.IsPlaying() && animator.CurrentAnimation != IncreaseAnimation) return;

		if (!animator.IsPlaying())
			animator.Play(IncreaseAnimation);

		int startExp = Mathf.Max(startingExp, previousLevelupRequirement);
		int endExp = Mathf.Min(levelupRequirement, targetExp);
		// Calculate interpolation speed to account for slight exp gains
		float interpolationSpeed = (endExp - previousLevelupRequirement) / (float)(levelupRequirement - previousLevelupRequirement);

		// Start interpolation
		expInterpolation = Mathf.MoveToward(expInterpolation, 1, ExpSmoothing / interpolationSpeed * PhysicsManager.physicsDelta);
		if (isSkippingLevel || Mathf.IsEqualApprox(expInterpolation, 1.0f)) // Workaround because something is wrong with Mathf.Lerp and ints...
		{
			isSkippingLevel = false;
			interpolatedExp = endExp;
		}
		else
		{
			interpolatedExp = Mathf.FloorToInt(Mathf.Lerp(startExp, endExp, expInterpolation));
		}
		SaveManager.ActiveGameData.exp = interpolatedExp;
		RedrawData();

		if (SaveManager.ActiveGameData.exp >= levelupRequirement && SaveManager.ActiveGameData.level < MaxLevel) // Level up
			ProcessLevelUp();
	}

	private void RedrawData()
	{
		float expRatio = (SaveManager.ActiveGameData.exp - previousLevelupRequirement) / ((float)levelupRequirement - previousLevelupRequirement);
		expRatio = Mathf.Clamp(expRatio, 0, 1);
		expFill.Scale = new(expRatio, 1);
		expLabel.Text = $"{ExtensionMethods.FormatMenuNumber(SaveManager.ActiveGameData.exp)}/{ExtensionMethods.FormatMenuNumber(levelupRequirement)}";

		int addedExp = interpolatedExp - startingExp;
		scoreExp = Math.Clamp(Stage.TotalScore - addedExp, 0, Stage.TotalScore);
		skillExp = Math.Clamp(Stage.CurrentEXP + Stage.TotalScore - addedExp, 0, Stage.CurrentEXP);
		if (useMissionExp)
			missionExp = Math.Clamp(Stage.CurrentEXP + Stage.TotalScore + Stage.Data.FirstClearBonus - addedExp, 0, Stage.Data.FirstClearBonus);

		scoreLabel.Text = ExtensionMethods.FormatMenuNumber(scoreExp);
		skillLabel.Text = ExtensionMethods.FormatMenuNumber(skillExp);
		missionLabel.Text = ExtensionMethods.FormatMenuNumber(missionExp);
	}

	private void SkipAnimations()
	{
		// Skip any active animation
		if (!animator.IsPlaying() || animator.CurrentAnimation == IncreaseAnimation)
			return;

		animator.Seek(animator.CurrentAnimationLength, true, true);
	}

	private void SkipResults()
	{
		// Skip everything
		SkipAnimations();
		SaveManager.ActiveGameData.exp = targetExp;
		interpolatedExp = targetExp;
		expInterpolation = 1.0f;
		ProcessLevelUp();
		RedrawData();
	}

	private void ProcessLevelUp()
	{
		int levelsGained = 0;

		while (SaveManager.ActiveGameData.exp >= levelupRequirement)
		{
			if (SaveManager.ActiveGameData.level >= MaxLevel)
			{
				SaveManager.ActiveGameData.level = MaxLevel;
				SaveManager.ActiveGameData.exp = targetExp;
				break;
			}

			// Level up
			if (!isLevelUpShown)
			{
				isLevelUpShown = true;
				animator.Seek(animator.CurrentAnimationLength, true, true);
				animator.Play(ShowLevelUpAnimation);
				animator.Advance(0.0);
			}

			expInterpolation = 0.0f;
			levelsGained++;
			SaveManager.ActiveGameData.level = Mathf.Min(SaveManager.ActiveGameData.level + 1, MaxLevel);
			previousLevelupRequirement = levelupRequirement;
			levelupRequirement = CalculateLevelUpRequirement(SaveManager.ActiveGameData.level); // Update level up requirement
		}

		if (levelsGained == 0)
			return;

		int maxSoulPower = SaveManager.ActiveGameData.CalculateMaxSoulPower();
		int maxSkillPoints = SkillRing.CalculateSkillPointsByLevel(SaveManager.ActiveGameData.level);

		int soulGaugeGain = maxSoulPower - CharacterController.instance.Skills.MaxSoulPower;
		int skillPointGain = maxSkillPoints - SaveManager.ActiveSkillRing.MaxSkillPoints;

		levelGainLabel.Text = $"+{levelsGained.ToString("00")}";
		skillPointGainLabel.Text = $"+{skillPointGain.ToString("000")}";
		soulGainLabel.Text = $"+{soulGaugeGain.ToString("000")}";

		SaveManager.ActiveSkillRing.UpdateTotalSkillPoints();

		levelLabel.Text = SaveManager.ActiveGameData.level.ToString("00");
		skillPointLabel.Text = maxSkillPoints.ToString("000");
		soulLabel.Text = maxSoulPower.ToString("000");
		soulLabel.GetParent<Control>().Visible = soulGaugeGain != 0;

		expFill.Scale = Vector2.One; // Ensure exp bar is full
		expLabel.Text = $"{ExtensionMethods.FormatMenuNumber(previousLevelupRequirement)}/{ExtensionMethods.FormatMenuNumber(previousLevelupRequirement)}";

		if (animator.CurrentAnimation != ShowLevelUpAnimation)
			animator.Play(LevelUpAnimation);
	}

	private void OnResultsClosed()
	{
		levelupRequirement = CalculateLevelUpRequirement(SaveManager.ActiveGameData.level);
		previousLevelupRequirement = CalculateLevelUpRequirement(SaveManager.ActiveGameData.level - 1);

		startingExp = SaveManager.ActiveGameData.exp;
		targetExp = startingExp + Stage.TotalScore; // Add exp from score
		targetExp += Stage.CurrentEXP; // Add exp from skills
		targetExp = Mathf.Min(targetExp, CalculateLevelUpRequirement(MaxLevel));

		if (useMissionExp) // Add mission bonus
		{
			if (Stage.LevelState == StageSettings.LevelStateEnum.Failed) // Don't add mission exp when player fails a level
				useMissionExp = false;
			else
				targetExp += Stage.Data.FirstClearBonus;
		}
		missionLabel.GetParent<Control>().Visible = useMissionExp;

		if (targetExp == startingExp) // No EXP was gained
		{
			EmitSignal(SignalName.Finished);
			return;
		}

		RedrawData();

		// Fade to black
		TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionProcess, new Callable(this, MethodName.InitializeMenu), (uint)ConnectFlags.OneShot);
		TransitionManager.instance.Connect(TransitionManager.SignalName.TransitionFinish, new Callable(this, MethodName.ShowMenu), (uint)ConnectFlags.OneShot);
		TransitionManager.StartTransition(new()
		{
			color = Colors.Black,
			inSpeed = .1f,
			outSpeed = .1f
		});
	}

	private void InitializeMenu()
	{
		animator.Play("init");
		TransitionManager.FinishTransition();
	}

	private void ShowMenu()
	{
		bgm.Play();
		isProcessing = true;
		animator.Play("show");
	}

	private void FinishMenu()
	{
		if (isFadingBgm)
			return;

		// Emit a signal; Transition is handled by NotificationMenu.cs
		isFadingBgm = true;
		EmitSignal(SignalName.Finished);
	}
}
