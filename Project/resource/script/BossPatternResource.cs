using Godot;

//Data for movement
namespace Project.Gameplay.Bosses
{
	public partial class BossPatternResource : Resource
	{
		[Export]
		public int damage = 0; //How much damage is needed to trigger this phase?
		/// <summary>
		/// Each attack must be on it's own line.
		/// Example attack format:
		/// type="V", startup=".5", delay="0"
		/// Startup is how much anticipation to give the player.
		/// Delay is the time between attacks.
		/// Missing parameters will automatically be set to "0"
		/// </summary>
		[Export(PropertyHint.MultilineText)]
		public string attacks = string.Empty;
	}
}
