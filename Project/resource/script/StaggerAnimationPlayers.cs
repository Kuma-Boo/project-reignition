using Godot;
using Project.Gameplay;

public partial class StaggerAnimationPlayers : Node
{
  [Export]
  public AnimationPlayer[] AnimationPlayers = [];
  [Export]
  public float StaggerTime = 1;
  [Export]
  public string AnimationName = "";
  private Tween _timeline;
  public override void _EnterTree()
  {
    StageSettings.Instance.ConnectRespawnSignal(this);
  }
  public override void _Ready()
  {
    _timeline = GetTree().CreateTween();
    _timeline.Pause();
    for (var i = 0; i < AnimationPlayers.Length; i++)
    {
      if (i > 0)
      {
        _timeline.TweenInterval(StaggerTime);
      }
      _timeline.TweenCallback(GetAnimation(i));
    }
  }
  private Callable GetAnimation(int idx)
  {
    return Callable.From(() =>
    {
      AnimationPlayers[idx].Play(AnimationName);
    });
  }
  public void Run()
  {
    _timeline.Play();
  }
  public void Reset()
  {
    _timeline.Stop();
  }
  public void Rerun()
  {
    Reset();
    Run();
  }
  public void Respawn()
  {
    Reset();
  }
}