using Godot;
using Project.Core;

namespace Project.Interface.Menus
{
	public partial class ReadyMenu : Menu
	{
		[Export]
		private Label mapLabel;
		[Export]
		private Label missionLabel;

		public void SetMapText(string text) => mapLabel.Text = text;
		public void SetMissionText(string text) => missionLabel.Text = text;


		public override void ShowMenu()
		{
			base.ShowMenu();
			HorizontalSelection = 0; // Default to yes
		}

		/// <summary> Path to the level scene. </summary>
		public string LevelPath { get; set; }
		/// <summary> Loads the level. </summary>
		protected override void Confirm()
		{
			TransitionManager.QueueSceneChange(LevelPath);
			TransitionManager.StartTransition(new TransitionData()
			{
				inSpeed = 1f,
				color = Colors.Black,
				loadAsynchronously = true
			});
		}
	}
}
