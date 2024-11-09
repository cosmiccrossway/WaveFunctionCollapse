using Godot;

namespace WaveFunctionCollapse.scripts;

public partial class Game : Node {
	[Export] public PackedScene OutputLevelScene { get; set; }
	
	private OutputLevel _outputLevel = null;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		GenerateLevel();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
		if (Input.IsActionJustPressed("enter")) {
			GenerateLevel();
		}
	}

	private void GenerateLevel() {
		_outputLevel?.QueueFree();
		_outputLevel = OutputLevelScene.Instantiate() as OutputLevel;
		AddChild(_outputLevel);
	}
}
