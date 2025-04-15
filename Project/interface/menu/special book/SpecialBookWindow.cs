using Godot;
using System;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SpecialBookWindow : Control
{

    [Export]
    private AnimationPlayer windowAnimator;
    [Export]
    private AnimationPlayer windowAnimator2;

    public void Glow()
    {
        windowAnimator2.Play("glow");
    }

    public void Select()
    {
        windowAnimator.Play("select");
    }

    public void Deselect()
    {
        windowAnimator.Play("RESET");
    }

}
