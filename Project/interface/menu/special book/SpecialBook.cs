using Godot;
using Godot.Collections;
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
    private BookPage[] chapter1;

    [Export]
    private Label chapterName;

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

    }

    protected override void UpdateSelection()
    {

        Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

        if (input == Vector2.Zero)
            return;

        StartSelectionTimer();


        if (menuFocus == 0)
        {


            if (Input.IsActionJustPressed("button_step_left"))//press left bumper
            {
                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection - 1, 16);
                tabs[chapterSelection].Select();
                return;
            }
            if (Input.IsActionJustPressed("button_step_right"))//press left bumper
            {
                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection + 1, 16);
                tabs[chapterSelection].Select();
                return;
            }



            if (input.X != 0)//move left or right
            {
                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection + (int)input.X, 16);
                tabs[chapterSelection].Select();
                return;
            }
            if (input.Y > 0)//move down
            {
                tabs[chapterSelection].Deselect_NoGlow();
                menuFocus = 1;
                pageSelection = 0;
                windows[pageSelection].Select();
                return;
            }
        }
        else if (menuFocus == 1)
        {
            if (Input.IsActionJustPressed("button_step_left"))//press left bumper
            {
                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection - 1, 16);
                tabs[chapterSelection].Select_NoGlow();
                return;
            }
            if (Input.IsActionJustPressed("button_step_right"))//press left bumper
            {
                tabs[chapterSelection].Deselect();
                chapterSelection = WrapSelection(chapterSelection + 1, 16);
                tabs[chapterSelection].Select_NoGlow();
                return;
            }

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
                return;
            }
            if (input.Y != 0)
            {
                windows[pageSelection].Deselect();
                if ((int)input.Y < 0 && pageSelection <= 4)//If we are going up on the first row
                {
                    tabs[chapterSelection].Select();
                    menuFocus = 0;
                    return;
                }

                pageSelection = WrapSelection(pageSelection + (5 * (int)input.Y), 14, pageSelection - 10);
                windows[pageSelection].Select();
                return;

            }

        }
        base.UpdateSelection();
    }
    public override void ShowMenu()
    {

        tabs[0].Select_NoSFX();
        chapterSelection = 0;
        pageSelection = 0;
        menuFocus = 0;

        base.ShowMenu();
    }

    private void LoadChapter(BookPage[] chapter)
    {
        GD.Print("Loading chapter...");
        for (int i = 0; i < 15; i++)
        {
            //if (chapter[i].unlocked)
            previewImages[i].Texture = (Texture2D)chapter[i].page_preview;
        }
    }

    private BookPage LoadPage(int chapter, int page)
    {
        switch (chapter)//I tried using a dictionary here, but it wasn't working. 
        {
            case 1:
                return chapter1[page];

        }
        return chapter1[0];
    }

    private void LoadChapter1()
    {
        LoadChapter(chapter1);
    }


}
