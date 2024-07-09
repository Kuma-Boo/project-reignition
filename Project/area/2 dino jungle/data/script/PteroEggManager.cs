using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects;

/// <summary> Responsible for handling the types of eggs that spawn. </summary>
public partial class PteroEggManager : Node3D
{
	// In the original game, you could only hold one egg at a time; You can now hold as many as you run across
	public static readonly Array<PteroEgg> heldEggs = []; // Contains all the eggs the player is currently holding

	// Models
	[Export]
	public Array<PackedScene> eggModels;
	[Export]
	public Array<PackedScene> signModels;

	private Array<PteroEgg> eggs = [];
	private Array<PteroNest> nests = [];

	public override void _Ready()
	{
		for (int i = 0; i < GetChildCount(); i++)
		{
			Node node = GetChild(i);
			if (node is PteroEgg)
				eggs.Add(node as PteroEgg);
			else if (node is PteroNest)
				nests.Add(node as PteroNest);
		}

		// Number of eggs and nests MUST BE THE SAME!!!
		for (int i = 0; i < eggs.Count; i++)
		{
			int type = GenerateEggType();
			eggs[i].SetType(eggModels[type].Instantiate<Node3D>());
			nests[GenerateEggPair(i)].SetType(signModels[type].Instantiate<Node3D>());
		}
	}

	private Array<int> excludedTypes = []; // Eggs that have already been generated
	private int GenerateEggType()
	{
		int type = Runtime.randomNumberGenerator.RandiRange(0, eggs.Count - 1);
		while (excludedTypes.Contains(type)) // Regenerate type if it's already been taken
		{
			type = Runtime.randomNumberGenerator.RandiRange(0, eggs.Count - 1);
		}

		excludedTypes.Add(type);
		return type;
	}

	private int GenerateEggPair(int eggIndex) // Determines which egg belongs to which nest
	{
		int index = Runtime.randomNumberGenerator.RandiRange(0, eggs.Count - 1);
		while (nests[index].AssignedEgg != null) // Nest is already taken
		{
			index = Runtime.randomNumberGenerator.RandiRange(0, eggs.Count - 1);
		}

		nests[index].AssignedEgg = eggs[eggIndex];
		return index;
	}
}