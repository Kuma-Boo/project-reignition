using Godot;
using Godot.Collections;
using System.Linq;
using System.Collections.Generic;

namespace Project.Gameplay;

/// <summary> Master skill list. </summary>
[Tool]
[GlobalClass]
public partial class SkillListResource : Resource
{
	public override Array<Dictionary> _GetPropertyList() => [ExtensionMethods.CreateProperty("Rebuild", Variant.Type.Bool)];

	public override bool _Set(StringName property, Variant value)
	{
		if (property == "Rebuild")
		{
			RebuildSkillList();
			NotifyPropertyListChanged();
		}

		return base._Set(property, value);
	}

	[Export]
	private string skillResourcePath;
	[Export(PropertyHint.ArrayType, "SkillResource")]
	private Array<SkillResource> skills = [];

	/// <summary> Gets the matching skill based on a SkillKey. </summary>
	public SkillResource GetSkill(SkillKey key)
	{
		foreach (var skill in skills)
		{
			if (skill.Key == key)
				return skill;
		}

		GD.PushError($"Couldn't find a skill with the key: {key}!");
		return null;
	}

	// Rebuilds the skill list
	private void RebuildSkillList()
	{
		if (!Engine.IsEditorHint() || string.IsNullOrWhiteSpace(skillResourcePath))
			return;

		skills.Clear();

		// Load skills from skill directory
		for (int i = 0; i < (int)SkillKey.Max; i++)
		{
			SkillKey key = (SkillKey)i;
			string targetFile = skillResourcePath + key.ToString() + ".tres";
			if (!ResourceLoader.Exists(targetFile))
			{
				GD.Print($"Couldn't find file {targetFile}.");
				continue;
			}

			Resource resource = ResourceLoader.Load<SkillResource>(targetFile, "SkillResource");
			GD.PrintT(targetFile, resource, resource is SkillResource);
			SkillResource skill = (SkillResource)resource;
			skills.Add(skill);
			ResourceSaver.Singleton.Save(skill, targetFile, ResourceSaver.SaverFlags.None);

			if (skill.Key != key)
				skill.Key = key;
		}

		// Reorder skill list
		List<SkillResource> skillList = [.. skills.ToArray()];
		skillList.Sort(new SkillRing.KeySorter());

		skills.Clear();
		skills.AddRange(skillList);

		ResourceSaver.Singleton.Save(this, skillResourcePath + "_SkillList.tres", ResourceSaver.SaverFlags.None);
		GD.Print("Skill List has been rebuilt.");
	}
}