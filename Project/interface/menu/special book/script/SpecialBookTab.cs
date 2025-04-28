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

	[Export] public ChapterType thisChapterType;
	[Export] private TextureRect thisTab;
	[Export] private Texture2D[] tabs;
	[Export] private Texture2D[] tabsSelected;
	[Export] private AnimationPlayer tabAnimator;

	public void ChangeTabImage(bool isSelected)
	{
		thisTab.Texture = isSelected ? tabsSelected[(int)thisChapterType] : tabs[(int)thisChapterType];
	}

	public void ChangeTab() => tabAnimator.Play("show_tab");

	public void Select() => tabAnimator.Play("select");

	public void SelectNoGlow() => tabAnimator.Play("select_noglow");

	public void SelectNoSFX() => tabAnimator.Play("select_nosfx");

	public void SelectNoMove() => tabAnimator.Play("select_nomove");

	public void SelectGlow() => tabAnimator.Play("show_glow");

	public void Deselect() => tabAnimator.Play("deselect");

	public void DeselectNoGlow() => tabAnimator.Play("hide_glow");

}
