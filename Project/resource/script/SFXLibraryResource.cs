using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay
{
	/// <summary>
	/// A collection of sound effects. Each key can have multiple sound effects associated with it, which will be chosen randomly.
	/// Note: There isn't any restrictions to avoid having the same sound effect play multiple times in a row.
	/// Important note: DO NOT reorder keys, otherwise stream data will become desynced
	/// </summary>
	[Tool]
	public partial class SFXLibraryResource : Resource
	{
		#region Editor
		public override Array<Dictionary> _GetPropertyList()
		{
			Array<Dictionary> properties = new Array<Dictionary>();

			properties.Add(ExtensionMethods.CreateProperty("Settings/Channel Count", Variant.Type.Int, PropertyHint.Range, "1,9"));
			properties.Add(ExtensionMethods.CreateProperty("Settings/Key Names", Variant.Type.Array, PropertyHint.TypeString, "21/0:StringName"));
			ValidateArrays();

			channelEditingIndex = Mathf.Clamp(channelEditingIndex, 1, channelCount);
			keyEditingIndex = Mathf.Clamp(keyEditingIndex, 0, KeyCount);

			properties.Add(ExtensionMethods.CreateProperty("Editing/Key", Variant.Type.Int, PropertyHint.Enum, GetKeyList(keys)));
			properties.Add(ExtensionMethods.CreateProperty("Editing/Channel", Variant.Type.Int, PropertyHint.Range, "1, 9"));

			if (KeyCount != 0)
				properties.Add(ExtensionMethods.CreateProperty("Editing/Streams", Variant.Type.Array, PropertyHint.TypeString, "24/17:AudioStream"));

			return properties;
		}

		public override Variant _Get(StringName property)
		{
			switch ((string)property)
			{
				case "Settings/Channel Count":
					return channelCount;
				case "Settings/Key Names":
					return keys;
				case "Editing/Key":
					return keyEditingIndex;
				case "Editing/Channel":
					return channelEditingIndex;
				case "Editing/Streams":
					return streams[channelEditingIndex - 1][keyEditingIndex];
			}

			return base._Get(property);
		}

		public override bool _Set(StringName property, Variant value)
		{
			switch ((string)property)
			{
				case "Settings/Channel Count":
					channelCount = (int)value;
					ValidateArrays();
					NotifyPropertyListChanged();
					break;
				case "Settings/Key Names":
					keys = (Array<StringName>)value;
					ValidateArrays();
					NotifyPropertyListChanged();
					break;
				case "Editing/Key":
					keyEditingIndex = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Editing/Channel":
					channelEditingIndex = (int)value;
					NotifyPropertyListChanged();
					break;
				case "Editing/Streams":
					streams[channelEditingIndex - 1][keyEditingIndex] = (Array<AudioStream>)value;
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

		/// <summary>
		/// Ensures all arrays are the correct size and are not null.
		/// </summary>
		private void ValidateArrays()
		{
			if (keys == null)
				keys = new Array<StringName>();

			if (streams == null)
				streams = new Array<Array<Array<AudioStream>>>();

			if (streams.Count != channelCount)
				streams.Resize(channelCount);

			for (int i = 0; i < channelCount; i++)
			{
				if (streams[i] == null || streams[i].Count == 0)
					streams[i] = new Array<Array<AudioStream>>();

				if (streams[i].Count != KeyCount)
					streams[i].Resize(KeyCount);

				for (int j = 0; j < KeyCount; j++)
				{
					if (streams[i][j] == null || streams[i][j].Count == 0)
						streams[i][j] = new Array<AudioStream>();
				}
			}
		}

		/// <summary>
		/// Ensure there aren't any duplicate keys.
		/// </summary>
		private void CheckDuplicateKeys()
		{
			Array<string> duplicateKeyChecker = new Array<string>();
			for (int i = 0; i < KeyCount; i++)
			{
				if (string.IsNullOrEmpty(keys[i]))
					GD.PrintErr($"Warning! Voice Key '{i}' is empty.");
				else if (duplicateKeyChecker.Contains(keys[i]))
					GD.PrintErr($"Warning! Voice Key '{keys[i]}' (Index {i}) is a duplicate.");
				else
					duplicateKeyChecker.Add(keys[i]);
			}
			duplicateKeyChecker.Clear();
		}
		#endregion

		[ExportSubgroup("DON'T EDIT THESE DIRECTLY!")]
		[Export]
		private Array<StringName> keys;
		public int KeyCount => keys.Count;

		/// <summary>
		/// How many channels does this library contain?
		/// Voice libraries should have 2. [0 -> En, 1 -> Ja]
		/// </summary>
		[Export]
		private int channelCount = 1;

		/// <summary> Arrays are ordered in Channel -> Key -> Index. </summary>
		[Export]
		private Array<Array<Array<AudioStream>>> streams;

		/// <summary> Current channel index being edited in the inspector. </summary>
		private int channelEditingIndex;
		/// <summary> Current key index being edited in the inspector. </summary>
		private int keyEditingIndex;

		/// <summary>
		/// Returns a random sound effect from a library.
		/// Channel can be useful for multiple languages.
		/// sfxIndex can be used to override rng.
		/// </summary>
		public AudioStream GetStream(StringName key, int channel = 0, int sfxIndex = -1)
		{
			int keyIndex = keys.GetStringNameIndex(key);

			if (keyIndex == -1)
			{
				GD.PrintErr($"Couldn't find sfx '{key}'!");
				return null;
			}

			int maxIndex = streams[channel][keyIndex].Count; //Get max random index

			if (maxIndex == 0) //No sound effect found
			{
				GD.PrintErr($"No sfx found for '{key}' on channel {channel}!");
				return null;
			}

			if (maxIndex == 1) //Randomization isn't possible with only one sfx.
				sfxIndex = 0;
			else if (sfxIndex == -1) //Randomize sfx
				sfxIndex = RuntimeConstants.randomNumberGenerator.RandiRange(0, maxIndex - 1);

			return streams[channel][keyIndex][sfxIndex];
		}

		public StringName GetKeyByIndex(int index) => keys[index];
	}
}
