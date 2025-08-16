using Godot;
using Project.Core;

namespace Project.Gameplay;

[GlobalClass]
public partial class WorldDataResource : Resource
{
	[Export] public LevelDataResource[] Levels { get; private set; }
	[Export] public SaveManager.WorldEnum World { get; private set; }
	[Export] public StringName WorldKey { get; private set; }
}
