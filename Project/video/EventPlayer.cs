using Godot;
using Project.Core;

namespace Project.Interface.Menus;

/// <summary>
/// Plays an event (cutscene) with the correct audio depending on the localization settings
/// </summary>
public partial class EventPlayer : Node
{
	[Export] private AudioStream enAudio;
	[Export] private AudioStream jaAudio;
	/// <summary> Subtitle time table, separated by spaces. We'll probably want to redo this later. </summary>
	[Export(PropertyHint.MultilineText)] private string subtitleData;
	private Gameplay.Triggers.DialogTrigger subtitles;

	private bool IsSpecialBook => Menu.menuMemory[Menu.MemoryKeys.ActiveMenu] == (int)Menu.MemoryKeys.SpecialBook;

	[Export] private AudioStreamPlayer audioPlayer;
	[Export] private VideoStreamPlayer videoPlayer;
	/// <summary> Optional key for unlocking a world ring. </summary>
	[Export(PropertyHint.Enum, "None, Sand Oasis, Dinosaur Jungle, Evil Foundry, Levitated Ruin, Pirate Storm, Skeleton Dome, Night Palace")]
	private SaveManager.WorldEnum worldRing;

	private float skipTimer;
	private const float SkipLength = 1f; // How long the pause button needs to be held to skip the cutscene

	public override void _Ready()
	{
		if (!string.IsNullOrEmpty(subtitleData))
			CreateSubtitles();

		// TODO Add spanish audio
		audioPlayer.Stream = SaveManager.UseEnglishVoices ? enAudio : jaAudio;
		CallDeferred(nameof(StartCutscene));

		if (worldRing != SaveManager.WorldEnum.LostPrologue && !SaveManager.ActiveGameData.IsWorldRingObtained(worldRing))
		{
			SaveManager.ActiveGameData.UnlockWorldRing(worldRing);
			NotificationManager.Instance.AddNotification(NotificationManager.NotificationType.WorldRing, $"unlock_ring_{worldRing.ToString().ToSnakeCase()}");
		}
	}

	private void StartCutscene()
	{
		videoPlayer.Play();
		audioPlayer.Play();

		subtitles?.Activate();
	}

	public override void _PhysicsProcess(double _)
	{
		if (TransitionManager.IsTransitionActive)
			return;

		if (Menu.menuMemory[Menu.MemoryKeys.ActiveMenu] != (int)Menu.MemoryKeys.SpecialBook)
		{
			// Process skipping story cutscene
			if (Runtime.Instance.IsActionJustPressed("sys_pause", "ui_accept") && !Input.IsActionJustPressed("toggle_fullscreen"))
			{
				skipTimer = Mathf.MoveToward(skipTimer, 1, PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(skipTimer, 1))
					OnEventFinished();

				return;
			}

			skipTimer = Mathf.MoveToward(skipTimer, 0, PhysicsManager.physicsDelta);
			return;
		}

		// Allow players to exit immediately when viewing from the special book
		if (Runtime.Instance.IsActionJustPressed("sys_cancel", "ui_cancel"))
			OnEventFinished();
	}

	private float GetStartTime(string[] data)
	{
		float t = SaveManager.UseEnglishVoices ? data[1].ToFloat() : data[3].ToFloat();
		if (t == -1) // Fallback to english
			t = data[1].ToFloat();
		return t;
	}

	private float GetSpacing(string[] data)
	{
		float t = SaveManager.UseEnglishVoices ? data[2].ToFloat() : data[4].ToFloat();
		if (t == -1) // Fallback to english
			t = data[2].ToFloat();
		return t;
	}

	private void CreateSubtitles()
	{
		subtitles = new Gameplay.Triggers.DialogTrigger()
		{
			IsCutscene = true,
			delays = [],
			displayLength = [],
			textKeys = [],
		};
		AddChild(subtitles);

		// Calculate the delays and display lengths
		string[] dataPoints = subtitleData.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
		string[] currentData = dataPoints[0].Split('	');
		string[] nextData = dataPoints[1].Split('	');
		float previousSpacing = GetStartTime(currentData); // First key is an exception and uses it's start time as delay
		float nextStartTime = GetStartTime(nextData);

		for (int i = 0; i < dataPoints.Length - 1; i++) // Skip the last key
		{
			subtitles.textKeys.Add(currentData[0]); // Assign key
			float currentStartTime = GetStartTime(currentData); // When to start subtitles
			float currentSpacing = GetSpacing(currentData); // Space between this subtitle and the next

			subtitles.delays.Add(previousSpacing); // Copy from previous delay
			subtitles.displayLength.Add(nextStartTime - currentStartTime - currentSpacing);

			// Advance read position
			currentData = nextData;
			if (i < dataPoints.Length - 2)
				nextData = dataPoints[i + 2].Split('	');
			else
				nextData = dataPoints[i + 1].Split('	');

			// Update values
			previousSpacing = currentSpacing;
			nextStartTime = GetStartTime(nextData);
		}

		// Deal with the last key
		currentData = dataPoints[dataPoints.Length - 1].Split('	');
		subtitles.textKeys.Add(currentData[0]); // Assign key
		subtitles.delays.Add(previousSpacing);
		subtitles.displayLength.Add(currentData[2].ToFloat()); // Final key's spacing is casted to be the display length
	}

	/// <summary> Called after the cutscene has finished playing. </summary>
	public void OnEventFinished()
	{
		TransitionManager.QueueSceneChange(IsSpecialBook ? TransitionManager.SpecialBookScenePath : TransitionManager.MenuScenePath);
		TransitionManager.StartTransition(new TransitionData()
		{
			color = Colors.Black,
			inSpeed = .5f,
		});
	}
}
