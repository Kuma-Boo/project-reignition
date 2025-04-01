using Godot;
using System;
using Project.Core;

namespace Project.Interface.Menus;

public partial class SpecialBookWindow : Control
{

    [Export]
    private AnimationPlayer windowAnimator;

    public void Select()
    {
        windowAnimator.Play("select");
    }

    public void Deselect()
    {
        windowAnimator.Play("RESET");
    }

}
