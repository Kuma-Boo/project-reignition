using Godot;

namespace Project.Gameplay
{
	/// <summary>
	/// Static stage settings. Unlike LevelSettings, this data gets shared between all levels that use the same Static area.
	/// </summary>
	public partial class StageSettings : Node3D
	{
		public static StageSettings instance;

		/// <summary> Pathfollower automatically assigns this path when level starts. </summary>
		[Export]
		public Path3D mainPath;
		/// <summary> Returns the position of a given position, from [0 <-> 1]. </summary>
		public float GetProgress(Vector3 pos) => mainPath.Curve.GetClosestOffset(pos - mainPath.GlobalPosition);

		[Export]
		public Node3D completionDemoNode;
		/// <summary> Camera demo that gets enabled after the level is cleared. </summary>
		[Export]
		public AnimationPlayer completionDemoAnimator;

		[Export]
		public SFXLibraryResource dialogLibrary;

		public override void _EnterTree()
		{
			instance = this; //Always override previous instance
		}

		public bool StartCompletionDemo()
		{
			if (completionDemoNode == null || completionDemoAnimator == null) return false;

			OnCameraDemoAdvance();

			completionDemoAnimator.Connect(AnimationPlayer.SignalName.AnimationFinished, new Callable(this, MethodName.OnCameraDemoAdvance));
			completionDemoAnimator.Play("demo1");
			return true;
		}

		/// <summary> Completion demo advanced, play a crossfade </summary>
		public void OnCameraDemoAdvance() => CharacterController.instance.Camera.StartCrossfade();
	}
}