using Godot;
using Godot.Collections;

namespace Project.Interface.Menus;

public partial class BookPage : Resource
{
	#region Editor
	public override Array<Dictionary> _GetPropertyList()
	{
		Array<Dictionary> properties = [];

		properties.Add(ExtensionMethods.CreateProperty("Unlock Settings/Mode", Variant.Type.Int, PropertyHint.Enum));
		return properties;
	}

	public override bool _Set(StringName property, Variant value)
	{
		switch ((string)property)
		{
			case "Spawn Settings/Spawn Pearls":
				NotifyPropertyListChanged();
				break;

			default:
				return false;
		}

		return true;
	}

	public override Variant _Get(StringName property)
	{
		switch ((string)property)
		{
		}
		return base._Get(property);
	}
	#endregion


	public enum UnlockMode
	{
		SpecificLevel,
		MedalCount,
		BigCameo,
	}
}
