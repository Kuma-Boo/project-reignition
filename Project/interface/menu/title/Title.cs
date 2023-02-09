using Godot;
using Project.Core;

namespace Project.Interface.Menus
{
	/// <summary>
	/// Press start. Also plays an intro cutscene if you wait long enough.
	/// </summary>
	public partial class Title : Menu
	{
		[Export]
		private AnimationPlayer animator;

		private bool isCutsceneActive;
		private float cutsceneTimer;
		private const float CUTSCENE_TIME_LENGTH = 5f;

		protected override void ProcessMenu()
		{
			if (isCutsceneActive)
			{
				SoundManager.FadeSFX(bgm, .2f);
				if (Controller.pauseButton.wasPressed || Controller.jumpButton.wasPressed)
					FinishCutscene();
			}
			else if (Controller.AnyButtonPressed) //Change menu
			{
				animator.Play("confirm");
				return;
			}
			else
			{
				cutsceneTimer += PhysicsManager.physicsDelta;
				if (cutsceneTimer >= CUTSCENE_TIME_LENGTH && !isCutsceneActive)
				{
					StartCutscene();
					return;
				}
			}
		}

		//Activate main menu (submenu 0);
		public override void OpenSubmenu() => _submenus[0].ShowMenu();

		public override void ShowMenu()
		{
			animator.Play("RESET");
			animator.Seek(0, true);
			animator.Play("show");

			cutsceneTimer = 0;
		}

		private void StartCutscene()
		{
			isCutsceneActive = true;
			animator.Play("cutscene-start");
		}

		private void FinishCutscene()
		{
			cutsceneTimer = 0;
			isCutsceneActive = false;
			animator.Play("cutscene-finish");
		}
	}
}
