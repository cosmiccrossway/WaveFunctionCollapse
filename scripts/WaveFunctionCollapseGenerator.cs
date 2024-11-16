using System;
using System.Threading;
using Godot;
using JetBrains.Annotations;
using WaveFunctionCollapse.scripts.wave_function_collapse;

namespace WaveFunctionCollapse.scripts;

[GlobalClass]
public partial class WaveFunctionCollapseGenerator : Node {
	[Signal]
	public delegate void GeneratedEventHandler();
	[UsedImplicitly] [Export] public PackedScene[] InputLevels { get; set; }
	[UsedImplicitly] [Export] public PackedScene RotateMapping { get; set; }
	[UsedImplicitly] [Export] public PackedScene ReflectMapping { get; set; }
	[UsedImplicitly] [Export] public string PathToTileMapLayer { get; set; }
	[UsedImplicitly] [Export] public TileMapLayer OutputTileMapLayer { get; set; }
	[UsedImplicitly] [Export] public int N { get; set; } = 2;
	[UsedImplicitly] [Export] public Vector2I StartPosition { get; set; } = Vector2I.Zero;
	[UsedImplicitly] [Export] public int Width { get; set; } = 64;
	[UsedImplicitly] [Export] public int Height { get; set; } = 64;
	[UsedImplicitly] [Export] public bool PeriodicInput { get; set; } = true;
	[UsedImplicitly] [Export] public bool Periodic { get; set; }
	[UsedImplicitly] [Export] public int Symmetry { get; set; } = 8;
	[UsedImplicitly] [Export] public bool Ground { get; set; }
	[UsedImplicitly] [Export] public int Limit { get; set; }
	[UsedImplicitly] [Export] public Model.Heuristic Heuristic { get; set; } = Model.Heuristic.Entropy;
	[UsedImplicitly] [Export] public bool ShowPatterns { get; set; }
	
	private Thread _generateThread;
	private OverlappingModel _model;

	public void Generate() {
		_generateThread = new Thread(ThreadedGenerate);
		_generateThread.Start();
	}

	private void ThreadedGenerate() {
		Random random = new();

		_model ??= BuildModel();

		_model.RegisterPreSetTiles(StartPosition);
		if (!ShowPatterns) {
			for (var i = 0; i < 10; i++) {
				var seed = random.Next();
				var success = _model.Run(seed, Limit);
				if (success) {
					_model.Save(StartPosition);
					break;
				}

				Console.WriteLine($"Failed Attempt {i + 1}");
			}
		} else {
			_model.SavePatterns();
		}

		CallDeferred("emit_signal", SignalName.Generated);
	}

	private OverlappingModel BuildModel() {
		var inputTileMapLayers = new TileMapLayer[InputLevels.Length];
		for (int i = 0; i < InputLevels.Length; i++) {
			inputTileMapLayers[i] = GetTileMapLayerFromPackedScene(InputLevels[i], PathToTileMapLayer);
		}

		var rotateMapping = GetTileMapLayerFromPackedScene(RotateMapping, PathToTileMapLayer);
		var reflectMapping = GetTileMapLayerFromPackedScene(ReflectMapping, PathToTileMapLayer);
		
		var model = new OverlappingModel(inputTileMapLayers, rotateMapping, reflectMapping, OutputTileMapLayer, N,
			Width, Height, PeriodicInput, Periodic, Symmetry, Ground, Heuristic);
		return model;
	}

	private static TileMapLayer GetTileMapLayerFromPackedScene(PackedScene packedScene, string pathToTileMapLayer) {
		if (packedScene == null) {
			throw new ArgumentNullException(nameof(packedScene));
		}
		var scene = packedScene.Instantiate();
		
		if (scene?.GetNode(pathToTileMapLayer) is not TileMapLayer tileMapLayer) {
			throw new Exception($"TileMapLayer not found for {scene?.GetName()} with path {pathToTileMapLayer}.");
		}

		return tileMapLayer;
	}
}
