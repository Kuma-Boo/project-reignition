using Godot;

namespace Project.Gameplay.Triggers;

public partial class EnvironmentFxTrigger : StageTriggerModule
{
	[Export(PropertyHint.Range, "0,1,0.1")] private float environmentFxFactor = 1f;
	[Export(PropertyHint.Range, "0,1,.1,or_greater")] private float blendTime = .2f;

	public override void Activate() => StageSettings.Instance.SetEnvironmentFxFactor(environmentFxFactor, blendTime);
	public override void Deactivate() => StageSettings.Instance.SetEnvironmentFxFactor(1f - environmentFxFactor, blendTime);
}
