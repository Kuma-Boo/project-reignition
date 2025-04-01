using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SpecialBookTab : Control
{
    public enum TabType
    { HISTORY, ILLUST, MOVIE, DEV, MUSIC, RANK, DIARY };

    [Export]
    public TabType thisTabType;


    [Export]
    private AnimationPlayer tabAnimator;

    public void ChangeTab()
    {
        tabAnimator.Play("show_" + thisTabType.ToString().ToLower());
    }

    public void Select()
    {
        tabAnimator.Play("select");
    }

    public void Deselect()
    {
        tabAnimator.Play("deselect");
    }

}
