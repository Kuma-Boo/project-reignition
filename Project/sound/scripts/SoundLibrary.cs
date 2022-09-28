using Godot;
using Godot.Collections;

public partial class SoundLibrary : Node
{
    [Export]
    public Array<AudioStream> clips;
}
