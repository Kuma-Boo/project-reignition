using Godot;
using Godot.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System;

namespace Project.Gameplay;

/// <summary> Master skill list. </summary>
[Tool]
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

	/// <summary> Gets the matching Base SkillResource from a SkillKey. </summary>
	public SkillResource GetSkill(SkillKey key)
	{
		if (key == SkillKey.Max)
			return null;

		foreach (var skill in skills)
		{
			GD.PrintT(skill.Key, key, skill.IsAugment);
			if (skill.Key == key && !skill.IsAugment)
				return skill;
		}

		GD.PushWarning($"Couldn't find a skill with the key: {key}!");
		return null;
	}

	private const string FileExtension = ".tres";

	// Rebuilds the skill list
	private void RebuildSkillList()
	{
		if (!Engine.IsEditorHint() || string.IsNullOrWhiteSpace(skillResourcePath))
			return;

		skills.Clear();

		// Load skills from skill directory
		DirAccess directory = DirAccess.Open(skillResourcePath);
		Array<string> files = new(directory.GetFiles());

		// Populate skill list
		for (int i = files.Count - 1; i >= 0; i--)
		{
			string targetFile = skillResourcePath + files[i];
			Resource resource = ResourceLoader.Load(targetFile);
			if (resource is not SkillResource) // Not a skill resource
			{
				files.RemoveAt(i);
				continue;
			}

			skills.Insert(0, resource as SkillResource);
			files[i] = files[i].Replace(FileExtension, string.Empty); // Remove file extension
		}

		// Make sure keys are set up correctly
		for (int i = 0; i < skills.Count; i++)
		{
			// Attempt to match file names (without numbers) with SkillKey
			if (!Enum.TryParse(Regex.Replace(files[i], "[0-9]", string.Empty), out SkillKey key))
				continue;

			skills[i].Key = key;
			skills[i].Augments?.Clear(); // Clear augments (they'll be added on the next pass)
			ResourceSaver.Singleton.Save(skills[i], skillResourcePath + files[i] + FileExtension, ResourceSaver.SaverFlags.ChangePath); // Save skill resource
		}

		// Make sure conflicting skills stay in sync with each other
		foreach (SkillResource skill in skills)
		{
			if (skill.SkillConflicts == null) // Skill has no conflicts
				continue;

			for (int j = skill.SkillConflicts.Count - 1; j >= 0; j--)
			{
				if (!Enum.TryParse(skill.SkillConflicts[j], out SkillKey conflictKey))
				{
					GD.PushWarning($"Couldn't find conflict {skill.SkillConflicts[j]} on skill {skill.ResourcePath}.");
					continue;
				}

				if (conflictKey == skill.Key) // Make sure skills don't conflict with themselves
				{
					skill.SkillConflicts.RemoveAt(j);
					continue;
				}

				SkillResource conflict = GetSkill(conflictKey); // Get the base version of the conflicting skill
				if (conflict == null)
				{
					skill.SkillConflicts.RemoveAt(j);
					GD.Print($"Couldn't find base skill for {skill.SkillConflicts[j]}.");
					continue;
				}

				if (conflict.SkillConflicts?.Contains(skill.Key.ToString()) == true) // Conflicts are synced, continue...
					continue;

				if (conflict.SkillConflicts == null)
					conflict.SkillConflicts = [];

				// Make sure file can be saved
				int fileIndex = files.IndexOf(conflict.Key.ToString());
				if (fileIndex == -1)
				{
					GD.PushWarning($"Couldn't save skill conflict between {skill.ResourcePath} and {conflict.ResourcePath}!");
					continue;
				}
				// Resync conflicts
				conflict.SkillConflicts.Add(skill.Key.ToString());

				// Save the conflicting skill resource
				string targetFilePath = skillResourcePath + files[fileIndex] + ".tres";
				ResourceSaver.Singleton.Save(skill, targetFilePath, ResourceSaver.SaverFlags.None);
			}
		}

		// Update augments
		for (int i = skills.Count - 1; i >= 0; i--)
		{
			SkillResource baseSkill = GetSkill(skills[i].Key);
			if (!skills[i].IsAugment) // Not an augment
			{
				if (baseSkill != skills[i]) // Log a warning for duplicate skills
					GD.PushWarning($"{baseSkill.ResourcePath} and {skills[i].ResourcePath} use the same SkillKey. Make sure one of them is configured to be an augment.");

				continue;
			}

			if (!baseSkill.HasAugments) // Reset augments if null
				baseSkill.Augments = [];

			// Make sure file can be saved
			int fileIndex = files.IndexOf(baseSkill.Key.ToString());
			if (fileIndex == -1)
			{
				GD.PushWarning($"Couldn't update {skills[i].ResourcePath}'s base skill ({baseSkill.ResourcePath}) augment list!");
				continue;
			}

			// Update base skill list
			baseSkill.Augments.Add(skills[i]);
			List<SkillResource> augmentList = [.. baseSkill.Augments.ToArray()];
			augmentList.Sort(new SkillRing.AugmentSorter());

			baseSkill.Augments.Clear();
			baseSkill.Augments.AddRange(augmentList);

			// Save the base skill resource
			string targetFilePath = skillResourcePath + files[fileIndex] + ".tres";
			ResourceSaver.Singleton.Save(skills[i], targetFilePath, ResourceSaver.SaverFlags.None);
			skills.RemoveAt(i); // Remove the skill from the main skill list
		}

		// Reorder skill list based on skill number
		List<SkillResource> skillList = [.. skills.ToArray()];
		skillList.Sort(new SkillRing.KeySorter());

		skills.Clear();
		skills.AddRange(skillList);

		ResourceSaver.Singleton.Save(this, skillResourcePath + "_SkillList.tres", ResourceSaver.SaverFlags.None);
		GD.Print("Skill List has been rebuilt.");
	}
}