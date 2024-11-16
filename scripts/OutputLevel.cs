using Godot;

namespace WaveFunctionCollapse.scripts;

[GlobalClass]
public partial class OutputLevel : Node2D {
	[Signal]
	public delegate void LevelGeneratedEventHandler();
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		var wfcGenerator = GetNode<WaveFunctionCollapseGenerator>("WaveFunctionCollapseGenerator");
		wfcGenerator.Generate();
		wfcGenerator.Generated += () => {
			if (wfcGenerator.StartPosition.X == 0) {
				wfcGenerator.StartPosition = new Vector2I(32, 0);
				wfcGenerator.Generate();
			} else if (wfcGenerator.StartPosition.X == 32) {
				wfcGenerator.StartPosition = new Vector2I(64, 0);
				wfcGenerator.Generate();
			} else {
				EmitSignal(SignalName.LevelGenerated);
			}
		};
	}
}
