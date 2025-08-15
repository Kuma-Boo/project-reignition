using Godot;
using Project.Core;
using System.Collections.Generic;

namespace Project.Interface.Menus;

public partial class SpecialBook : Menu
{
	private int tabSelection;
	private int pageSelection;

	private SpecialBookPage GetActivePage => tabs[tabSelection].PageResources[pageSelection];

	/// <summary> Keeps track of the entry the player is currently focusing on. </summary>
	private MenuStateEnum menuState;
	private enum MenuStateEnum
	{
		Chapter, // Player is selecting a chapter
		Page, // Player is selecting a page
		Entry, // Player is inspecting a particular entry
		Image // Player is inspecting an image
	}

	[Export] private SpecialBookTab[] tabs;
	[Export] private SpecialBookWindow[] windows;
	[Export] private Sprite2D[] thumbnails;

	[Export] private Label chapterName;
	[Export] private Label chapterLabel;
	[Export] private Label textboxTitle;
	[Export] private Label previewDescription;

	[Export] private Label previewNumber;
	[Export] private TextureRect previewTextureRect;
	[Export] private TextureRect fullTextureRect;

	[Export] private AnimationPlayer navigationAnimator;
	private bool isNavigationVisible;

	[Export] private AudioStreamPlayer audioPlayer;

	[Export] private AudioStreamPlayer sfxOpen;
	[Export] private AudioStreamPlayer sfxSelect;
	[Export] private AudioStreamPlayer sfxCancel;
	[Export] private AudioStreamPlayer sfxConfirm;
	[Export] private AudioStreamPlayer sfxCategorySelect;

	private bool isPlayingSlideshow;
	private int slideshowIndex;
	private readonly List<SpecialBookPage> slideshowPages = [];

	protected override void SetUp()
	{
		audioPlayer.Finished += PlayBgm;

		foreach (SpecialBookTab tab in tabs)
		{
			tab.PreviewTextureLoaded += OnPreviewTextureLoaded;
			tab.FullTextureLoaded += OnFullTextureLoaded;
			tab.Initialize();
		}

		foreach (SpecialBookWindow window in windows)
			window.Initialize();

		// Populate the random page list with unlocked pages
		foreach (SpecialBookTab tab in tabs)
		{
			foreach (SpecialBookPage page in tab.PageResources)
			{
				if (page.IsUnlocked())
					slideshowPages.Add(page);
			}
		}
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

			// Open page
			OpenEntry(true);
			return;
		}

		tabs[tabSelection].Select(); // Select chapter tab
		base.ShowMenu();
	}

	/// <summary> Opens an entry. </summary>
	private void OpenEntry(bool openInstantly)
	{
		menuState = MenuStateEnum.Entry;
		animator.Play("show-entry");

		if (openInstantly)
			animator.Seek(animator.CurrentAnimationLength);
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
		if (menuState == MenuStateEnum.Chapter || menuState == MenuStateEnum.Page) // Change chapter by using the bumpers
		{
			if (Input.IsActionJustPressed("button_step_left"))
				ProcessTabSelection(Vector2I.Left, menuState == MenuStateEnum.Page);

			if (Input.IsActionJustPressed("button_step_right"))
				ProcessTabSelection(Vector2I.Right, menuState == MenuStateEnum.Page);

			if (Runtime.Instance.IsActionJustPressed("sys_pause", "ui_accept") && !Input.IsActionJustPressed("toggle_fullscreen"))
				StartSlideshow();
		}

		base.ProcessMenu();
	}

	protected override void UpdateSelection()
	{
		Vector2I input = new(Mathf.Sign(Input.GetAxis("ui_left", "ui_right")), Mathf.Sign(Input.GetAxis("ui_up", "ui_down")));
		StartSelectionTimer();
		ProcessMenuInput(input);
	}

	protected override void Confirm()
	{
		if (menuState == MenuStateEnum.Chapter)
		{
			ProcessMenuInput(Vector2I.Down);
			return;
		}

		if (menuState == MenuStateEnum.Image)
		{
			isNavigationVisible = !isNavigationVisible;
			navigationAnimator.Play(isNavigationVisible ? "show" : "hide");
			return;
		}

		SpecialBookPage page = GetActivePage;

		if (menuState == MenuStateEnum.Page)
		{
			if (page.IsUnlocked())
				OpenEntry(false);

			return;
		}

		if (!page.IsUnlocked())
			return;

		switch (page.PageType)
		{
			case SpecialBookPage.PageTypeEnum.Image:
				StartImage();
				break;
			case SpecialBookPage.PageTypeEnum.Music:
				bgm.Stop();
				audioPlayer.Stream = ResourceLoader.Load<AudioStream>(page.AudioStreamPath);
				audioPlayer.Play();
				break;
			case SpecialBookPage.PageTypeEnum.Video:
				bgm.Stop();

				TransitionManager.QueueSceneChange(page.VideoEventPath);
				TransitionManager.StartTransition(new()
				{
					color = Colors.Black,
					inSpeed = .5f,
				});

				DisableProcessing();
				break;
		}
	}

	protected override void Cancel()
	{
		if (menuState == MenuStateEnum.Chapter)
		{
			// Reset menu memory
			menuMemory[MemoryKeys.SpecialBook] = 0;

			tabs[tabSelection].Deselect();
			animator.Play("hide");
			return;
		}

		if (menuState == MenuStateEnum.Page)
		{
			tabs[tabSelection].SelectNoMove();
			windows[pageSelection].Deselect();
			menuState = MenuStateEnum.Chapter;

			chapterName.Visible = true;
			textboxTitle.Visible = false;
			sfxCategorySelect.Play();
		}

		if (menuState == MenuStateEnum.Entry)
		{
			animator.Play("hide-entry");
			menuState = MenuStateEnum.Page;
		}

		if (menuState == MenuStateEnum.Image)
		{
			if (!isPlayingSlideshow)
			{
				animator.Play("hide-image");
				menuState = MenuStateEnum.Entry;
			}
			else
			{
				animator.Play("hide-random");
				menuState = MenuStateEnum.Page;
				isPlayingSlideshow = false;
				tabs[tabSelection].DeselectNoGlow();
				windows[pageSelection].Select();
			}
		}
	}

	private void StartImage()
	{
		menuState = MenuStateEnum.Image;
		animator.Play("show-image");

		isNavigationVisible = true;
		navigationAnimator.Play("RESET");

		StartSelectionTimer();
	}

	private void StartSlideshow()
	{
		if (GetUnlockedEntriesCount() <= 1) // Not enough entries for a slideshow
			return;

		for (int i = 0; i < slideshowPages.Count; i++) // Randomize the pages
		{
			int targetIndex = Runtime.randomNumberGenerator.RandiRange(0, slideshowPages.Count - 1);
			SpecialBookPage swapPage = slideshowPages[i];
			slideshowPages[i] = slideshowPages[targetIndex];
			slideshowPages[targetIndex] = slideshowPages[i];
		}

		slideshowIndex = 0;
		isPlayingSlideshow = true;
		menuState = MenuStateEnum.Image;
		animator.Play("show-random");
	}

	private void ProcessMenuInput(Vector2I input)
	{
		if (isPlayingSlideshow)
			return;

		if (menuState == MenuStateEnum.Chapter)
		{
			ProcessTabSelection(input);
			return;
		}

		if (menuState == MenuStateEnum.Page)
		{
			ProcessWindowSelection(input);
			return;
		}

		if (input.X == 0)
			return;

		windows[pageSelection].Deselect();
		int currentTab = tabSelection;
		do
		{
			pageSelection += input.X;
			if (pageSelection > 14 || pageSelection < 0)
			{
				pageSelection = WrapSelection(pageSelection, 15);
				tabSelection = WrapSelection(tabSelection + input.X, 16);
				sfxOpen.Play();
			}

		} while (!CanSelectPage(GetActivePage)); // Skips over every page we don't have unlocked. If we're on the full view, skips over movies and music too

		if (currentTab != tabSelection)
		{
			tabs[currentTab].Deselect();
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

	private void ProcessTabSelection(Vector2I input, bool autoSelect = false)
	{
		if (input.X != 0) // move left or right
		{
			tabs[tabSelection].Deselect();
			tabSelection = WrapSelection(tabSelection + (int)input.X, tabs.Length);
			tabs[tabSelection].Select();

			LoadChapterData();
			sfxCategorySelect.Play();

			if (!autoSelect)
				return;
		}

		if (input.Y > 0 || autoSelect) // Move down to the pages
		{
			tabs[tabSelection].DeselectNoGlow();

			if (menuState != MenuStateEnum.Page)
			{
				menuState = MenuStateEnum.Page;
				pageSelection = 0;
				windows[pageSelection].Select();
			}

			chapterName.Visible = false;
			textboxTitle.Visible = true;

			LoadPageData();

			if (!autoSelect)
				sfxSelect.Play();
		}
	}

	private void ProcessWindowSelection(Vector2I input)
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
				menuState = MenuStateEnum.Chapter;
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
			slideshowIndex = WrapSelection(slideshowIndex + 1, slideshowPages.Count);
		} while (!CanSelectPage((SpecialBookPage)slideshowPages[slideshowIndex]));

		tabSelection = GetChapterFromPage((SpecialBookPage)slideshowPages[slideshowIndex]);
		pageSelection = GetSelectionFromPage((SpecialBookPage)slideshowPages[slideshowIndex]);

		tabs[tabSelection].Select();
		windows[pageSelection].Select();

		LoadChapterData();
		LoadPageData((SpecialBookPage)slideshowPages[slideshowIndex]);
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

	/// <summary> Checks if we can view a page. </summary>
	/// <returns> Returns true if the page is viewable. </returns>
	private bool CanSelectPage(SpecialBookPage page)
	{
		if (!page.IsUnlocked())
			return false;

		// We can view any page (except achievements) when we're not inspecting an image closely
		if (menuState != MenuStateEnum.Image && page.PageType != SpecialBookPage.PageTypeEnum.Achievement)
			return true;

		// Otherwise, we can only switch to other images 
		return page.PageType == SpecialBookPage.PageTypeEnum.Image;
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
				navigationAnimator.Play("image");
				break;
			case SpecialBookPage.PageTypeEnum.Music:
			case SpecialBookPage.PageTypeEnum.Video:
				navigationAnimator.Play("media");
				break;
		}

		if (page.IsUnlocked())
		{
			string pageKey = $"spb_title_ch{tabSelection + 1}_{pageSelection + 1}";
			textboxTitle.Text = chapterName.Text + "\n" + Tr(pageKey);
			previewDescription.Text = Tr(pageKey.Replace("title", "desc"));
			previewNumber.Text = "-" + ((15 * tabSelection) + pageSelection + 1).ToString("D3") + "-";

			previewTextureRect.Texture = tabs[tabSelection].GetPreviewTexture(pageSelection);
			fullTextureRect.Texture = tabs[tabSelection].GetFullTexture(pageSelection);
		}
		else
		{
			textboxTitle.Text = page.GetLocalizedUnlockRequirements();
		}
	}

	private void OnPreviewTextureLoaded(int tabIndex, int pageIndex)
	{
		if (pageIndex != pageSelection || tabIndex != tabSelection)
			return;

		previewTextureRect.Texture = tabs[tabSelection].GetPreviewTexture(pageSelection);
	}

	private void OnFullTextureLoaded(int tabIndex, int pageIndex)
	{
		if (pageIndex != pageSelection || tabIndex != tabSelection)
			return;

		fullTextureRect.Texture = tabs[tabSelection].GetFullTexture(pageSelection);
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
