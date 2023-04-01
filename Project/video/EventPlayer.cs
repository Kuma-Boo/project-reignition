using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface.Menus
{
	/// <summary>
	/// Plays an event (cutscene) with the correct audio depending on the localization settings
	/// </summary>
	public partial class EventPlayer : Node
	{
		/// Scene to switch to after event is finished.
		public static string QueuedScene { get; set; }

		[Export]
		private AudioStream enAudio;
		[Export]
		private AudioStream jaAudio;
		/// <summary> Subtitle time table, separated by spaces. </summary>
		[Export(PropertyHint.MultilineText)]
		private string subtitleData;
		private Gameplay.Triggers.DialogTrigger subtitles;

		[Export]
		private AudioStreamPlayer audioPlayer;
		[Export]
		private VideoStreamPlayer videoPlayer;

		private float skipTimer;
		private const float SKIP_LENGTH = 1f; //How long the pause button needs to be held to skip the cutscene

		public override void _Ready()
		{
			if (!string.IsNullOrEmpty(subtitleData))
				CreateSubtitles();

			audioPlayer.Stream = SaveManager.UseEnglishVoices ? enAudio : jaAudio;
			CallDeferred(nameof(StartCutscene));
		}

		private void StartCutscene()
		{
			videoPlayer.Play();
			audioPlayer.Play();

			if (subtitles != null)
				subtitles.Activate();
		}

		public override void _PhysicsProcess(double _)
		{
			if (Input.IsActionPressed("button_pause")) //Skip cutscene
			{
				skipTimer = Mathf.MoveToward(skipTimer, 1, PhysicsManager.physicsDelta);
				if (Mathf.IsEqualApprox(skipTimer, 1))
				{

				}
			}
			else
				skipTimer = Mathf.MoveToward(skipTimer, 0, PhysicsManager.physicsDelta);

			// Only do this when viewing from the special book
			if (Menu.menuMemory[Menu.MemoryKeys.ActiveMenu] == (int)Menu.MemoryKeys.SpecialBook &&
				Input.IsActionJustPressed("button_action")) // Return to the special book menu
			{
			}
		}

		private float GetStartTime(string[] data)
		{
			float t = SaveManager.UseEnglishVoices ? data[1].ToFloat() : data[3].ToFloat();
			if (t == -1) //Fallback to english
				t = data[1].ToFloat();
			return t;
		}

		private float GetSpacing(string[] data)
		{
			float t = SaveManager.UseEnglishVoices ? data[2].ToFloat() : data[4].ToFloat();
			if (t == -1) //Fallback to english
				t = data[2].ToFloat();
			return t;
		}

		private void CreateSubtitles()
		{
			subtitles = new Gameplay.Triggers.DialogTrigger()
			{
				isCutscene = true,
				delays = new Array<float>(),
				displayLength = new Array<float>(),
				textKeys = new Array<string>(),
			};
			AddChild(subtitles);

			//Calculate the delays and display lengths
			string[] dataPoints = subtitleData.Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
			string[] currentData = dataPoints[0].Split('	');
			string[] nextData = dataPoints[1].Split('	');
			float previousSpacing = GetStartTime(currentData); //First key is an exception and uses it's start time as delay
			float nextStartTime = GetStartTime(nextData);

			for (int i = 0; i < dataPoints.Length - 1; i++) //Skip the last key
			{
				subtitles.textKeys.Add(currentData[0]); //Assign key
				float currentStartTime = GetStartTime(currentData); //When to start subtitles
				float currentSpacing = GetSpacing(currentData); //Space between this subtitle and the next

				subtitles.delays.Add(previousSpacing); //Copy from previous delay
				subtitles.displayLength.Add(nextStartTime - currentStartTime - currentSpacing);

				//Advance read position
				currentData = nextData;
				if (i < dataPoints.Length - 2)
					nextData = dataPoints[i + 2].Split('	');
				else
					nextData = dataPoints[i + 1].Split('	');

				//Update values
				previousSpacing = currentSpacing;
				nextStartTime = GetStartTime(nextData);
			}

			//Deal with the last key
			currentData = dataPoints[dataPoints.Length - 1].Split('	');
			subtitles.textKeys.Add(currentData[0]); //Assign key
			subtitles.delays.Add(previousSpacing);
			subtitles.displayLength.Add(currentData[2].ToFloat()); //Final key's spacing is casted to be the display length
		}


		/// <summary> Called after the cutscene has finished playing. </summary>
		public void OnEventFinished()
		{

		}
	}
}
