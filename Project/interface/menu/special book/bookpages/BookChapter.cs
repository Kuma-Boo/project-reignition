using Godot;
using System;

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
