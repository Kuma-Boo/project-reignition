using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	[Tool]
	public partial class CharacterSound : Node
	{
		/*
		For some reason, there seem to be a lot of duplicate AudioStreams from the original game.
		Will leave them unused for now.
		*/

		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();


			if (actionEditingIndex >= actionKeys.Count) //Out of range
				actionEditingIndex = 0;
			properties.Add(ExtensionMethods.CreateProperty("Actions/Editing Key", Variant.Type.Int, PropertyHint.Enum, GetKeyList(actionKeys)));
			if (actionStreams != null)
			{
				if (actionKeys.Count != actionStreams.Count)
					actionStreams.Resize(actionKeys.Count);

				properties.Add(ExtensionMethods.CreateProperty("Actions/Audio Stream", Variant.Type.Object, PropertyHint.ResourceType, "AudioStream"));
			}


			if (materialEditingIndex >= materialKeys.Count) //Out of range
				materialEditingIndex = 0;
			properties.Add(ExtensionMethods.CreateProperty("Materials/Editing Key", Variant.Type.Int, PropertyHint.Enum, GetKeyList(materialKeys)));
			if (stepStreams != null)
			{
				if (materialKeys.Count != stepStreams.Count)
					stepStreams.Resize(materialKeys.Count);

				properties.Add(ExtensionMethods.CreateProperty("Materials/Stepping Streams", Variant.Type.Array, PropertyHint.TypeString, "24/17:AudioStream"));
			}

			if (landingStreams != null)
			{
				if (materialKeys.Count != landingStreams.Count)
					landingStreams.Resize(materialKeys.Count);

				properties.Add(ExtensionMethods.CreateProperty("Materials/Landing Streams", Variant.Type.Array, PropertyHint.TypeString, "24/17:AudioStream"));
			}



			if (voiceEditingIndex >= voiceKeys.Count) //Out of range
				voiceEditingIndex = 0;
			properties.Add(ExtensionMethods.CreateProperty("Voices/Editing Key", Variant.Type.Int, PropertyHint.Enum, GetKeyList(voiceKeys)));
			if (enStreams.Count != voiceKeys.Count || jaStreams.Count != voiceKeys.Count)
			{
				enStreams.Resize(voiceKeys.Count);
				jaStreams.Resize(voiceKeys.Count);
			}

			properties.Add(ExtensionMethods.CreateProperty("Voices/English Stream", Variant.Type.Object, PropertyHint.ResourceType, "AudioStream"));
			properties.Add(ExtensionMethods.CreateProperty("Voices/Japanese Stream", Variant.Type.Object, PropertyHint.ResourceType, "AudioStream"));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Actions/Editing Key":
					return actionEditingIndex;
				case "Actions/Audio Stream":
					return actionStreams[actionEditingIndex];

				case "Materials/Editing Key":
					return materialEditingIndex;
				case "Materials/Stepping Streams":
					return stepStreams[materialEditingIndex];
				case "Materials/Landing Streams":
					return landingStreams[materialEditingIndex];

				case "Voices/Editing Key":
					return voiceEditingIndex;
				case "Voices/English Stream":
					return enStreams[voiceEditingIndex];
				case "Voices/Japanese Stream":
					return jaStreams[voiceEditingIndex];
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Actions/Editing Key":
					actionEditingIndex = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Actions/Audio Stream":
					if (actionStreams == null)
						break;
					actionStreams[actionEditingIndex] = (AudioStream)value;
					break;

				case "Materials/Editing Key":
					materialEditingIndex = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Materials/Stepping Streams":
					if (stepStreams == null)
						break;
					stepStreams[materialEditingIndex] = (Array<AudioStream>)value;
					break;
				case "Materials/Landing Streams":
					if (landingStreams == null)
						break;
					landingStreams[materialEditingIndex] = (Array<AudioStream>)value;
					break;

				case "Voices/Editing Key":
					voiceEditingIndex = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Voices/English Stream":
					if (enStreams == null)
						break;
					enStreams[voiceEditingIndex] = (AudioStream)value;
					break;
				case "Voices/Japanese Stream":
					if (jaStreams == null)
						break;
					jaStreams[voiceEditingIndex] = (AudioStream)value;
					break;
				default:
					return false;
			}

			return true;
		}

		private string GetKeyList(Array<StringName> array)
		{
			string value = string.Empty;
			for (int i = 0; i < array.Count; i++)
			{
				value += array[i];
				if (i < array.Count - 1)
					value += ",";
			}

			return value;
		}

		private void CheckDuplicateKeys()
		{
			Array<string> duplicateKeyChecker = new Array<string>();
			for (int i = 0; i < actionKeys.Count; i++)
			{
				if (string.IsNullOrEmpty(actionKeys[i]))
					GD.PrintErr($"Warning! Action Key '{i}' is empty.");
				else if (duplicateKeyChecker.Contains(actionKeys[i]))
					GD.PrintErr($"Warning! Action Key '{actionKeys[i]}' (Index {i}) is a duplicate.");
				else
					duplicateKeyChecker.Add(actionKeys[i]);
			}
			duplicateKeyChecker.Clear();

			for (int i = 0; i < materialKeys.Count; i++)
			{
				if (string.IsNullOrEmpty(materialKeys[i]))
					GD.PrintErr($"Warning! Material Key '{i}' is empty.");
				else if (duplicateKeyChecker.Contains(materialKeys[i]))
					GD.PrintErr($"Warning! Material Key '{materialKeys[i]}' (Index {i}) is a duplicate.");
				else
					duplicateKeyChecker.Add(materialKeys[i]);
			}
			duplicateKeyChecker.Clear();

			for (int i = 0; i < voiceKeys.Count; i++)
			{
				if (string.IsNullOrEmpty(voiceKeys[i]))
					GD.PrintErr($"Warning! Voice Key '{i}' is empty.");
				else if (duplicateKeyChecker.Contains(voiceKeys[i]))
					GD.PrintErr($"Warning! Voice Key '{voiceKeys[i]}' (Index {i}) is a duplicate.");
				else
					duplicateKeyChecker.Add(voiceKeys[i]);
			}
			duplicateKeyChecker.Clear();
		}
		#endregion

		public override void _Ready()
		{
			/*
			//Use this code snippet to figure out the hint key for arrays
			foreach (Dictionary item in GetPropertyList())
			{
				if ((string)item["name"] == "enClips")
					GD.Print(item);
			}
			*/

			if (Engine.IsEditorHint())
			{
				CheckDuplicateKeys();
				return;
			}

			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechStart, new Callable(this, MethodName.MuteGameplayVoice));
			SoundManager.instance.Connect(SoundManager.SignalName.SonicSpeechEnd, new Callable(this, MethodName.UnmuteGameplayVoice));
		}

		//Important note: DO NOT reorder keys, otherwise stream data will become desynced
		//Actions (Jumping, sliding, etc)
		[ExportGroup("Actions")]
		[Export]
		private Array<StringName> actionKeys;
		[Export]
		private Array<AudioStream> actionStreams;
		[Export]
		private AudioStreamPlayer actionChannel; //Channel for playing action sound effects
		private int actionEditingIndex;
		public void PlayActionSFX(StringName key)
		{
			int index = actionKeys.GetStringNameIndex(key);
			if (index == -1)
			{
				GD.PrintErr($"Couldn't find action key '{key}'!");
				return;
			}

			actionChannel.Stream = actionStreams[index];
			actionChannel.Play();
		}


		//Materials (footsteps, landing, etc)
		[ExportGroup("Materials")]
		[Export]
		private Array<StringName> materialKeys;
		[Export]
		private Array<Array<AudioStream>> stepStreams;
		[Export]
		private Array<Array<AudioStream>> landingStreams;
		[Export]
		private AudioStreamPlayer footstepChannel;
		[Export]
		private AudioStreamPlayer landingChannel;
		private int materialEditingIndex;
		private int groundKeyIndex; //Type of ground
		public void PlayLandingSFX()
		{
			int sfxIndex = RuntimeConstants.randomNumberGenerator.RandiRange(0, landingStreams[groundKeyIndex].Count - 1);
			landingChannel.Stream = landingStreams[groundKeyIndex][sfxIndex];
			landingChannel.Play();
		}

		public void PlayFootstepSFX()
		{
			int sfxIndex = RuntimeConstants.randomNumberGenerator.RandiRange(0, stepStreams[groundKeyIndex].Count - 1);
			footstepChannel.Stream = stepStreams[groundKeyIndex][sfxIndex];
			footstepChannel.Play();
		}

		public void UpdateGroundType(Node collision)
		{
			//Loop through material keys and see if anything matches
			for (int i = 0; i < materialKeys.Count; i++)
			{
				if (!collision.IsInGroup(materialKeys[i])) continue;

				groundKeyIndex = i;
				return;
			}

			if (groundKeyIndex != 0) //Avoid being spammed with warnings
			{
				GD.PrintErr($"'{collision.Name}' isn't in any sound groups found in CharacterSound.cs.");
				groundKeyIndex = 0; //Default to first key is the default (pavement)
			}
		}

		[ExportGroup("Voices")]
		[Export]
		private Array<StringName> voiceKeys;
		[Export]
		private Array<AudioStream> enStreams;
		[Export]
		private Array<AudioStream> jaStreams;
		[Export]
		private AudioStreamPlayer voiceChannel;
		private int voiceEditingIndex;
		public void PlayVoice(StringName key)
		{
			int index = voiceKeys.GetStringNameIndex(key);

			if (index == -1)
			{
				GD.PrintErr($"Couldn't find voice key '{key}'!");
				return;
			}

			voiceChannel.Stream = SaveManager.UseEnglishVoices ? enStreams[index] : jaStreams[index];
			voiceChannel.Play();
		}

		private void MuteGameplayVoice() //Kills channel
		{
			voiceChannel.Stop();
			voiceChannel.VolumeDb = -80f;
		}

		private void UnmuteGameplayVoice() //Stops any currently active voice clip and resets channel volume
		{
			voiceChannel.Stop();
			voiceChannel.VolumeDb = 0f;
		}
	}
}
