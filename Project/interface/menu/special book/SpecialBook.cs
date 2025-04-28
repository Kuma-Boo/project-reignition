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

	[Export] private AnimationPlayer animatorNav;
	[Export] private AnimationPlayer animatorRandom;

	[Export] private NavigationButton navZoom;
	[Export] private NavigationButton navPlay;
	[Export] private BGMPlayer player;

	[Export] private AudioStreamPlayer sfxOpen;

	[Export] private AudioStreamPlayer sfxSelect;

	[Export] private AudioStreamPlayer sfxCancel;

	[Export] private AudioStreamPlayer sfxConfirm;

	[Export] private AudioStreamPlayer sfxCategorySelect;

	private bool playRandom = false;
	private int seekRandom = 0;
	private readonly Array randomPages = [];

	protected override void SetUp()
	{
		menuFocus = MenuFocus.Chapter;

		for (int i = 0; i < tabs.Length; i++)
			tabs[i].ChangeTab();

		for (int i = 0; i < pages.Length; i++)
			pages[i].Glow();
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
					tabs[chapterSelection].Select_NoGlow();

				LoadChapter(chapters[chapterSelection].pages);
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
					tabs[chapterSelection].Select_NoGlow();

				LoadChapter(chapters[chapterSelection].pages);
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
					animatorRandom.Play("playrandom");
					animatorRandom.Seek(0.0);

					sfxOpen.Play();
				}


			}
		}


		base.ProcessMenu();
	}

	protected override void UpdateSelection()
	{
		// BUG: Only dpad seems to work, stick doesn't
		Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

		if (input == Vector2.Zero)
			return;

		if (menuFocus == MenuFocus.Image && !playRandom)
		{
			// Hides the nav buttons after 5 seconds of inactivity
			animatorNav.Play("hide_navbuttons");
			animatorNav.Seek(0.0);
		}

		StartSelectionTimer();
		MenuControls(input);
	}

	protected override void Confirm()
	{
		BookPage thisPage = GetPage(chapterSelection, pageSelection);

		if (menuFocus == MenuFocus.Chapter)
		{
			MenuControls(new Vector2(0, 1));
			sfxSelect.Play();
			return;
		}
		if (menuFocus == MenuFocus.Page)
		{
			if (GetPage(chapterSelection, pageSelection).unlocked)
			{
				animator.Play("show_description");
				menuFocus = MenuFocus.Description;
				sfxConfirm.Play();
				return;
			}

		}
		if (menuFocus == MenuFocus.Description)
		{
			if (GetPage(chapterSelection, pageSelection).unlocked)
			{
				if (thisPage.page_full != null) // if this is an image
				{
					animator.Play("show_fullimage");
					menuFocus = MenuFocus.Image;
					animatorNav.Play("hide_navbuttons");
					animatorNav.Seek(0.0);
					sfxOpen.Play();
					StartSelectionTimer();
				}

				if (thisPage.videoFilePath != "")
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
					bgm.Stop();
					player.Stream = thisPage.track;
					player.loopEndPosition = (float)player.Stream.GetLength() - 1f;
					player.Play();
				}

			}
		}
	}

	protected override void Cancel()
	{
		if (menuFocus == MenuFocus.Chapter)
		{
			menuMemory[MemoryKeys.SpecialBook] = 0;

			tabs[chapterSelection].Deselect();
			animator.Play("hide");
			return;
		}

		if (menuFocus == MenuFocus.Page)
		{
			tabs[chapterSelection].Select_NoMove();
			pages[pageSelection].Deselect();
			menuFocus = 0;

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
				animatorRandom.Stop();
				menuFocus = MenuFocus.Page;
				playRandom = false;
				tabs[chapterSelection].Deselect_NoGlow();
				pages[pageSelection].Select();
			}
			sfxCancel.Play();

		}

	}

	public override void ShowMenu()
	{
		menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.SpecialBook;

		menuFocus = 0;
		playRandom = false;

		if (menuMemory[MemoryKeys.SpecialBook] != 0) // If we are returning from an event scene
		{
			chapterSelection = GetChapterFromMemory();
			pageSelection = GetPageFromMemory() + 1;

			tabs[chapterSelection].Select_NoGlow();
			pages[pageSelection].Select();

			chapterName.Visible = false;
			textboxTitle.Visible = true;

			menuFocus = MenuFocus.Page;

			GD.Print("Loading Chapter " + chapterSelection);
			LoadChapter(chapters[chapterSelection].pages);
			LoadPage(GetPage(chapterSelection, pageSelection));

		}
		else
		{
			chapterSelection = 0;
			pageSelection = 0;

			tabs[0].Select_NoSFX();
		}

		for (int chapter = 0; chapter < chapters.Length; chapter++)
		{
			for (int page = 0; page < pages.Length; page++)
				GetPage(chapter, page).unlocked = false;
		}

		LoadSaveData();
		base.ShowMenu();
	}

	public override void OpenParentMenu()
	{
		//  Return to main menu
		FadeBGM(.5f);
		menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.MainMenu;
		TransitionManager.QueueSceneChange(TransitionManager.MenuScenePath);
		TransitionManager.StartTransition(new()
		{
			color = Colors.Black,
			inSpeed = .5f,
		});
	}

	private void MenuControls(Vector2 input)
	{
		if (menuFocus == MenuFocus.Chapter)
		{
			if (input.X != 0) // move left or right
			{
				tabs[chapterSelection].Deselect();
				chapterSelection = WrapSelection(chapterSelection + (int)input.X, 16);
				tabs[chapterSelection].Select();

				LoadChapter(chapters[chapterSelection].pages);
				sfxCategorySelect.Play();
				return;
			}

			if (input.Y > 0) // Move down
			{
				tabs[chapterSelection].Deselect_NoGlow();
				menuFocus = MenuFocus.Page;
				pageSelection = 0;
				pages[pageSelection].Select();
				chapterName.Visible = false;
				textboxTitle.Visible = true;

				LoadPage(GetPage(chapterSelection, pageSelection));
				sfxSelect.Play();
				return;
			}
		}
		else if (menuFocus == MenuFocus.Page)
		{
			if (input.X != 0) // If we are going left or right
			{
				pages[pageSelection].Deselect();

				if (pageSelection <= 4) // row 1
					pageSelection = WrapSelection(pageSelection + (int)input.X, 4, 0);
				else if (pageSelection >= 5 && pageSelection <= 9) // row 2
					pageSelection = WrapSelection(pageSelection + (int)input.X, 9, 5);
				else if (pageSelection >= 10 && pageSelection <= 14) // row 3
					pageSelection = WrapSelection(pageSelection + (int)input.X, 14, 10);

				pages[pageSelection].Select();
				LoadPage(GetPage(chapterSelection, pageSelection));

				sfxSelect.Play();
				return;
			}
			if (input.Y != 0)
			{
				pages[pageSelection].Deselect();

				if ((int)input.Y < 0 && pageSelection <= 4) // If we are going up on the first row
				{
					tabs[chapterSelection].Select_NoMove();
					menuFocus = 0;
					chapterName.Visible = true;
					textboxTitle.Visible = false;
					sfxCategorySelect.Play();
					return;
				}

				pageSelection = WrapSelection(pageSelection + (5 * (int)input.Y), 14, pageSelection - 10); // Wraps the selection vertically
				pages[pageSelection].Select();
				LoadPage(GetPage(chapterSelection, pageSelection));

				sfxSelect.Play();
				return;

			}

		}
		else if (menuFocus == MenuFocus.Description || menuFocus == MenuFocus.Image)
		{
			if (input.X != 0 && !playRandom)
			{
				pages[pageSelection].Deselect();

				int ogChapter = chapterSelection;
				do
				{
					pageSelection += (int)input.X;
					if (pageSelection > 14 || pageSelection < 0)
					{
						pageSelection = WrapSelection(pageSelection, 15);
						chapterSelection = WrapSelection(chapterSelection + (int)input.X, 16);
						sfxOpen.Play();
					}

				} while (!IsValid(GetPage(chapterSelection, pageSelection), menuFocus)); // Skips over every page we don't have unlocked. If we're on the full view, skips over movies and music too

				if (ogChapter != chapterSelection)
				{
					tabs[ogChapter].Deselect();
					tabs[chapterSelection].Select_NoGlow();
				}

				if (input.X > 0)
					sfxConfirm.Play();
				else if (input.X < 0)
					sfxCancel.Play();

				pages[pageSelection].Select();
				LoadChapter(GetChapter(chapterSelection));
				LoadPage(GetPage(chapterSelection, pageSelection));
			}
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

		LoadChapter(GetChapter(chapterSelection));
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
		if (!page.unlocked)
			return false;

		if (focus == MenuFocus.Page || focus == MenuFocus.Description)
		{
			return true;
		}
		else if (focus == MenuFocus.Image)
		{
			if (page.page_full != null) // If this is an image, then we can view it
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
				if (page.unlock_clear)
				{
					foreach (SaveManager.GameData data in SaveManager.GameSaveSlots)
					{
						if (data.GetRank((StringName)page.StageUnlock()) > 0) // If we have at least a bronze medal
						{
							page.unlocked = true;
							break;
						}
					}
				}

				if (page.unlock_gold)
				{
					foreach (SaveManager.GameData data in SaveManager.GameSaveSlots)
					{
						if (data.GetRank((StringName)page.StageUnlock()) > 2) // If we have a gold medal
						{
							page.unlocked = true;
							break;
						}
					}
				}

				if (page.unlock_silver)
				{
					int silver = 0;
					for (int world = 0; world < 8; world++)
					{
						for (int level = 1; level < 30; level++) // Iterate through each level in each world
						{
							foreach (SaveManager.GameData data in SaveManager.GameSaveSlots) // Search each save file for that level
							{
								if (page.StageUnlock((BookPage.World)world, level) != "")
								{
									if (data.GetRank((StringName)page.StageUnlock((BookPage.World)world, level)) > 1) // If we ahve a silver medal, add one to the counter, and stop searching saves
									{
										silver++;
										break;
									}
								}
							}
							if (silver >= page.unlock_numSilver)
								break;
						}
						if (silver >= page.unlock_numSilver)
						{
							page.unlocked = true;
							break;
						}
					}
				}

				if (page.unlock_allstage)
				{
					int gold = 0;
					for (int level = 1; level < page.NumStages(page.unlock_world) + 1; level++) // Go through each level
					{
						foreach (SaveManager.GameData data in SaveManager.GameSaveSlots) // Search each save file for that level
						{
							if (data.GetRank((StringName)page.StageUnlock(page.unlock_world, level)) == 3) // If we have a gold medal, add one to the counter, and stop searching saves
							{
								gold++;
								break;
							}
						}
						if (gold == page.NumStages(page.unlock_world))
						{
							page.unlocked = true;
							break;
						}

					}

				}

			}
		}

	}

	private BookPage[] GetChapter(int chapter) => chapters[chapter].pages;
	private BookPage GetPage(int chapter, int page) => chapters[chapter].pages[page];

	private void LoadChapter(BookPage[] chapter)
	{
		chapterLabel.Text = Tr("spb_chapter") + " " + (chapterSelection + 1);

		chapterName.Text = "[" + Tr("spb_chapter_" + tabs[chapterSelection].thisChapterType.ToString().ToLower()) + "]";

		for (int i = 0; i < chapter.Length; i++)
		{
			if (chapter != null && chapter[i].unlocked)
				previewImages[i].Texture = (Texture2D)chapter[i].page_preview;
			else previewImages[i].Texture = null;
		}
	}

	private void LoadPage(BookPage page)
	{
		menuMemory[MemoryKeys.SpecialBook] = (15 * chapterSelection) + (pageSelection);
		if (page.page_full != null)
		{
			navZoom.Visible = true;
			navPlay.Visible = false;
		}
		else if (page.videoFilePath != "" || page.track != null)
		{
			navZoom.Visible = false;
			navPlay.Visible = true;
		}

		if (page.unlocked)
		{
			textboxTitle.Text = chapterName.Text + "\n" + Tr(page.name);
			previewDescription.Text = Tr(page.name.Replace("title", "desc"));
			previewImage.Texture = (Texture2D)page.page_preview_big;
			previewNumber.Text = "-" + ((15 * chapterSelection) + pageSelection + 1).ToString("D3") + "-";
			fullImage.Texture = (Texture2D)page.page_full;
		}
		else
		{
			textboxTitle.Text = LoadHint(page);
		}

	}

	private int GetChapterFromMemory()
	{
		int goal = menuMemory[MemoryKeys.SpecialBook];
		int chapter = 0;
		int page = 0;
		for (int i = 0; i < chapters.Length; i++)
		{
			for (int j = 0; j < pages.Length; j++)
			{
				page++;

				if (page == goal)
					return chapter;
			}
			chapter++;

		}
		return chapter;
	}

	private int GetPageFromMemory()
	{
		int goal = menuMemory[MemoryKeys.SpecialBook];
		int page = 0;

		for (int i = 0; i < 15; i++)
		{
			for (int j = 0; j < pages.Length; j++)
			{
				page++;

				if (page == goal)
					return j;
			}
		}

		return 0;
	}

	private string LoadHint(BookPage page)
	{
		if (page.unlock_silver)
			return Tr("spb_hint_silvermedal").Replace("XX", page.unlock_numSilver.ToString());

		if (page.unlock_clear)
			return Tr("spb_hint_complete_" + page.unlock_world.ToString().ToLower()).Replace("XX", page.unlock_stageNum.ToString());

		if (page.unlock_gold)
			return Tr("spb_hint_goldmedal_" + page.unlock_world.ToString().ToLower()).Replace("XX", page.unlock_stageNum.ToString());

		if (page.unlock_allstage)
			return Tr("spb_hint_allmission_" + page.unlock_world.ToString().ToLower());

		return "???";
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
				if (bookPage.unlocked == true && IsValid(bookPage, MenuFocus.Image))
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
				GetPage(chapter, page).unlocked = true;
		}
	}

	private void LoadChapter1() => LoadChapter(chapters[chapterSelection].pages);
}
