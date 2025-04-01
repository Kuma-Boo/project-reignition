using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SpecialBook : Menu
{

    private int chapterSelection;
    private int pageSelection;

    ///<Summary>
    ///0 = chapter focus
    ///1 = page focus
    ///2 = description focus
    ///3 = image focus
    ///</Summary
    private int menuFocus;
    [Export]
    private string[] chapters;

    [Export]
    private SpecialBookTab[] tabs;

    protected override void SetUp()
    {
        chapterSelection = 0;
        pageSelection = 0;

        for (int i = 0; i < 16; i++)
        {
            tabs[i].ChangeTab();
            GD.Print("Changing tab type " + i);
        }

    }

    protected override void ProcessMenu()
    {

    }
}
