using Godot;
using Godot.Collections;
using System.Collections.Generic;
using Project.Core;

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
    private Label description;





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

        UnlockAll();
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
            }
        }
        base.ProcessMenu();
    }

    protected override void UpdateSelection()
    {

        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

        if (input == Vector2.Zero)
            return;

        StartSelectionTimer();


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
                    tabs[chapterSelection].Select();
                    menuFocus = 0;
                    chapterName.Visible = true;
                    textboxTitle.Visible = false;
                    return;
                }

                pageSelection = WrapSelection(pageSelection + (5 * (int)input.Y), 14, pageSelection - 10);
                windows[pageSelection].Select();
                return;

            }

        }
        //base.UpdateSelection();
    }
    public override void ShowMenu()
    {

        tabs[0].Select_NoSFX();
        chapterSelection = 0;
        pageSelection = 0;
        menuFocus = 0;

        base.ShowMenu();
    }

    private BookPage GetPage(int chapter, int page)
    {
        return chapters[chapter].pages[page];
    }

    private void LoadChapter(BookPage[] chapter)
    {
        chapterLabel.Text = "spb_chapter " + (chapterSelection + 1);
        GD.Print(chapterLabel.Text);
        if (menuFocus == 0)
            chapterName.Text = "[" + Tr("spb_chapter_" + tabs[chapterSelection].thisChapterType.ToString().ToLower()) + "]";

        for (int i = 0; i < 15; i++)
        {
            if (chapter != null && chapter[i].unlocked)
                previewImages[i].Texture = (Texture2D)chapter[i].page_preview;
        }
    }

    private void LoadPage(BookPage page)
    {

        if (page.unlocked)
        {
            GD.Print(page.ResourceName.Replace("page", "title"));
            textboxTitle.Text = Tr(page.ResourceName.Replace("page", "title"));
            //TODO:
            //load page preview
            //load full page
            //load description
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
        {
            switch (page.unlock_world)
            {
                case BookPage.World.LP:
                    return Tr("spb_hint_complete_lp").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.SO:
                    return Tr("spb_hint_complete_so").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.DJ:
                    return Tr("spb_hint_complete_dj").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.EF:
                    return Tr("spb_hint_complete_ef").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.LR:
                    return Tr("spb_hint_complete_lr").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.PS:
                    return Tr("spb_hint_complete_ps").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.SD:
                    return Tr("spb_hint_complete_sd").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.NP:
                    return Tr("spb_hint_complete_np").Replace("XX", page.unlock_stageNum.ToString());
            }
        }

        if (page.unlock_gold)
        {
            switch (page.unlock_world)
            {
                case BookPage.World.LP:
                    return Tr("spb_hint_goldmedal_lp").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.SO:
                    return Tr("spb_hint_goldmedal_so").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.DJ:
                    return Tr("spb_hint_goldmedal_dj").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.EF:
                    return Tr("spb_hint_goldmedal_ef").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.LR:
                    return Tr("spb_hint_goldmedal_lr").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.PS:
                    return Tr("spb_hint_goldmedal_ps").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.SD:
                    return Tr("spb_hint_goldmedal_sd").Replace("XX", page.unlock_stageNum.ToString());
                case BookPage.World.NP:
                    return Tr("spb_hint_goldmedal_np").Replace("XX", page.unlock_stageNum.ToString());
            }
        }

        if (page.unlock_allstage)
        {
            switch (page.unlock_world)
            {
                case BookPage.World.LP:
                    return Tr("spb_hint_allmission_lp");
                case BookPage.World.SO:
                    return Tr("spb_hint_allmission_so");
                case BookPage.World.DJ:
                    return Tr("spb_hint_allmission_dj");
                case BookPage.World.EF:
                    return Tr("spb_hint_allmission_ef");
                case BookPage.World.LR:
                    return Tr("spb_hint_allmission_lr");
                case BookPage.World.PS:
                    return Tr("spb_hint_allmission_ps");
                case BookPage.World.SD:
                    return Tr("spb_hint_allmission_sd");
                case BookPage.World.NP:
                    return Tr("spb_hint_allmission_np");
            }
        }

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
