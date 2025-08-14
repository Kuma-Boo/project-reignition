using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SpecialBook : Menu
{
	private int tabSelection;
	private int pageSelection;

	private SpecialBookPage GetActivePage => tabs[tabSelection].PageResources[pageSelection];

	/// <summary> Keeps track of the entry the player is currently focusing on. </summary>
	private MenuStatusEnum menuStatus;
	private enum MenuStatusEnum
	{
		Chapter,
		Page,
		Description,
		Image,
		Video
	}

	[Export] private SpecialBookTab[] tabs;
	[Export] private SpecialBookWindow[] windows;
	[Export] private Sprite2D[] thumbnails;

	[Export] private Label chapterName;
	[Export] private Label chapterLabel;
	[Export] private Label textboxTitle;
	[Export] private Label previewDescription;

	[Export] private TextureRect previewImage;
	[Export] private Label previewNumber;

	[Export] private TextureRect fullImage;

	[Export] private AnimationPlayer navigationAnimator;
	private bool isNavigationVisible;

	[Export] private NavigationButton navZoom;
	[Export] private NavigationButton navPlay;
	[Export] private BGMPlayer audioPlayer;

	[Export] private AudioStreamPlayer sfxOpen;
	[Export] private AudioStreamPlayer sfxSelect;
	[Export] private AudioStreamPlayer sfxCancel;
	[Export] private AudioStreamPlayer sfxConfirm;
	[Export] private AudioStreamPlayer sfxCategorySelect;

	[Export] private AnimationPlayer randomAnimator;
	private bool playRandom;
	private int seekRandom;
	private readonly Array randomPages = [];

	private string currentResourceLoading;

	protected override void SetUp()
	{
		foreach (SpecialBookWindow window in windows)
			window.Initialize();
	}

	public override void ShowMenu()
	{
		menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.SpecialBook;
		PlayBgm();

		// Update selections based on memory
		tabSelection = menuMemory[MemoryKeys.SpecialBook] / windows.Length;
		pageSelection = menuMemory[MemoryKeys.SpecialBook] % windows.Length;

		LoadChapterData();

		if (menuMemory[MemoryKeys.SpecialBook] != 0)
		{
			tabs[tabSelection].SelectNoGlow();
			// If we are returning from an event scene
			windows[pageSelection].Select();
			chapterName.Visible = false;
			textboxTitle.Visible = true;
			LoadPageData();

			// Open description
			menuStatus = MenuStatusEnum.Description;
			animator.Play("show_description");
			animator.Advance(animator.CurrentAnimationLength);
			return;
		}

		tabs[tabSelection].Select(); // Select chapter tab
		base.ShowMenu();
	}

	public override void OpenParentMenu()
	{
		// Return to main menu
		FadeBgm(.5f);
		menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.MainMenu;
		TransitionManager.QueueSceneChange(TransitionManager.MenuScenePath);
		TransitionManager.StartTransition(new()
		{
			color = Colors.Black,
			inSpeed = .5f,
		});
	}

	protected override void ProcessMenu()
	{
		if (!string.IsNullOrEmpty(currentResourceLoading))
		{
			if (ResourceLoader.LoadThreadedGetStatus(currentResourceLoading) == ResourceLoader.ThreadLoadStatus.InProgress)
				return; // Still loading...

			ApplyLoadedResource();
			return;
		}

		if (menuStatus == MenuStatusEnum.Chapter || menuStatus == MenuStatusEnum.Page) // Change chapter by using the bumpers
		{
			if (Input.IsActionJustPressed("button_step_left"))
			{
				tabs[tabSelection].Deselect();
				tabSelection = WrapSelection(tabSelection - 1, 16);

				if (menuStatus == MenuStatusEnum.Chapter)
					tabs[tabSelection].Select();
				else
					tabs[tabSelection].SelectNoGlow();

				LoadChapterData();
				LoadPageData();

				sfxCategorySelect.Play();
			}

			if (Input.IsActionJustPressed("button_step_right"))
			{
				tabs[tabSelection].Deselect();
				tabSelection = WrapSelection(tabSelection + 1, 16);

				if (menuStatus == MenuStatusEnum.Chapter)
					tabs[tabSelection].Select();
				else
					tabs[tabSelection].SelectNoGlow();

				LoadChapterData();
				LoadPageData();

				sfxCategorySelect.Play();
			}

			if (Runtime.Instance.IsActionJustPressed("sys_pause", "ui_accept") && !Input.IsActionJustPressed("toggle_fullscreen"))
			{
				if (GetUnlockedEntriesCount() > 1) // If we have more than one page unlocked, then play the slideshow
				{
					RandomizeList();
					playRandom = true;
					seekRandom = 0;
					menuStatus = MenuStatusEnum.Image;
					randomPages.Shuffle();
					animator.Play("show_playrandom");
					randomAnimator.Play("playrandom");
					randomAnimator.Seek(0.0);

					sfxOpen.Play();
				}
			}
		}

		base.ProcessMenu();
	}

	private void ApplyLoadedResource()
	{
		Resource resource = ResourceLoader.LoadThreadedGet(currentResourceLoading);
		currentResourceLoading = string.Empty;
	}

	protected override void UpdateSelection()
	{
		Vector2I input = new(Mathf.Sign(Input.GetAxis("ui_left", "ui_right")), Mathf.Sign(Input.GetAxis("ui_up", "ui_down")));

		StartSelectionTimer();
		MenuControls(input);
	}

	protected override void Confirm()
	{
		SpecialBookPage page = GetActivePage;

		if (menuStatus == MenuStatusEnum.Chapter)
		{
			MenuControls(new Vector2I(0, 1));
			sfxSelect.Play();
			return;
		}

		if (menuStatus == MenuStatusEnum.Page)
		{
			if (page.IsUnlocked())
			{
				animator.Play("show_description");
				menuStatus = MenuStatusEnum.Description;
				sfxConfirm.Play();
			}

			return;
		}

		if (menuStatus == MenuStatusEnum.Description)
		{
			if (page.IsUnlocked())
			{
				switch (page.PageType)
				{
					case SpecialBookPage.PageTypeEnum.Image:
						menuStatus = MenuStatusEnum.Image;
						animator.Play("show_fullimage");

						isNavigationVisible = true;
						navigationAnimator.Play("RESET");

						sfxOpen.Play();
						StartSelectionTimer();
						return;
					case SpecialBookPage.PageTypeEnum.Music:
						// TODO Improve audio controls or actually have the bgm loop.
						bgm.Stop();

						// TODO Load audio asyncronously
						audioPlayer.Stream = ResourceLoader.Load<AudioStream>(page.AudioStreamPath);
						audioPlayer.loopEndPosition = (float)audioPlayer.Stream.GetLength() - 1f;
						audioPlayer.Play();
						return;
					case SpecialBookPage.PageTypeEnum.Video:
						/*
						TODO Allow users to play videos once cutscenes have been added to the game. 
						bgm.Stop();
						menuFocus = MenuFocus.Video;

						TransitionManager.QueueSceneChange(thisPage.videoFilePath);
						TransitionManager.StartTransition(new()
						{
							color = Colors.Black,
							inSpeed = .5f,
						});

						*/
						return;
				}
			}

			return;
		}

		if (menuStatus == MenuStatusEnum.Image)
		{
			isNavigationVisible = !isNavigationVisible;
			navigationAnimator.Play(isNavigationVisible ? "show" : "hide");
		}
	}

	protected override void Cancel()
	{
		if (menuStatus == MenuStatusEnum.Chapter)
		{
			// Reset menu memory
			menuMemory[MemoryKeys.SpecialBook] = 0;

			tabs[tabSelection].Deselect();
			animator.Play("hide");
			return;
		}

		if (menuStatus == MenuStatusEnum.Page)
		{
			tabs[tabSelection].SelectNoMove();
			windows[pageSelection].Deselect();
			menuStatus = MenuStatusEnum.Chapter;

			chapterName.Visible = true;
			textboxTitle.Visible = false;
			sfxCategorySelect.Play();
		}

		if (menuStatus == MenuStatusEnum.Description)
		{
			animator.Play("hide_description");
			menuStatus = MenuStatusEnum.Page;
			sfxCancel.Play();
		}

		if (menuStatus == MenuStatusEnum.Image)
		{
			if (!playRandom)
			{
				animator.Play("hide_fullimage");
				menuStatus = MenuStatusEnum.Description;
			}
			else
			{
				animator.Play("hide_playrandom");
				randomAnimator.Stop();
				menuStatus = MenuStatusEnum.Page;
				playRandom = false;
				tabs[tabSelection].DeselectNoGlow();
				windows[pageSelection].Select();
			}

			sfxCancel.Play();
		}
	}

	private void MenuControls(Vector2I input)
	{
		if (menuStatus == MenuStatusEnum.Chapter)
		{
			ProcessChapterSelection(input);
			return;
		}

		if (menuStatus == MenuStatusEnum.Page)
		{
			ProcessPageSelection(input);
			return;
		}

		if (menuStatus == MenuStatusEnum.Description || menuStatus == MenuStatusEnum.Image)
		{
			if (input.X == 0 || playRandom)
				return;

			windows[pageSelection].Deselect();

			int ogChapter = tabSelection;
			do
			{
				pageSelection += input.X;
				if (pageSelection > 14 || pageSelection < 0)
				{
					pageSelection = WrapSelection(pageSelection, 15);
					tabSelection = WrapSelection(tabSelection + input.X, 16);
					sfxOpen.Play();
				}

			} while (!IsValid(GetActivePage, menuStatus)); // Skips over every page we don't have unlocked. If we're on the full view, skips over movies and music too

			if (ogChapter != tabSelection)
			{
				tabs[ogChapter].Deselect();
				tabs[tabSelection].SelectNoGlow();
			}

			if (input.X > 0)
				sfxConfirm.Play();
			else if (input.X < 0)
				sfxCancel.Play();

			windows[pageSelection].Select();
			LoadChapterData();
			LoadPageData();
		}
	}

	private void ProcessChapterSelection(Vector2I input)
	{
		if (input.X != 0) // move left or right
		{
			tabs[tabSelection].Deselect();
			tabSelection = WrapSelection(tabSelection + (int)input.X, 16);
			tabs[tabSelection].Select();

			LoadChapterData();
			sfxCategorySelect.Play();
			return;
		}

		if (input.Y > 0) // Move down to the pages
		{
			tabs[tabSelection].DeselectNoGlow();
			menuStatus = MenuStatusEnum.Page;
			pageSelection = 0;
			windows[pageSelection].Select();
			chapterName.Visible = false;
			textboxTitle.Visible = true;

			LoadPageData();
			sfxSelect.Play();
		}
	}

	private void ProcessPageSelection(Vector2I input)
	{
		if (input.X != 0) // If we are going left or right
		{
			windows[pageSelection].Deselect();

			if (pageSelection <= 4) // row 1
				pageSelection = WrapSelection(pageSelection + input.X, 4, 0);
			else if (pageSelection >= 5 && pageSelection <= 9) // row 2
				pageSelection = WrapSelection(pageSelection + input.X, 9, 5);
			else if (pageSelection >= 10 && pageSelection <= 14) // row 3
				pageSelection = WrapSelection(pageSelection + input.X, 14, 10);

			windows[pageSelection].Select();
			LoadPageData();

			sfxSelect.Play();
		}
		if (input.Y != 0)
		{
			windows[pageSelection].Deselect();

			if (input.Y < 0 && pageSelection <= 4) // If we are going up on the first row
			{
				tabs[tabSelection].SelectNoMove();
				menuStatus = MenuStatusEnum.Chapter;
				chapterName.Visible = true;
				textboxTitle.Visible = false;
				sfxCategorySelect.Play();
				return;
			}

			pageSelection = WrapSelection(pageSelection + (5 * input.Y), 14, pageSelection - 10); // Wraps the selection vertically
			windows[pageSelection].Select();
			LoadPageData();

			sfxSelect.Play();
		}
	}

	public void PlayRandomPage()
	{
		tabs[tabSelection].Deselect();
		windows[pageSelection].Deselect();
		do
		{
			seekRandom = WrapSelection(seekRandom + 1, randomPages.Count);
		} while (!IsValid((SpecialBookPage)randomPages[seekRandom], menuStatus));

		tabSelection = GetChapterFromPage((SpecialBookPage)randomPages[seekRandom]);
		pageSelection = GetSelectionFromPage((SpecialBookPage)randomPages[seekRandom]);

		tabs[tabSelection].Select();
		windows[pageSelection].Select();

		LoadChapterData();
		LoadPageData((SpecialBookPage)randomPages[seekRandom]);
	}

	private int GetSelectionFromPage(SpecialBookPage page)
	{
		foreach (SpecialBookTab tab in tabs)
		{
			for (int i = 0; i < tab.PageResources.Length; i++)
			{
				if (page == tab.PageResources[i])
					return i;
			}
		}

		return 0;
	}

	private int GetChapterFromPage(SpecialBookPage page)
	{
		for (int i = 0; i < tabs.Length; i++)
		{
			foreach (SpecialBookPage p in tabs[i].PageResources)
			{
				if (page == p)
					return i;
			}
		}

		return 0;
	}

	private void RandomizeList()
	{
		if (randomPages.Count == 0)
		{
			// Populate the random page list
			foreach (SpecialBookTab tab in tabs)
			{
				foreach (SpecialBookPage page in tab.PageResources)
					randomPages.Add(page);
			}
		}

		randomPages.Shuffle();
	}

	/// <summary> 
	/// Checks if we can view a page.
	/// </summary>
	/// <returns> Returns true if the page is viewable. </returns>
	private bool IsValid(SpecialBookPage page, MenuStatusEnum focus)
	{
		if (!page.IsUnlocked())
			return false;

		if (focus == MenuStatusEnum.Page || focus == MenuStatusEnum.Description)
			return true;

		if (focus == MenuStatusEnum.Image && page.PageType == SpecialBookPage.PageTypeEnum.Image) // If this is an image, then we can view it
			return true;

		return false;
	}

	/// <summary> Loads a chapter based on the current chapterSelection. </summary>
	private void LoadChapterData()
	{
		SpecialBookTab tab = tabs[tabSelection];
		chapterLabel.Text = Tr("spb_chapter") + " " + (tabSelection + 1);
		chapterName.Text = "[" + Tr("spb_chapter_" + tab.chapterType.ToString().ToLower()) + "]";

		// Load preview images
		for (int i = 0; i < tab.PageResources.Length; i++)
		{
			if (tab.PageResources[i] != null && tab.PageResources[i].IsUnlocked())
			{
				thumbnails[i].Texture = tab.PageThumbnail;
				continue;
			}

			thumbnails[i].Texture = null;
		}
	}

	private void LoadPageData(SpecialBookPage page = null)
	{
		if (page == null)
			page = GetActivePage;

		menuMemory[MemoryKeys.SpecialBook] = (15 * tabSelection) + pageSelection;
		switch (page.PageType)
		{
			case SpecialBookPage.PageTypeEnum.Image:
				navZoom.Visible = true;
				navPlay.Visible = false;
				break;
			case SpecialBookPage.PageTypeEnum.Music:
				navZoom.Visible = false;
				navPlay.Visible = true;
				break;
			case SpecialBookPage.PageTypeEnum.Video:
				navZoom.Visible = false;
				navPlay.Visible = false;
				break;
		}

		/*
		if (page.IsUnlocked())
		{
			textboxTitle.Text = chapterName.Text + "\n" + Tr(page.name);
			previewDescription.Text = Tr(page.name.Replace("title", "desc"));
			previewImage.Texture = page.previewImage;
			previewNumber.Text = "-" + ((15 * tabSelection) + pageSelection + 1).ToString("D3") + "-";
			fullImage.Texture = page.fullImage;
		}
		else
		{
			textboxTitle.Text = LoadHint(page);
		}
	}

	private string LoadHint(SpecialBookPage page)
	{
		if (page.unlockSilver)
			return Tr("spb_hint_silvermedal").Replace("XX", page.unlockSilverMedalRequirement.ToString());

		if (page.unlockClear)
			return Tr("spb_hint_complete_" + AbbreviateWorld(page.unlockWorld)).Replace("XX", page.unlockStageNumber.ToString());

		if (page.unlockGold)
			return Tr("spb_hint_goldmedal_" + AbbreviateWorld(page.unlockWorld)).Replace("XX", page.unlockStageNumber.ToString());

		if (page.unlockAllStage)
			return Tr("spb_hint_allmission_" + AbbreviateWorld(page.unlockWorld));

		return "???";
		*/
	}

	private string AbbreviateWorld(SaveManager.WorldEnum world)
	{
		return world switch
		{
			SaveManager.WorldEnum.LostPrologue => "lp",
			SaveManager.WorldEnum.SandOasis => "so",
			SaveManager.WorldEnum.DinosaurJungle => "dj",
			SaveManager.WorldEnum.EvilFoundry => "ef",
			SaveManager.WorldEnum.LevitatedRuin => "lr",
			SaveManager.WorldEnum.PirateStorm => "ps",
			SaveManager.WorldEnum.SkeletonDome => "sd",
			SaveManager.WorldEnum.NightPalace => "np",
			_ => string.Empty,
		};
	}

	/// <summary>
	/// Checks the number of entries the players have unlocked.
	/// </summary>
	/// <returns> The number of entries unlocked. </returns>
	private int GetUnlockedEntriesCount()
	{
		int num = 0;

		for (int i = 0; i < tabs.Length; i++)
		{
			for (int j = 0; j < windows.Length; j++)
			{
				SpecialBookPage bookPage = tabs[i].PageResources[j];
				if (bookPage.IsUnlocked() && bookPage.PageType == SpecialBookPage.PageTypeEnum.Image)
					num++;
			}
		}

		return num;
	}
}
