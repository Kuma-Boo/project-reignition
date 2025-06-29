using Godot;

namespace Project.Interface.Menus;

[GlobalClass]
public partial class BookChapter : Resource
{
	/// <summary> The image used to preview this chapter's pages. </summary>
	[Export] public Texture2D pagePreview;

	/// <summary> An array of all the book page resources. </summary>
	[Export] public BookPage[] pages;
}
