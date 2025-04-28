using Godot;
using Godot.Collections;
using System.Collections.Generic;
using Project.Core;
using System;

namespace Project.Interface.Menus;

public partial class SpecialBook : Menu
{
    private int chapterSelection;
    private int pageSelection;

    ///<Summary>
    ///0 = chapter focus | 
    ///1 = page focus | 
    ///2 = description focus | 
    ///3 = image focus | 
    ///</Summary>
    private int menuFocus;

    [Export]
    private SpecialBookTab[] tabs;

    [Export]
    private SpecialBookWindow[] windows;
    [Export]
    private Sprite2D[] previewImages;

    [Export]
    private BookChapter[] chapters;

    [Export]
    private Label chapterName;

    [Export]
    private Label chapterLabel;

    [Export]
    private Label textboxTitle;

    [Export]
    private Label previewDescription;

    [Export]
    private TextureRect previewImage;

    [Export]
    private Label previewNumber;

    [Export]
    private TextureRect fullImage;

    [Export]
    private AnimationPlayer animatorNav;
    [Export]
    private AnimationPlayer animatorRandom;

    [Export]
    private NavigationButton navZoom;
    [Export]
    private NavigationButton navPlay;
    [Export]
    private BGMPlayer player;


    private Godot.Collections.Array randomPages;

    private bool playRandom;
    private int seekRandom;




    protected override void SetUp()
    {
        chapterSelection = 0;
        pageSelection = 0;
        menuFocus = 0;
        playRandom = false;
        seekRandom = 0;


        GD.Print("Length: " + tabs.Length);
        for (int i = 0; i < tabs.Length; i++)
        {
            tabs[i].ChangeTab();

        }

        for (int i = 0; i < windows.Length; i++)
        {
            windows[i].Glow();
        }



        //UnlockAll();
    }

    protected override void ProcessMenu()
    {

        if (menuFocus == 0 || menuFocus == 1)//Change chapter by using the bumpers
        {
            if (Input.IsActionJustPressed("button_step_left"))
            {
                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection - 1, 16);

                if (menuFocus == 0)
                    tabs[chapterSelection].Select();
                else if (menuFocus == 1)
                    tabs[chapterSelection].Select_NoGlow();

                LoadChapter(chapters[chapterSelection].pages);
                LoadPage(GetPage(chapterSelection, pageSelection));


            }

            if (Input.IsActionJustPressed("button_step_right"))
            {
                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection + 1, 16);

                if (menuFocus == 0)
                    tabs[chapterSelection].Select();
                else if (menuFocus == 1)
                    tabs[chapterSelection].Select_NoGlow();

                LoadChapter(chapters[chapterSelection].pages);
                LoadPage(GetPage(chapterSelection, pageSelection));
            }

            if (Input.IsActionJustPressed("button_pause"))
            {
                if (CheckNumUnlocked() > 1) //If we have more than one page unlocked, then play the slideshow
                {
                    RandomizeList();
                    playRandom = true;
                    seekRandom = 0;
                    menuFocus = 3;
                    randomPages.Shuffle();
                    animator.Play("show_playrandom");
                    animatorRandom.Play("playrandom");
                    animatorRandom.Seek(0.0);
                }


            }
        }


        base.ProcessMenu();
    }

    protected override void UpdateSelection()
    {

        //BUG: Only dpad seems to work, stick doesn't
        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");



        if (input == Vector2.Zero)
            return;
        else
        {
            if (menuFocus == 3 && !playRandom)
            {
                animatorNav.Play("hide_navbuttons");//Hides the nav buttons after 5 seconds of inactivity
                animatorNav.Seek(0.0);
            }

        }

        StartSelectionTimer();

        MenuControls(input);

    }

    protected override void Confirm()
    {
        BookPage thisPage = GetPage(chapterSelection, pageSelection);

        if (menuFocus == 0)
        {
            MenuControls(new Vector2(0, 1));
            return;
        }
        if (menuFocus == 1)
        {
            if (GetPage(chapterSelection, pageSelection).unlocked)
            {
                animator.Play("show_description");
                menuFocus = 2;

                return;
            }

        }
        if (menuFocus == 2)
        {
            if (GetPage(chapterSelection, pageSelection).unlocked)
            {
                if (thisPage.page_full != null) //if this is an image
                {
                    animator.Play("show_fullimage");
                    menuFocus = 3;
                    animatorNav.Play("hide_navbuttons");
                    animatorNav.Seek(0.0);
                    StartSelectionTimer();
                }

                if (thisPage.videoFilePath != "")
                {
                    bgm.Stop();
                    menuFocus = 4;

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
                    player.Play();
                }

            }
        }
    }

    protected override void Cancel()
    {
        if (menuFocus == 0)
        {
            tabs[chapterSelection].Deselect();
            animator.Play("hide");
        }

        if (menuFocus == 1)
        {
            tabs[chapterSelection].Select_NoMove();
            windows[pageSelection].Deselect();
            menuFocus = 0;
        }

        if (menuFocus == 2)
        {
            animator.Play("hide_description");
            menuFocus = 1;
        }

        if (menuFocus == 3)
        {
            if (!playRandom)
            {
                animator.Play("hide_fullimage");
                menuFocus = 2;
            }
            else
            {
                animator.Play("hide_playrandom");
                animatorRandom.Stop();
                menuFocus = 1;
                playRandom = false;
                tabs[chapterSelection].Deselect_NoGlow();
                windows[pageSelection].Select();
            }

        }

    }


    public override void ShowMenu()
    {
        menuMemory[MemoryKeys.ActiveMenu] = (int)MemoryKeys.SpecialBook;

        tabs[0].Select_NoSFX();
        chapterSelection = 0;
        pageSelection = 0;
        menuFocus = 0;
        playRandom = false;

        for (int chapter = 0; chapter < chapters.Length; chapter++)
        {
            for (int page = 0; page < 15; page++)
            {
                GetPage(chapter, page).unlocked = false;
            }
        }

        LoadSaveData();

        base.ShowMenu();
    }

    private void MenuControls(Vector2 input)
    {
        if (menuFocus == 0)
        {
            if (input.X != 0)//move left or right
            {

                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection + (int)input.X, 16);
                tabs[chapterSelection].Select();

                LoadChapter(chapters[chapterSelection].pages);
                return;
            }

            if (input.Y > 0)//move down
            {
                tabs[chapterSelection].Deselect_NoGlow();
                menuFocus = 1;
                pageSelection = 0;
                windows[pageSelection].Select();
                chapterName.Visible = false;
                textboxTitle.Visible = true;

                LoadPage(GetPage(chapterSelection, pageSelection));
                return;
            }
        }
        else if (menuFocus == 1)
        {

            if (input.X != 0)//If we are going left or right
            {
                windows[pageSelection].Deselect();

                if (pageSelection <= 4)//row 1
                    pageSelection = WrapSelection(pageSelection + (int)input.X, 4, 0);
                else if (pageSelection >= 5 && pageSelection <= 9)//row 2
                    pageSelection = WrapSelection(pageSelection + (int)input.X, 9, 5);
                else if (pageSelection >= 10 && pageSelection <= 14)//row 3
                    pageSelection = WrapSelection(pageSelection + (int)input.X, 14, 10);

                windows[pageSelection].Select();
                LoadPage(GetPage(chapterSelection, pageSelection));
                return;
            }
            if (input.Y != 0)
            {
                windows[pageSelection].Deselect();

                if ((int)input.Y < 0 && pageSelection <= 4)//If we are going up on the first row
                {
                    tabs[chapterSelection].Select_NoMove();
                    menuFocus = 0;
                    chapterName.Visible = true;
                    textboxTitle.Visible = false;
                    return;
                }

                pageSelection = WrapSelection(pageSelection + (5 * (int)input.Y), 14, pageSelection - 10); //Wraps the selection vertically
                windows[pageSelection].Select();
                LoadPage(GetPage(chapterSelection, pageSelection));
                return;

            }

        }
        else if (menuFocus == 2 || menuFocus == 3)
        {
            if (input.X != 0 && !playRandom)
            {
                windows[pageSelection].Deselect();

                int ogChapter = chapterSelection;
                do
                {
                    pageSelection += (int)input.X;
                    if (pageSelection > 14 || pageSelection < 0)
                    {
                        pageSelection = WrapSelection(pageSelection, 15);
                        chapterSelection = WrapSelection(chapterSelection + (int)input.X, 16);
                    }

                } while (!IsValid(GetPage(chapterSelection, pageSelection), menuFocus));//Skips over every page we don't have unlocked. If we're on the full view, skips over movies and music too

                if (ogChapter != chapterSelection)
                {
                    tabs[ogChapter].Deselect();
                    tabs[chapterSelection].Select_NoGlow();
                }


                windows[pageSelection].Select();
                LoadChapter(GetChapter(chapterSelection));
                LoadPage(GetPage(chapterSelection, pageSelection));
            }
        }
    }

    public void PlayRandomPage()
    {

        do
        {
            seekRandom = WrapSelection(seekRandom + 1, randomPages.Count);
        } while (!IsValid((BookPage)randomPages[seekRandom], menuFocus));

        LoadPage((BookPage)randomPages[seekRandom]);

    }

    private void RandomizeList()
    {
        randomPages = new Godot.Collections.Array();
        for (int i = 0; i < 16; i++)
        {
            for (int j = 0; j < 15; j++)
            {
                randomPages.Add(GetPage(i, j));
            }
        }
    }

    private bool IsValid(BookPage page, int focus)//Checks if we can view the page
    {
        if (focus == 1 || focus == 2)
        {
            if (page.unlocked)
                return true;
        }
        else if (focus == 3)
        {
            if (page.unlocked && page.page_full != null) //If this is an image, then we can view it
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
                        if (data.GetRank((StringName)page.StageUnlock()) > 0) //If we have at least a bronze medal
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
                        if (data.GetRank((StringName)page.StageUnlock()) > 2) //If we have a gold medal
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
                        for (int level = 1; level < 30; level++)
                        {
                            foreach (SaveManager.GameData data in SaveManager.GameSaveSlots)
                            {
                                if (page.StageUnlock((BookPage.World)world, level) != "")
                                {
                                    if (data.GetRank((StringName)page.StageUnlock((BookPage.World)world, level)) > 1)
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

            }
        }

    }

    private BookPage[] GetChapter(int chapter)
    {
        return chapters[chapter].pages;
    }
    private BookPage GetPage(int chapter, int page)
    {
        return chapters[chapter].pages[page];
    }

    private void LoadChapter(BookPage[] chapter)
    {
        chapterLabel.Text = Tr("spb_chapter") + " " + (chapterSelection + 1);

        chapterName.Text = "[" + Tr("spb_chapter_" + tabs[chapterSelection].thisChapterType.ToString().ToLower()) + "]";

        for (int i = 0; i < 15; i++)
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
            previewNumber.Text = "-" + ((15 * chapterSelection) + (pageSelection + 1)).ToString("D3") + "-";
            fullImage.Texture = (Texture2D)page.page_full;
        }
        else
        {
            textboxTitle.Text = LoadHint(page);
        }

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


    private int CheckNumUnlocked()
    {
        int num = 0;

        for (int chapter = 0; chapter < chapters.Length; chapter++)
        {
            for (int page = 0; page < 15; page++)
            {
                if (chapters[chapter].pages[page].unlocked == true && IsValid(chapters[chapter].pages[page], 3))
                    num++;
            }
        }
        return num;
    }
    private void UnlockAll()
    {
        for (int i = 0; i < chapters.Length; i++)
        {
            for (int page = 0; page < 15; page++)
            {
                chapters[i].pages[page].unlocked = true;
            }
        }
    }

    private void LoadChapter1()
    {
        LoadChapter(chapters[0].pages);
    }
}
