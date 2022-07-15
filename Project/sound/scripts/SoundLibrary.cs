using Godot;
using Godot.Collections;

public class SoundLibrary : Node
{
    [Export]
    public string name;
    [Export]
    public Array<string> ids;
    [Export]
    public Array<AudioStream> clips;
}
