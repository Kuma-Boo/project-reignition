using Godot;

namespace Project.Gameplay.Objects
{
    public class Pickup : RespawnableObject
    {
        [Signal]
        public delegate void Collected();
        protected override bool IsRespawnable() => true;

        public void OnEnter() => CallDeferred(nameof(Collect));

        protected virtual void Collect()
        {
            EmitSignal(nameof(Collected));
        }
    }
}
