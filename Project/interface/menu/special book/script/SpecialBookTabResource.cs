using Godot;
using Godot.Collections;
using Project.Core;
using Project.Gameplay;

namespace Project.Interface.Menus;

public partial class SpecialBookTabResource : Resource
{
	[Export] public SpecialBookTab.ChapterType chapterType;

	[Export] public Texture2D PageThumbnail { get; private set; }

	[Export] public SpecialBookPage[] PageResources { get; private set; }

	[Export(PropertyHint.Dir)] public string PageTexturePath { get; private set; }
}
