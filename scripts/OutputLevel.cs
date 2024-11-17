using System;
using Godot;

namespace WaveFunctionCollapse.scripts;

[GlobalClass]
public partial class OutputLevel : Node2D {
	[Signal]
	public delegate void LevelGeneratedEventHandler();
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		var wfcGenerator = GetNode<WaveFunctionCollapseGenerator>("WaveFunctionCollapseGenerator");
		wfcGenerator.Generate(new Vector2I(68, 69));
		wfcGenerator.Generated += () => {
			EmitSignal(SignalName.LevelGenerated);
		};
	}
}
