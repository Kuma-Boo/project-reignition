using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class WorldSelect : Menu
	{
		[Export]
		private AnimationPlayer animator;

		[ExportSubgroup("Demo Video Settings")]
		[Export]
		private VideoStreamPlayer videoPlayer;
		[Export]
		private TextureRect videoCrossfade;
		[Export]
		private Array<VideoStreamTheora> videoStreams = new Array<VideoStreamTheora>();
		private bool isChangingVideo;

		private Color crossfadeColor;
		private float videoFadeFactor;
		private const float VIDEO_FADE_SPEED = 2.0f;

		[ExportSubgroup("Selection Settings")]
		[Export]
		private LevelDescription description;
		[Export]
		private Array<Rect2> levelSpriteRegions = new Array<Rect2>();
		[Export]
		private Array<string> levelDescriptionKeys = new Array<string>();
		[Export]
		private Array<NodePath> levelTextSprites = new Array<NodePath>();
		[Export]
		private Array<NodePath> levelGlowSprites = new Array<NodePath>();
		private readonly Array<Sprite2D> _levelTextSprites = new Array<Sprite2D>();
		private readonly Array<Sprite2D> _levelGlowSprites = new Array<Sprite2D>();

		protected override void SetUp()
		{
			for (int i = 0; i < levelTextSprites.Count; i++)
				_levelTextSprites.Add(GetNode<Sprite2D>(levelTextSprites[i]));

			for (int i = 0; i < levelGlowSprites.Count; i++)
				_levelGlowSprites.Add(GetNode<Sprite2D>(levelGlowSprites[i]));

			VerticalSelection = menuMemory[MenuKeys.WorldSelect];
		}

		public override void _PhysicsProcess(double _)
		{
			base._PhysicsProcess(_);

			if (IsVisibleInTree())
			{
				UpdateVideo();
				if (videoPlayer.Stream != null)
				{
					if (!videoPlayer.IsPlaying())
						videoPlayer.CallDeferred(VideoStreamPlayer.MethodName.Play);
					else
						videoFadeFactor = Mathf.MoveToward(videoFadeFactor, 1, VIDEO_FADE_SPEED * PhysicsManager.physicsDelta);

					if (isChangingVideo)
						isChangingVideo = false;
				}

				videoPlayer.Modulate = Colors.Transparent.Lerp(Colors.White, videoFadeFactor);
				videoCrossfade.Modulate = crossfadeColor.Lerp(Colors.Transparent, videoFadeFactor);
			}
		}

		protected override void UpdateSelection()
		{
			if (Controller.verticalAxis.sign != 0)
			{
				VerticalSelection = WrapSelection(VerticalSelection + Controller.verticalAxis.sign, SaveManager.WORLD_COUNT);
				menuMemory[MenuKeys.WorldSelect] = VerticalSelection;
				menuMemory[MenuKeys.LevelSelect] = 0; //Reset level selection

				bool isScrollingUp = Controller.verticalAxis.sign < 0;
				int transitionIndex = WrapSelection(isScrollingUp ? VerticalSelection - 1 : VerticalSelection + 1, SaveManager.WORLD_COUNT);
				UpdateSpriteRegion(3, transitionIndex); //Update level text

				animator.Play(isScrollingUp ? "scroll-up" : "scroll-down");
				animator.Seek(0.0, true);
				DisableProcessing();
			}
		}

		protected override void Confirm()
		{
			//World hasn't been unlocked
			if (!SaveManager.ActiveGameData.IsWorldUnlocked(VerticalSelection)) return;

			animator.Play("confirm");
		}

		protected override void Cancel() => animator.Play("cancel");

		public override void ShowMenu() => animator.Play("show");
		public override void OpenSubmenu() => _submenus[VerticalSelection].ShowMenu();

		private void UpdateVideo()
		{
			//Don't change video
			if (!SaveManager.ActiveGameData.IsWorldUnlocked(VerticalSelection) ||
			videoPlayer.Stream == videoStreams[VerticalSelection] || !Controller.IsHoldingNeutral) return;

			if (videoPlayer.IsPlaying())
			{
				if (videoPlayer.GetVideoTexture() != null)
				{
					if (videoCrossfade.Texture != null)
					{
						videoCrossfade.Texture.Dispose();
						videoCrossfade.Texture = null;
					}

					videoFadeFactor = 0;
					crossfadeColor = videoPlayer.Modulate;
					videoCrossfade.Texture = (Texture2D)videoPlayer.GetVideoTexture().Duplicate();
				}

				videoPlayer.Stop();
			}

			videoPlayer.Stream = null;
			CallDeferred(MethodName.ChangeVideo);
		}

		private void ChangeVideo()
		{
			if (isChangingVideo) return;

			isChangingVideo = true;
			videoPlayer.SetDeferred("stream", videoStreams[VerticalSelection]);
		}

		public void UpdateLevelText()
		{
			UpdateSpriteRegion(0, VerticalSelection - 1); //Top option
			UpdateSpriteRegion(1, VerticalSelection); //Center option
			UpdateSpriteRegion(2, VerticalSelection + 1); //Bottom option

			for (int i = 0; i < _levelGlowSprites.Count; i++) //Sync glow regions
				_levelGlowSprites[i].RegionRect = _levelTextSprites[i].RegionRect;
		}

		private void UpdateSpriteRegion(int spriteIndex, int selectionIndex)
		{
			selectionIndex = WrapSelection(selectionIndex, SaveManager.WORLD_COUNT);
			if (!SaveManager.ActiveGameData.IsWorldUnlocked(selectionIndex)) //World isn't unlocked.
				selectionIndex = levelSpriteRegions.Count - 1;

			_levelTextSprites[spriteIndex].RegionRect = levelSpriteRegions[selectionIndex];

			if (spriteIndex == 1) //Updating primary selection
				description.SetText(levelDescriptionKeys[selectionIndex]);
		}
	}
}
