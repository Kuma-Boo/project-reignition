using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Interface
{
	/// <summary>
	/// Plays an event (cutscene) with the correct audio depending on the localization settings
	/// </summary>
	public class EventPlayer : Node
	{
		[Export]
		public AudioStream enAudio;
		[Export]
		public AudioStream jaAudio;
		[Export(PropertyHint.MultilineText)]
		public string subtitleData = string.Empty; //Subtitle data, as a csv file
		private Gameplay.Triggers.DialogTrigger _subtitles;

		[Export]
		public NodePath videoPlayer;
		private VideoPlayer _videoPlayer;

		[Export]
		public NodePath audioPlayer;
		private AudioStreamPlayer _audioPlayer;

		private InputManager.Controller Controller => InputManager.controller;

		public override void _Ready()
		{
			_videoPlayer = GetNode<VideoPlayer>(videoPlayer);
			_audioPlayer = GetNode<AudioStreamPlayer>(audioPlayer);

			if(!string.IsNullOrEmpty(subtitleData))
				CreateSubtitles();
			
			_audioPlayer.Stream = SaveManager.UseEnglishVoices ? enAudio : jaAudio;
			CallDeferred(nameof(StartCutscene));
		}

		private void StartCutscene()
		{
			_videoPlayer.Play();
			_audioPlayer.Play();

			if(_subtitles != null)
				_subtitles.Activate();
		}

		public override void _PhysicsProcess(float _)
		{
			if(Controller.pauseButton.wasPressed) //Skip cutscene
			{

			}
			else if (Controller.actionButton.wasPressed) //Check if we're in the cutscene viewer, and return to the menu
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
			_subtitles = new Gameplay.Triggers.DialogTrigger()
			{
				delays = new Array<float>(),
				displayLength = new Array<float>(),
				textKeys = new Array<string>(),
			};
			AddChild(_subtitles);

			//Calculate the delays and display lengths
			string[] dataPoints = subtitleData.Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
			string[] currentData = dataPoints[1].Split('	');
			string[] nextData = dataPoints[2].Split('	');
			float previousSpacing = GetStartTime(currentData); //First key is an exception and uses it's start time as delay
			float nextStartTime = GetStartTime(nextData);

			for (int i = 1; i < dataPoints.Length - 1; i++) //Skip the first and last keys
			{
				_subtitles.textKeys.Add(currentData[0]); //Assign key
				float currentStartTime = GetStartTime(currentData); //When to start subtitles
				float currentSpacing = GetSpacing(currentData); //Space between this subtitle and the next

				_subtitles.delays.Add(previousSpacing); //Copy from previous delay
				_subtitles.displayLength.Add(nextStartTime - currentStartTime - currentSpacing);

				//Advance read position
				currentData = nextData;
				if(i < dataPoints.Length - 2)
					nextData = dataPoints[i + 2].Split('	');
				else
					nextData = dataPoints[i + 1].Split('	');
					
				//Update values
				previousSpacing = currentSpacing;
				nextStartTime = GetStartTime(nextData);
			}

			//Deal with the last key
			currentData = dataPoints[dataPoints.Length - 1].Split('	');
			_subtitles.textKeys.Add(currentData[0]); //Assign key
			_subtitles.delays.Add(previousSpacing);
			_subtitles.displayLength.Add(currentData[2].ToFloat()); //Final key's spacing is casted to be the display length
		}

		public void OnEventFinished() //Called after the cutscene has finished playing
		{
			
		}
	}
}
