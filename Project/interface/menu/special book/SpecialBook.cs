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




    protected override void SetUp()
    {
        chapterSelection = 0;
        pageSelection = 0;
        menuFocus = 0;


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
        }


        base.ProcessMenu();
    }

    protected override void UpdateSelection()
    {

        //BUG: Only dpad seems to work, stick doesn't
        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

        if (input == Vector2.Zero)
            return;

        StartSelectionTimer();

        MenuControls(input);

    }

    protected override void Confirm()
    {
        BookPage thisPage = GetPage(chapterSelection, pageSelection);

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
                if (thisPage.track == null && thisPage.videoFilePath == "") //if this is an image
                {
                    animator.Play("show_fullimage");
                    menuFocus = 3;
                    StartSelectionTimer();
                }


                //if (thisPage.track != null) //if this is audio
                //TODO: play audio track

                //if (thisPage.videoFilePath != null) //if this is video
                //TODO: play video file
            }
        }
    }

    protected override void Cancel()
    {
        if (menuFocus == 0)
        {
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
            animator.Play("hide_fullimage");
            menuFocus = 2;
        }
    }


    public override void ShowMenu()
    {

        tabs[0].Select_NoSFX();
        chapterSelection = 0;
        pageSelection = 0;
        menuFocus = 0;

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

                //GD.Print(chapterSelection);
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

                pageSelection = WrapSelection(pageSelection + (5 * (int)input.Y), 14, pageSelection - 10);
                windows[pageSelection].Select();
                LoadPage(GetPage(chapterSelection, pageSelection));
                return;

            }

        }
        else if (menuFocus == 2 || menuFocus == 3)
        {
            if (input.X != 0)
            {
                windows[pageSelection].Deselect();


                pageSelection += (int)input.X;

                //do
                //{
                if (pageSelection > 14 || pageSelection < 0)
                {
                    tabs[chapterSelection].Deselect();

                    pageSelection = WrapSelection(pageSelection, 15);
                    chapterSelection = WrapSelection(chapterSelection + (int)input.X, 16);

                    tabs[chapterSelection].Select_NoGlow();
                }
                //} while (!IsValid(GetPage(chapterSelection, pageSelection)));//Skips over every page we don't have unlocked. If we're on the full view, skips over movies and music


                windows[pageSelection].Select();
                LoadChapter(GetChapter(chapterSelection));
                LoadPage(GetPage(chapterSelection, pageSelection));
            }
        }
    }


    private bool IsValid(BookPage page)//Checks if we can view the page
    {
        if (menuFocus == 1 || menuFocus == 2)
        {
            if (page.unlocked)
                return true;
        }
        else if (menuFocus == 3)
        {
            if (page.unlocked || page.videoFilePath != null || page.track != null)
                return true;
        }
        return false;
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
        chapterLabel.Text = "spb_chapter " + (chapterSelection + 1);
        GD.Print(chapterLabel.Text);

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
