using Godot;
using System.Collections.Generic;

namespace Project.Interface.Menus;

public partial class SpecialBookTab : Control
{
	[Signal] public delegate void PreviewTextureLoadedEventHandler(int tabIndex, int pageIndex);
	[Signal] public delegate void FullTextureLoadedEventHandler(int tabIndex, int pageIndex);

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
	[Export] private Texture2D musicPreviewTexture;
	[Export] private Texture2D[] achievementTextures;
	[Export] private Texture2D[] unselectedTextures;
	[Export] private Texture2D[] selectedTextures;
	[Export] private TextureRect tabTextureRect;
	[Export] private AnimationPlayer animator;

	[ExportGroup("Page Settings")]
	/// <summary> The thumbnail used to preview this chapter's pages. </summary>
	[Export] public Texture2D PageThumbnail { get; private set; }
	/// <summary> An array of all the pages associated with this chapter. </summary>
	[Export] public SpecialBookPage[] PageResources { get; private set; }
	/// <summary> Path containing all this chapter's textures. </summary>
	[Export(PropertyHint.Dir)] private string pageTexturePath;

	private int asyncLoadIndex = -1;
	private string currentAsyncFilePath;
	private bool isLoadingFullImages;
	private Texture2D[] previewTextures;
	private Texture2D[] fullTextures;

	public void Initialize()
	{
		previewTextures = new Texture2D[PageResources.Length];
		fullTextures = new Texture2D[PageResources.Length];

		if (string.IsNullOrEmpty(pageTexturePath))
			return;

		IncrementLoadIndex();
	}

	public override void _Process(double _)
	{
		if (asyncLoadIndex == -1)
			return;

		if (ResourceLoader.LoadThreadedGetStatus(currentAsyncFilePath) == ResourceLoader.ThreadLoadStatus.InProgress)
			return;

		Resource texture = ResourceLoader.LoadThreadedGet(currentAsyncFilePath);
		if (isLoadingFullImages)
		{
			fullTextures[asyncLoadIndex] = texture as Texture2D;
			EmitSignal(SignalName.FullTextureLoaded, GetIndex(), asyncLoadIndex);
		}
		else
		{
			previewTextures[asyncLoadIndex] = texture as Texture2D;
			EmitSignal(SignalName.PreviewTextureLoaded, GetIndex(), asyncLoadIndex);
		}

		IncrementLoadIndex();
	}

	private void IncrementLoadIndex()
	{
		asyncLoadIndex++;
		if (asyncLoadIndex >= PageResources.Length)
		{
			if (isLoadingFullImages) // Finished loading textures
			{
				asyncLoadIndex = -1;
				return;
			}

			asyncLoadIndex = 0; // Start loading full images
			isLoadingFullImages = true;
		}

		if (PageResources[asyncLoadIndex].PageType == SpecialBookPage.PageTypeEnum.Music ||
			PageResources[asyncLoadIndex].PageType == SpecialBookPage.PageTypeEnum.Achievement) // Nothing to load for music or achievements
		{
			IncrementLoadIndex();
			return;
		}

		if (isLoadingFullImages &&
			PageResources[asyncLoadIndex].PageType == SpecialBookPage.PageTypeEnum.Video)
		{
			// Videos don't have full images
			IncrementLoadIndex();
			return;
		}

		currentAsyncFilePath = pageTexturePath + $"/{(asyncLoadIndex + 1).ToString("00")}";
		currentAsyncFilePath += isLoadingFullImages ? ".png" : "P.png";

		if (ResourceLoader.Exists(currentAsyncFilePath))
		{
			Error error = ResourceLoader.LoadThreadedRequest(currentAsyncFilePath, "Texture2D");

			if (error != Error.Ok)
				IncrementLoadIndex();
		}
		else
		{
			GD.PushWarning($"Could not find {currentAsyncFilePath}.");
		}
	}

	public void ChangeTabImage(bool isSelected) =>
		tabTextureRect.Texture = isSelected ? selectedTextures[(int)chapterType] : unselectedTextures[(int)chapterType];

	public void Select() => animator.Play("select");

	public void SelectNoGlow() => animator.Play("select_noglow");

	public void SelectNoSFX() => animator.Play("select_nosfx");

	public void SelectNoMove() => animator.Play("select_nomove");

	public void Deselect() => animator.Play("deselect");

	public void HideGlow() => animator.Play("hide_glow");

	public Texture2D GetPreviewTexture(int pageIndex)
	{
		if (PageResources[pageIndex].PageType == SpecialBookPage.PageTypeEnum.Music)
			return musicPreviewTexture;

		if (PageResources[pageIndex].PageType == SpecialBookPage.PageTypeEnum.Achievement)
			return achievementTextures[PageResources[pageIndex].AchievementType - 1];

		return previewTextures[pageIndex];
	}

	public Texture2D GetFullTexture(int pageIndex) => fullTextures[pageIndex];
}
