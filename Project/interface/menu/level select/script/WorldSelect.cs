using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class WorldSelect : Menu
{
	[ExportGroup("Media Settings")]
	[Export]
	private VideoStreamPlayer primaryVideoPlayer;
	[Export]
	private VideoStreamPlayer secondaryVideoPlayer;
	[Export]
	private Array<StringName> videoStreamPaths = [];
	private VideoStream[] videoStreams;
	private VideoStreamPlayer ActiveVideoPlayer { get; set; }
	private VideoStreamPlayer PreviousVideoPlayer { get; set; }

	private Color crossfadeColor;
	private float videoFadeFactor;
	private const float VideoCrossfadeSpeed = 5.0f;

	[ExportGroup("Selection Settings")]
	[Export]
	private Description description;
	[Export]
	private Array<Rect2> levelSpriteRegions = [];
	[Export]
	private Array<string> levelDescriptionKeys = [];
	[Export]
	private Array<NodePath> levelTextSprites = [];
	[Export]
	private Array<NodePath> levelGlowSprites = [];
	[Export]
	private Array<NodePath> levelNewSprites = [];
	private Array<int> newLevelList = [];
	private readonly Array<Sprite2D> _levelTextSprites = [];
	private readonly Array<Sprite2D> _levelGlowSprites = [];
	private readonly Array<Control> _levelNewSprites = [];
	protected override void SetUp()
	{
		for (int i = 0; i < levelTextSprites.Count; i++)
			_levelTextSprites.Add(GetNode<Sprite2D>(levelTextSprites[i]));

		for (int i = 0; i < levelGlowSprites.Count; i++)
			_levelGlowSprites.Add(GetNode<Sprite2D>(levelGlowSprites[i]));

		for (int i = 0; i < levelNewSprites.Count; i++)
			_levelNewSprites.Add(GetNode<Control>(levelNewSprites[i]));

		VerticalSelection = menuMemory[MemoryKeys.WorldSelect];
		videoStreams = new VideoStream[videoStreamPaths.Count];
		CallDeferred(MethodName.LoadVideos);

		if (menuMemory[MemoryKeys.ActiveMenu] == (int)MemoryKeys.LevelSelect) // Activate the correct submenu
		{
			animator.Play("init-level-select");
			animator.Advance(0);
			VerticalSelection = (int)SaveManager.ActiveGameData.lastPlayedWorld;
			OpenSubmenu();
		}
	}

	public override void ShowMenu()
	{
		newLevelList.Clear();
		int levelIndex = 0;
		foreach (Menu item in _submenus) // Update new level statuses
		{
			if (item is LevelSelect levelSelect)
			{
				if (levelSelect.HasNewLevel())
					newLevelList.Add(levelIndex);

				levelIndex++;
			}
		}

		VerticalSelection = menuMemory[MemoryKeys.WorldSelect];

		if (ActiveVideoPlayer != null &&
			menuMemory[MemoryKeys.ActiveMenu] != (int)MemoryKeys.LevelSelect &&
			menuMemory[MemoryKeys.ActiveMenu] != (int)MemoryKeys.WorldSelect)
		{
			ActiveVideoPlayer.Stream = videoStreams[VerticalSelection];
		}

		if (animator.AssignedAnimation == "init" || animator.AssignedAnimation == "cancel")
			animator.Play("show-fade-video");
		else
			animator.Play(SHOW_ANIMATION);

		menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.WorldSelect;
	}

	/// <summary>
	/// Load videos a frame after scene is set up to prevent crashing
	/// </summary>
	private void LoadVideos()
	{
		for (int i = 0; i < videoStreams.Length; i++)
		{
			if (ResourceLoader.Exists(videoStreamPaths[i], "VideoStream"))
			{
				videoStreams[i] = ResourceLoader.Load<VideoStream>(videoStreamPaths[i]);
				continue;
			}

			GD.PushWarning($"Couldn't find video {videoStreams[i]}!");
		}
	}

	public override void _Process(double _)
	{
		if (primaryVideoPlayer.IsVisibleInTree())
		{
			UpdateVideo();
			if (ActiveVideoPlayer.Stream != null)
			{
				if (!ActiveVideoPlayer.IsPlaying())
					ActiveVideoPlayer.CallDeferred(VideoStreamPlayer.MethodName.Play);
				else
					videoFadeFactor = Mathf.MoveToward(videoFadeFactor, 1, VideoCrossfadeSpeed * PhysicsManager.normalDelta);
			}

			ActiveVideoPlayer.Modulate = Colors.Transparent.Lerp(Colors.White, videoFadeFactor);

			if (PreviousVideoPlayer != null)
				PreviousVideoPlayer.Modulate = crossfadeColor.Lerp(Colors.Transparent, videoFadeFactor);
		}
	}

	protected override void UpdateSelection()
	{
		int inputSign = Mathf.Sign(Input.GetAxis("move_up", "move_down"));
		if (inputSign != 0)
		{
			VerticalSelection = WrapSelection(VerticalSelection + inputSign, (int)SaveManager.WorldEnum.Max);
			menuMemory[MemoryKeys.WorldSelect] = VerticalSelection;
			menuMemory[MemoryKeys.LevelSelect] = 0; // Reset level selection

			bool isScrollingUp = inputSign < 0;
			int transitionIndex = WrapSelection(isScrollingUp ? VerticalSelection - 1 : VerticalSelection + 1, (int)SaveManager.WorldEnum.Max);
			UpdateSpriteRegion(3, transitionIndex); // Update level text

			animator.Play(isScrollingUp ? SCROLL_UP_ANIMATION : SCROLL_DOWN_ANIMATION);
			animator.Seek(0.0, true);
			DisableProcessing();
		}
	}

	protected override void Confirm()
	{
		// World hasn't been unlocked
		if (!SaveManager.ActiveGameData.IsWorldUnlocked((SaveManager.WorldEnum)VerticalSelection)) return;

		base.Confirm();
	}

	public override void OpenParentMenu()
	{
		base.OpenParentMenu();
		ActiveVideoPlayer.Stop();

		SaveManager.SaveGameData();
		SaveManager.ActiveSaveSlotIndex = -1;
	}

	public override void OpenSubmenu()
	{
		SaveManager.ActiveGameData.lastPlayedWorld = (SaveManager.WorldEnum)VerticalSelection;
		_submenus[VerticalSelection].ShowMenu();
	}

	private void UpdateVideo()
	{
		// Don't change video?
		if (ActiveVideoPlayer != null && ActiveVideoPlayer.Stream == videoStreams[VerticalSelection]) return;
		if (!SaveManager.ActiveGameData.IsWorldUnlocked((SaveManager.WorldEnum)VerticalSelection)) return; // World is locked
		if (!Mathf.IsZeroApprox(Input.GetAxis("move_up", "move_down"))) return; // Still scrolling

		if (ActiveVideoPlayer?.IsPlaying() == true)
		{
			videoFadeFactor = 0;
			crossfadeColor = ActiveVideoPlayer.Modulate;

			PreviousVideoPlayer = ActiveVideoPlayer;
			PreviousVideoPlayer.Paused = true;
		}

		ActiveVideoPlayer = ActiveVideoPlayer == secondaryVideoPlayer ? primaryVideoPlayer : secondaryVideoPlayer;
		ActiveVideoPlayer.Stream = videoStreams[VerticalSelection];
		ActiveVideoPlayer.Paused = false;
	}

	public void UpdateLevelText()
	{
		UpdateSpriteRegion(0, VerticalSelection - 1); // Top option
		UpdateSpriteRegion(1, VerticalSelection); // Center option
		UpdateSpriteRegion(2, VerticalSelection + 1); // Bottom option

		for (int i = 0; i < _levelGlowSprites.Count; i++) // Sync glow regions
			_levelGlowSprites[i].RegionRect = _levelTextSprites[i].RegionRect;
	}

	private void UpdateSpriteRegion(int spriteIndex, int selectionIndex)
	{
		selectionIndex = WrapSelection(selectionIndex, (int)SaveManager.WorldEnum.Max);
		if (!SaveManager.ActiveGameData.IsWorldUnlocked((SaveManager.WorldEnum)selectionIndex)) // World isn't unlocked.
			selectionIndex = levelSpriteRegions.Count - 1;

		// Update new notifications
		_levelNewSprites[spriteIndex].Visible = newLevelList.Contains(selectionIndex);
		_levelTextSprites[spriteIndex].RegionRect = levelSpriteRegions[selectionIndex];

		if (spriteIndex == 1) // Updating primary selection
		{
			description.ShowDescription();
			description.Text = levelDescriptionKeys[selectionIndex];
		}

	}
}