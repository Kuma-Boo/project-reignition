using Godot;

namespace Project.Gameplay
{
    //Parent class of all stage trigger objects. Always loaded.
    public abstract class StageTriggerObject : Spatial
    {
        public CharacterController Character => CharacterController.instance; //Reference to the character controller

        public virtual void Respawn() { }
        public virtual void Activate() { }
        public virtual void Deactivate(bool isMovingForward) { } //Normally unused. isMovingForward is which way the trigger was left.

        public virtual bool IsRespawnable() => false;
    }
}
