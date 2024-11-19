using Godot;
using JetBrains.Annotations;

namespace WaveFunctionCollapse.scripts;

public partial class Game : Node {
	[UsedImplicitly] [Export] public PackedScene OutputLevelScene { get; set; }

	private OutputLevel _outputLevel;
	private Player _player;
	private DebugScreen _debugScreen;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		_player = GetNode<Player>("Player");
		_debugScreen = GetNode<DebugScreen>("CanvasLayer/DebugScreen");
		GenerateLevel();
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) {
		if (Input.IsActionJustPressed("enter")) {
			GenerateLevel();
		}
		_outputLevel?.AttemptChunkGeneration(_player.Position);
	}

	private void GenerateLevel() {
		var newOutputLevel = OutputLevelScene.Instantiate() as OutputLevel;
		if (_outputLevel == null) {
			_outputLevel = newOutputLevel;
			_debugScreen.OutputLevel = _outputLevel;
		}

		AddChild(newOutputLevel);
		if (newOutputLevel != null) {
			newOutputLevel.AttemptChunkGeneration(_player.Position);
			newOutputLevel.LevelGenerated += () => {
				if (_outputLevel != newOutputLevel) {
					_outputLevel?.QueueFree();
					_outputLevel = newOutputLevel;
					_debugScreen.OutputLevel = _outputLevel;
				}
			};
		}
	}
}
