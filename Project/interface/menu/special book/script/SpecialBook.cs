using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SpecialBook : Menu
{
	private int chapterSelection;
	private int pageSelection;

	/// <summary> Keeps track of the entry the player is currently focusing on. </summary>
	private MenuFocus menuFocus;
	private enum MenuFocus
	{
		Chapter,
		Page,
		Description,
		Image,
		Video
	}

	[Export] private SpecialBookTab[] tabs;
	[Export] private SpecialBookWindow[] pages;
	[Export] private Sprite2D[] previewImages;
	[Export] private BookChapter[] chapters;

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
	[Export] private BGMPlayer player;

	[Export] private AudioStreamPlayer sfxOpen;
	[Export] private AudioStreamPlayer sfxSelect;
	[Export] private AudioStreamPlayer sfxCancel;
	[Export] private AudioStreamPlayer sfxConfirm;
	[Export] private AudioStreamPlayer sfxCategorySelect;

	[Export] private AnimationPlayer randomAnimator;
	private bool playRandom;
	private int seekRandom;
	private readonly Array randomPages = [];

	protected override void SetUp()
	{
		for (int i = 0; i < tabs.Length; i++)
			tabs[i].ChangeTab();

		for (int i = 0; i < pages.Length; i++)
			pages[i].Glow();
	}

	public override void ShowMenu()
	{
		menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.SpecialBook;
		LoadSaveData();

		// Update selections based on memory
		chapterSelection = menuMemory[MemoryKeys.SpecialBook] / pages.Length;
		pageSelection = menuMemory[MemoryKeys.SpecialBook] % pages.Length;

		// Select the tab and load the chapter's data
		LoadChapter();
		PlayBGM();

		if (menuMemory[MemoryKeys.SpecialBook] != 0)
		{
			tabs[chapterSelection].SelectNoGlow();
			// If we are returning from an event scene
			pages[pageSelection].Select();
			chapterName.Visible = false;
			textboxTitle.Visible = true;
			LoadPage(GetPage(chapterSelection, pageSelection));

			// Open description
			menuFocus = MenuFocus.Description;
			animator.Play("show_description");
			animator.Advance(animator.CurrentAnimationLength);
			return;
		}

		tabs[chapterSelection].Select(); // Select chapter tab
		base.ShowMenu();
	}

	public override void OpenParentMenu()
	{
		// Return to main menu
		FadeBGM(.5f);
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
		if (menuFocus == MenuFocus.Chapter || menuFocus == MenuFocus.Page) // Change chapter by using the bumpers
		{
			if (Input.IsActionJustPressed("button_step_left"))
			{
				tabs[chapterSelection].Deselect();
				chapterSelection = WrapSelection(chapterSelection - 1, 16);

				if (menuFocus == MenuFocus.Chapter)
					tabs[chapterSelection].Select();
				else
					tabs[chapterSelection].SelectNoGlow();

				LoadChapter();
				LoadPage(GetPage(chapterSelection, pageSelection));

				sfxCategorySelect.Play();
			}

			if (Input.IsActionJustPressed("button_step_right"))
			{
				tabs[chapterSelection].Deselect();
				chapterSelection = WrapSelection(chapterSelection + 1, 16);

				if (menuFocus == MenuFocus.Chapter)
					tabs[chapterSelection].Select();
				else
					tabs[chapterSelection].SelectNoGlow();

				LoadChapter();
				LoadPage(GetPage(chapterSelection, pageSelection));

				sfxCategorySelect.Play();
			}

			if (Input.IsActionJustPressed("button_pause"))
			{
				if (GetUnlockedEntriesCount() > 1) // If we have more than one page unlocked, then play the slideshow
				{
					RandomizeList();
					playRandom = true;
					seekRandom = 0;
					menuFocus = MenuFocus.Image;
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

	protected override void UpdateSelection()
	{
		Vector2I input = new(Mathf.Sign(Input.GetAxis("ui_left", "ui_right")), Mathf.Sign(Input.GetAxis("ui_up", "ui_down")));

		if (input == Vector2I.Zero)
			return;

		StartSelectionTimer();
		MenuControls(input);
	}

	protected override void Confirm()
	{
		BookPage thisPage = GetPage(chapterSelection, pageSelection);

		if (menuFocus == MenuFocus.Chapter)
		{
			MenuControls(new Vector2I(0, 1));
			sfxSelect.Play();
			return;
		}

		if (menuFocus == MenuFocus.Page)
		{
			if (GetPage(chapterSelection, pageSelection).Unlocked)
			{
				animator.Play("show_description");
				menuFocus = MenuFocus.Description;
				sfxConfirm.Play();
			}

			return;
		}

		if (menuFocus == MenuFocus.Description)
		{
			if (GetPage(chapterSelection, pageSelection).Unlocked)
			{
				if (thisPage.fullImage != null) // If this is an image
				{
					menuFocus = MenuFocus.Image;
					animator.Play("show_fullimage");

					isNavigationVisible = true;
					navigationAnimator.Play("RESET");

					sfxOpen.Play();
					StartSelectionTimer();
					return;
				}

				if (!string.IsNullOrEmpty(thisPage.videoFilePath))
				{
					bgm.Stop();
					menuFocus = MenuFocus.Video;

					TransitionManager.QueueSceneChange(thisPage.videoFilePath);
					TransitionManager.StartTransition(new()
					{
						color = Colors.Black,
						inSpeed = .5f,
					});
				}

				if (thisPage.track != null)
				{
					// TODO Improve audio controls or actually have the bgm loop.
					bgm.Stop();
					player.Stream = thisPage.track;
					player.loopEndPosition = (float)player.Stream.GetLength() - 1f;
					player.Play();
				}
			}

			return;
		}

		if (menuFocus == MenuFocus.Image)
		{
			isNavigationVisible = !isNavigationVisible;
			navigationAnimator.Play(isNavigationVisible ? "show" : "hide");
		}
	}

	protected override void Cancel()
	{
		if (menuFocus == MenuFocus.Chapter)
		{
			// Reset menu memory
			menuMemory[MemoryKeys.SpecialBook] = 0;

			tabs[chapterSelection].Deselect();
			animator.Play("hide");
			return;
		}

		if (menuFocus == MenuFocus.Page)
		{
			tabs[chapterSelection].SelectNoMove();
			pages[pageSelection].Deselect();
			menuFocus = MenuFocus.Chapter;

			chapterName.Visible = true;
			textboxTitle.Visible = false;
			sfxCategorySelect.Play();
		}

		if (menuFocus == MenuFocus.Description)
		{
			animator.Play("hide_description");
			menuFocus = MenuFocus.Page;
			sfxCancel.Play();
		}

		if (menuFocus == MenuFocus.Image)
		{
			if (!playRandom)
			{
				animator.Play("hide_fullimage");
				menuFocus = MenuFocus.Description;
			}
			else
			{
				animator.Play("hide_playrandom");
				randomAnimator.Stop();
				menuFocus = MenuFocus.Page;
				playRandom = false;
				tabs[chapterSelection].DeselectNoGlow();
				pages[pageSelection].Select();
			}

			sfxCancel.Play();
		}
	}

	private void MenuControls(Vector2I input)
	{
		if (menuFocus == MenuFocus.Chapter)
		{
			ProcessChapterSelection(input);
			return;
		}

		if (menuFocus == MenuFocus.Page)
		{
			ProcessPageSelection(input);
			return;
		}

		if (menuFocus == MenuFocus.Description || menuFocus == MenuFocus.Image)
		{
			if (input.X == 0 || playRandom)
				return;

			pages[pageSelection].Deselect();

			int ogChapter = chapterSelection;
			do
			{
				pageSelection += input.X;
				if (pageSelection > 14 || pageSelection < 0)
				{
					pageSelection = WrapSelection(pageSelection, 15);
					chapterSelection = WrapSelection(chapterSelection + input.X, 16);
					sfxOpen.Play();
				}

			} while (!IsValid(GetPage(chapterSelection, pageSelection), menuFocus)); // Skips over every page we don't have unlocked. If we're on the full view, skips over movies and music too

			if (ogChapter != chapterSelection)
			{
				tabs[ogChapter].Deselect();
				tabs[chapterSelection].SelectNoGlow();
			}

			if (input.X > 0)
				sfxConfirm.Play();
			else if (input.X < 0)
				sfxCancel.Play();

			pages[pageSelection].Select();
			LoadChapter();
			LoadPage(GetPage(chapterSelection, pageSelection));
		}
	}

	private void ProcessChapterSelection(Vector2I input)
	{
		if (input.X != 0) // move left or right
		{
			tabs[chapterSelection].Deselect();
			chapterSelection = WrapSelection(chapterSelection + (int)input.X, 16);
			tabs[chapterSelection].Select();

			LoadChapter();
			sfxCategorySelect.Play();
			return;
		}

		if (input.Y > 0) // Move down to the pages
		{
			tabs[chapterSelection].DeselectNoGlow();
			menuFocus = MenuFocus.Page;
			pageSelection = 0;
			pages[pageSelection].Select();
			chapterName.Visible = false;
			textboxTitle.Visible = true;

			LoadPage(GetPage(chapterSelection, pageSelection));
			sfxSelect.Play();
		}
	}

	private void ProcessPageSelection(Vector2I input)
	{
		if (input.X != 0) // If we are going left or right
		{
			pages[pageSelection].Deselect();

			if (pageSelection <= 4) // row 1
				pageSelection = WrapSelection(pageSelection + input.X, 4, 0);
			else if (pageSelection >= 5 && pageSelection <= 9) // row 2
				pageSelection = WrapSelection(pageSelection + input.X, 9, 5);
			else if (pageSelection >= 10 && pageSelection <= 14) // row 3
				pageSelection = WrapSelection(pageSelection + input.X, 14, 10);

			pages[pageSelection].Select();
			LoadPage(GetPage(chapterSelection, pageSelection));

			sfxSelect.Play();
		}
		if (input.Y != 0)
		{
			pages[pageSelection].Deselect();

			if (input.Y < 0 && pageSelection <= 4) // If we are going up on the first row
			{
				tabs[chapterSelection].SelectNoMove();
				menuFocus = MenuFocus.Chapter;
				chapterName.Visible = true;
				textboxTitle.Visible = false;
				sfxCategorySelect.Play();
				return;
			}

			pageSelection = WrapSelection(pageSelection + (5 * input.Y), 14, pageSelection - 10); // Wraps the selection vertically
			pages[pageSelection].Select();
			LoadPage(GetPage(chapterSelection, pageSelection));

			sfxSelect.Play();
		}
	}

	public void PlayRandomPage()
	{
		tabs[chapterSelection].Deselect();
		pages[pageSelection].Deselect();
		do
		{
			seekRandom = WrapSelection(seekRandom + 1, randomPages.Count);
		} while (!IsValid((BookPage)randomPages[seekRandom], menuFocus));

		chapterSelection = GetChapterFromPage((BookPage)randomPages[seekRandom]);
		pageSelection = GetSelectionFromPage((BookPage)randomPages[seekRandom]);

		tabs[chapterSelection].Select();
		pages[pageSelection].Select();

		LoadChapter();
		LoadPage((BookPage)randomPages[seekRandom]);
	}

	private int GetSelectionFromPage(BookPage page)
	{
		for (int chapter = 0; chapter < chapters.Length; chapter++)
		{
			for (int pages = 0; pages < 15; pages++)
			{
				if (page == chapters[chapter].pages[pages])
					return pages;
			}
		}
		return 0;
	}

	private int GetChapterFromPage(BookPage page)
	{
		for (int chapter = 0; chapter < chapters.Length; chapter++)
		{
			for (int pages = 0; pages < this.pages.Length; pages++)
			{
				if (page == chapters[chapter].pages[pages])
					return chapter;
			}
		}
		return 0;
	}

	private void RandomizeList()
	{
		randomPages.Clear();
		for (int i = 0; i < 16; i++)
		{
			for (int j = 0; j < pages.Length; j++)
				randomPages.Add(GetPage(i, j));
		}
	}

	/// <summary> 
	/// Checks if we can view a page.
	/// </summary>
	/// <returns> Returns true if the page is viewable. </returns>
	private bool IsValid(BookPage page, MenuFocus focus)
	{
		if (!page.Unlocked)
			return false;

		if (focus == MenuFocus.Page || focus == MenuFocus.Description)
		{
			return true;
		}
		else if (focus == MenuFocus.Image)
		{
			if (page.fullImage != null) // If this is an image, then we can view it
				return true;
		}
		return false;
	}

	private void LoadSaveData()
	{
		SaveManager.LoadGameData();

		for (int i = 0; i < chapters.Length; i++)
		{
			foreach (BookPage page in chapters[i].pages)
			{
				// Revert page to locked and unlock based on save data
				page.Unlocked = false;

				if (page.unlockClear)
				{
					foreach (SaveManager.GameData data in SaveManager.GameSaveSlots)
					{
						if (data.GetRank((StringName)page.StageUnlock()) > 0) // If we have at least a bronze medal
						{
							page.Unlocked = true;
							break;
						}
					}
				}

				if (page.unlockGold)
				{
					foreach (SaveManager.GameData data in SaveManager.GameSaveSlots)
					{
						if (data.GetRank((StringName)page.StageUnlock()) > 2) // If we have a gold medal
						{
							page.Unlocked = true;
							break;
						}
					}
				}

				if (page.unlockSilver)
				{
					int silver = 0;
					for (int world = 0; world < 8; world++)
					{
						for (int level = 1; level < 30; level++) // Iterate through each level in each world
						{
							foreach (SaveManager.GameData data in SaveManager.GameSaveSlots) // Search each save file for that level
							{
								if (page.StageUnlock((SaveManager.WorldEnum)world, level) != "")
								{
									if (data.GetRank((StringName)page.StageUnlock((SaveManager.WorldEnum)world, level)) > 1) // If we ahve a silver medal, add one to the counter, and stop searching saves
									{
										silver++;
										break;
									}
								}
							}
							if (silver >= page.unlockSilverMedalRequirement)
								break;
						}
						if (silver >= page.unlockSilverMedalRequirement)
						{
							page.Unlocked = true;
							break;
						}
					}
				}

				if (page.unlockAllStage)
				{
					int gold = 0;
					for (int level = 1; level < page.NumStages(page.unlockWorld) + 1; level++) // Go through each level
					{
						foreach (SaveManager.GameData data in SaveManager.GameSaveSlots) // Search each save file for that level
						{
							if (data.GetRank((StringName)page.StageUnlock(page.unlockWorld, level)) == 3) // If we have a gold medal, add one to the counter, and stop searching saves
							{
								gold++;
								break;
							}
						}
						if (gold == page.NumStages(page.unlockWorld))
						{
							page.Unlocked = true;
							break;
						}

					}

				}

			}
		}

	}

	private BookPage GetPage(int chapter, int page) => chapters[chapter].pages[page];

	/// <summary> Loads a chapter based on the current chapterSelection. </summary>
	private void LoadChapter()
	{
		BookChapter chapter = chapters[chapterSelection];
		chapterLabel.Text = Tr("spb_chapter") + " " + (chapterSelection + 1);
		chapterName.Text = "[" + Tr("spb_chapter_" + tabs[chapterSelection].thisChapterType.ToString().ToLower()) + "]";

		// Load preview images
		for (int i = 0; i < chapter.pages.Length; i++)
		{
			if (chapter.pages[i] != null && chapter.pages[i].Unlocked)
			{
				previewImages[i].Texture = chapter.pagePreview;
				continue;
			}

			previewImages[i].Texture = null;
		}
	}

	private void LoadPage(BookPage page)
	{
		menuMemory[MemoryKeys.SpecialBook] = (15 * chapterSelection) + (pageSelection);
		if (page.fullImage != null)
		{
			navZoom.Visible = true;
			navPlay.Visible = false;
		}
		else if (page.videoFilePath != "" || page.track != null)
		{
			navZoom.Visible = false;
			navPlay.Visible = true;
		}

		if (page.Unlocked)
		{
			textboxTitle.Text = chapterName.Text + "\n" + Tr(page.name);
			previewDescription.Text = Tr(page.name.Replace("title", "desc"));
			previewImage.Texture = page.previewImage;
			previewNumber.Text = "-" + ((15 * chapterSelection) + pageSelection + 1).ToString("D3") + "-";
			fullImage.Texture = page.fullImage;
		}
		else
		{
			textboxTitle.Text = LoadHint(page);
		}
	}

	private string LoadHint(BookPage page)
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

		for (int chapter = 0; chapter < chapters.Length; chapter++)
		{
			for (int page = 0; page < pages.Length; page++)
			{
				BookPage bookPage = GetPage(chapter, page);
				if (bookPage.Unlocked == true && IsValid(bookPage, MenuFocus.Image))
					num++;
			}
		}

		return num;
	}

	/// <summary> A debug method that unlocks all the pages for testing. </summary>
	private void UnlockAll()
	{
		for (int chapter = 0; chapter < chapters.Length; chapter++)
		{
			for (int page = 0; page < pages.Length; page++)
				GetPage(chapter, page).Unlocked = true;
		}
	}
}
