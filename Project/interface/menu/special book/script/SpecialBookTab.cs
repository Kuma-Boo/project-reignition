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

	[Export] public ChapterType chapterType;
	[Export] private TextureRect tabTextureRect;
	[Export] private Texture2D[] tabs;
	[Export] private Texture2D[] tabsSelected;
	[Export] private AnimationPlayer tabAnimator;

	public void ChangeTabImage(bool isSelected)
	{
		tabTextureRect.Texture = isSelected ? tabsSelected[(int)chapterType] : tabs[(int)chapterType];
	}

	public void Select() => tabAnimator.Play("select");

	public void SelectNoGlow() => tabAnimator.Play("select_noglow");

	public void SelectNoSFX() => tabAnimator.Play("select_nosfx");

	public void SelectNoMove() => tabAnimator.Play("select_nomove");

	public void Deselect() => tabAnimator.Play("deselect");

	public void DeselectNoGlow() => tabAnimator.Play("hide_glow");

}
