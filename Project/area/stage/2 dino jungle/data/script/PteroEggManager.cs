using Godot;
using Godot.Collections;
using Project.Core;

namespace Project.Gameplay.Objects
{
	/// <summary>
	/// Responsible for handling the types of eggs that spawn.
	/// </summary>
	public partial class PteroEggManager : Node3D
	{
		//In the original game, you could only hold one egg at a time.
		//You can now hold 2. 
		private static PteroEgg eggOne; //First held egg
		private static PteroEgg eggTwo; //Second held egg
		public static bool IsEggHeld(PteroEgg egg) => eggOne == egg || eggTwo == egg;
		public static void DropEgg(PteroEgg egg)
		{
			if (eggOne == egg)
			{
				eggOne.Frighten();
				eggOne = eggTwo;
			}

			eggTwo = null;
			egg.EggIndex = 0;
		}
		public static void PickUpEgg(PteroEgg egg)
		{
			if (eggTwo != null) //Too many eggs!
				DropEgg(eggOne);

			if (eggOne == null)
			{
				eggOne = egg;
				egg.EggIndex = 1;
			}
			else
			{
				eggTwo = egg;
				egg.EggIndex = 2;
			}
		}

		//Models
		[Export]
		public PackedScene[] eggs;
		[Export]
		public PackedScene[] signs;

		private Array<PteroEgg> _eggs = new Array<PteroEgg>();
		private Array<PteroNest> _nests = new Array<PteroNest>();

		public override void _Ready()
		{
			for (int i = 0; i < GetChildCount(); i++)
			{
				Node node = GetChild(i);
				if (node is PteroEgg)
					_eggs.Add(node as PteroEgg);
				else if (node is PteroNest)
					_nests.Add(node as PteroNest);
			}

			//Number of eggs and nests MUST BE THE SAME!!!
			for (int i = 0; i < _eggs.Count; i++)
			{
				int type = GenerateEggType();
				_eggs[i].SetType(eggs[type].Instantiate<Node3D>());
				_nests[GenerateEggPair(i)].SetType(signs[type].Instantiate<Node3D>());
			}
		}

		public override void _ExitTree() //Clear memory
		{
			eggOne = eggTwo = null;
		}

		private Array<int> _excludedTypes = new Array<int>(); //Eggs that have already been generated
		private int GenerateEggType()
		{
			int type = RuntimeConstants.randomNumberGenerator.RandiRange(0, 8);
			while (_excludedTypes.Contains(type)) //Regenerate type if it's already been taken
			{
				type = RuntimeConstants.randomNumberGenerator.RandiRange(0, 8);
			}

			_excludedTypes.Add(type);
			return type;
		}

		private int GenerateEggPair(int eggIndex) //Determines which egg belongs to which nest
		{
			int index = RuntimeConstants.randomNumberGenerator.RandiRange(0, _eggs.Count - 1);
			while (_nests[index].AssignedEgg != null) //Nest is already taken
			{
				index = RuntimeConstants.randomNumberGenerator.RandiRange(0, _eggs.Count - 1);
			}

			_nests[index].AssignedEgg = _eggs[eggIndex];
			return index;
		}
	}
}
