using Godot;
using System;


namespace Project.Interface.Menus;
[GlobalClass]
public partial class BookChapter : Resource
{

    [Export]
    SpecialBookTab.ChapterType type;
    [Export]
    public BookPage[] pages;

    public BookChapter() : this(SpecialBookTab.ChapterType.DEV, null) { }

    public BookChapter(SpecialBookTab.ChapterType type, BookPage[] pages)
    {
        this.type = type;
        this.pages = pages;
    }
}
