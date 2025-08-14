using Godot;

namespace Project.Interface.Menus;

public partial class SpecialBookTab : Control
{
	public enum ChapterType
	{
		History,
		Illust,
		Movie,
		Dev,
		Music,
		Rank,
		Diary
	};

	[ExportGroup("Tab Settings")]
	[Export] public ChapterType chapterType;
	[Export] private Texture2D[] unselectedTextures;
	[Export] private Texture2D[] selectedTextures;
	[Export] private TextureRect tabTextureRect;
	[Export] private AnimationPlayer animator;

	[ExportGroup("Page Settings")]
	/// <summary> The thumbnail used to preview this chapter's pages. </summary>
	[Export] public Texture2D PageThumbnail { get; private set; }
	/// <summary> An array of all the pages associated with this chapter. </summary>
	[Export] public OldBookPage[] PageResources { get; private set; }
	/// <summary> Path containing all this chapter's textures. </summary>
	[Export(PropertyHint.Dir)] private string pageTexturePath;

	public void ChangeTabImage(bool isSelected)
	{
		tabTextureRect.Texture = isSelected ? selectedTextures[(int)chapterType] : unselectedTextures[(int)chapterType];
	}

	public void Select() => animator.Play("select");

	public void SelectNoGlow() => animator.Play("select_noglow");

	public void SelectNoSFX() => animator.Play("select_nosfx");

	public void SelectNoMove() => animator.Play("select_nomove");

	public void Deselect() => animator.Play("deselect");

	public void DeselectNoGlow() => animator.Play("hide_glow");

}
