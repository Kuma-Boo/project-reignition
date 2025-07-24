using Godot;
using Project.Core;

namespace Project.Gameplay;

public partial class Score : Control
{

	private StageSettings Stage => StageSettings.Instance;

	[ExportGroup("Time & Score")]
	[Export]
	private Control rankPreviewerRoot;
	[Export]
	private TextureRect mainRank;
	[Export]
	private TextureRect transitionRank;
	[Export]
	private Texture2D[] rankTextures;
	[Export]
	private AudioStreamPlayer rankUpSFX;
	[Export]
	private AudioStreamPlayer rankDownSFX;
	private int CurrentRank { get; set; }
	private Tween rankTween;
	public void InitializeRankPreviewer()
	{
		rankPreviewerRoot.Visible = SaveManager.ActiveSkillRing.IsSkillEquipped(SkillKey.RankPreview);
		if (!rankPreviewerRoot.Visible)
			return;

		CurrentRank = Mathf.Max(0, Stage.CalculateRank(true));
		mainRank.Texture = rankTextures[CurrentRank];
	}

	private void UpdateRankPreviewer()
	{
		if (!rankPreviewerRoot.Visible)
			return;

		int rank = Stage.CalculateRank(true);
		if (CurrentRank == rank || rankTween?.IsRunning() == true)
			return;

		int rankDirection = rank - CurrentRank;
		if (rankDirection < 0)
			StartRankDownTween(rankDirection);
		else
			StartRankUpTween(rankDirection);

		CurrentRank = rank;
	}

	private void StartRankDownTween(int rankDirection)
	{
		rankDownSFX.Play();
		transitionRank.Texture = mainRank.Texture;
		transitionRank.Position = Vector2.Zero;
		transitionRank.SelfModulate = Colors.White;
		mainRank.Texture = rankTextures[CurrentRank + rankDirection];
		rankTween = CreateTween().SetParallel();
		rankTween.TweenProperty(transitionRank, "self_modulate", Colors.Transparent, .5f);
		rankTween.TweenProperty(transitionRank, "position", Vector2.Down * 128, .5f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		rankTween.TweenCallback(new Callable(this, MethodName.CompleteRankDownTween)).SetDelay(.5f);
	}

	private void StartRankUpTween(int rankDirection)
	{
		rankUpSFX.Play();
		transitionRank.Texture = rankTextures[CurrentRank + rankDirection];
		transitionRank.Position = Vector2.Up * 256;
		rankTween = CreateTween().SetParallel();
		rankTween.TweenProperty(transitionRank, "self_modulate", Colors.White, .5f);
		rankTween.TweenProperty(transitionRank, "position", Vector2.Zero, .5f).SetTrans(Tween.TransitionType.Bounce);
		rankTween.TweenCallback(new Callable(this, MethodName.CompleteRankUpTween)).SetDelay(.5f);
	}

	private void CompleteRankUpTween()
	{
		mainRank.Texture = transitionRank.Texture;
		transitionRank.SelfModulate = Colors.Transparent;
		rankTween.Kill();
	}

	private void CompleteRankDownTween() => rankTween.Kill();

	[Export]
	private Label time;
	public void UpdateTime()
	{
		UpdateRankPreviewer(); // Update rank every frame

		if (Stage.Data.MissionTimeLimit != 0) // Time limit; Draw time counting DOWN
		{
			float timeLeft = Mathf.Clamp(Stage.Data.MissionTimeLimit - Stage.CurrentTime, 0, Stage.Data.MissionTimeLimit);
			time.Text = ExtensionMethods.FormatTime(timeLeft);
			return;
		}

		time.Text = Stage.DisplayTime;
	}

	[Export]
	private Label score;
	public void UpdateScore() => score.Text = Stage.DisplayScore;
}
