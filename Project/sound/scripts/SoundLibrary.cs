using Godot;
using Godot.Collections;

public class SoundLibrary : Node
{
    [Export]
    public Array<AudioStream> clips;
}
