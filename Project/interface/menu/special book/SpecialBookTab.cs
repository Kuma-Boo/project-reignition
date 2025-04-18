using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SpecialBookTab : Control
{
    public enum ChapterType
    { HISTORY, ILLUST, MOVIE, DEV, MUSIC, RANK, DIARY };

    [Export]
    public ChapterType thisChapterType;

    [Export]
    private TextureRect thisTab;

    [Export]
    private Texture2D[] tabs;

    [Export]
    private Texture2D[] tabsSelected;


    [Export]
    private AnimationPlayer tabAnimator;

    public void ChangeTabImage(bool select)
    {
        if (!select)
            thisTab.Texture = tabs[(int)thisChapterType];
        else
            thisTab.Texture = tabsSelected[(int)thisChapterType];
    }

    public void ChangeTab() => tabAnimator.Play("show_tab");

    public void Select() => tabAnimator.Play("select");

    public void Select_NoGlow() => tabAnimator.Play("select_noglow");

    public void Select_NoSFX() => tabAnimator.Play("select_nosfx");

    public void Select_NoMove() => tabAnimator.Play("select_nomove");

    public void Select_Glow() => tabAnimator.Play("show_glow");

    public void Deselect() => tabAnimator.Play("deselect");

    public void Deselect_NoGlow() => tabAnimator.Play("hide_glow");

}
