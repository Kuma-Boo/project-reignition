using Godot;
using System;


namespace Project.Interface.Menus;
[GlobalClass]
public partial class BookChapter : Resource
{

    [Export]
    public BookPage[] pages;

    public BookChapter() : this(null) { }

    public BookChapter(BookPage[] pages)
    {
        this.pages = pages;
    }
}
