using Godot;
using JetBrains.Annotations;

namespace WaveFunctionCollapse.scripts;

[GlobalClass]
public partial class DebugScreen : Label {

	[UsedImplicitly][Export] public Player Player { get; set; }
	public OutputLevel OutputLevel { get; set; }

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
		if (Input.IsActionJustPressed("debug_screen")) {
			Visible = !Visible;
		}
		Text = $"Seed: {OutputLevel.GetSeed() ?? ""}\n" + 
			   $"Player Position: {Player.Position}\n" +
			   $"Tile Position: {OutputLevel?.GetTilePosition(Player.Position).ToString() ?? "None"}\n" +
			   $"Chunk Position: {OutputLevel?.GetChunkPosition(Player.Position).ToString() ?? "None"}";
	}
}
