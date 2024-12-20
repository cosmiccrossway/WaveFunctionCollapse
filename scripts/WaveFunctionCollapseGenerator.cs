using System;
using System.Collections.Generic;
using System.Threading;
using Godot;
using JetBrains.Annotations;
using WaveFunctionCollapse.scripts.wave_function_collapse;
using Environment = System.Environment;
using Vector2I = Godot.Vector2I;

namespace WaveFunctionCollapse.scripts;

[GlobalClass]
public partial class WaveFunctionCollapseGenerator : Node {
	[Signal]
	public delegate void ChunkGeneratedEventHandler(Vector2I chunkPosition);
	[UsedImplicitly] [Export] public PackedScene[] InputLevels { get; set; }
	[UsedImplicitly] [Export] public PackedScene RotateMapping { get; set; }
	[UsedImplicitly] [Export] public PackedScene ReflectMapping { get; set; }
	[UsedImplicitly] [Export] public string PathToTileMapLayer { get; set; }
	[UsedImplicitly] [Export] public TileMapLayer OutputTileMapLayer { get; set; }
	[UsedImplicitly] [Export] public bool InfiniteGeneration { get; set; }
	[UsedImplicitly] [Export] public string Seed { get; set; }
	[UsedImplicitly] [Export] public int N { get; set; } = 2;
	[UsedImplicitly] [Export] public int Width { get; set; } = 66;
	[UsedImplicitly] [Export] public int Height { get; set; } = 66;
	[UsedImplicitly] [Export] public int XSpacing { get; set; } = 64;
	[UsedImplicitly] [Export] public int YSpacing { get; set; } = 64;
	[UsedImplicitly] [Export] public bool PeriodicInput { get; set; }
	[UsedImplicitly] [Export] public bool Periodic { get; set; }
	[UsedImplicitly] [Export] public int Symmetry { get; set; } = 8;
	[UsedImplicitly] [Export] public bool Ground { get; set; }
	[UsedImplicitly] [Export] public int Limit { get; set; }
	[UsedImplicitly] [Export] public Model.Heuristic Heuristic { get; set; } = Model.Heuristic.Entropy;
	[UsedImplicitly] [Export] public bool ShowPatterns { get; set; }

	private OverlappingModel _model;
	private FastNoiseLite _noise;
	private readonly Queue<Vector2I> _toGenerate;
	private bool _canGenerate = true;
	private bool _hasBeenGenerated;

	public WaveFunctionCollapseGenerator() {
		_toGenerate = new Queue<Vector2I>();
		var generateThread = new Thread(GenerateLoop);
		generateThread.Start();
		
		ChunkGenerated += chunkPosition => {
			_canGenerate = true;
			Console.WriteLine($"Chunk {chunkPosition} Generated");
		};
	}

	public override void _Ready() {
		Seed ??= Environment.TickCount.ToString();
		Console.WriteLine($"Seed: {Seed}, HashCode: {GetStableHashCode(Seed)}");
		_noise = new FastNoiseLite();
		_noise.Seed = GetStableHashCode(Seed);
	}

	public List<Vector2I> AttemptChunkGeneration(Vector2 position) {
		var chunkPosition = WorldPositionToChunkPosition(position);
		var chunksToGenerate = new List<Vector2I>(9);
		if (InfiniteGeneration) {
			for (var y = chunkPosition.Y - YSpacing; y <= chunkPosition.Y + YSpacing; y += YSpacing) {
				for (var x = chunkPosition.X - XSpacing; x <= chunkPosition.X + XSpacing; x += XSpacing) {
					var chunkToGenerate = new Vector2I(x, y);
					chunksToGenerate.Add(chunkToGenerate);
					if (!IsChunkGenerated(chunkToGenerate)) {
						Generate(chunkToGenerate);
					}
				}
			}
		} else if (!_hasBeenGenerated) {
			_hasBeenGenerated = true;
			chunksToGenerate.Add(chunkPosition);
			if (!IsChunkGenerated(chunkPosition)) {
				Generate(chunkPosition);
			}
		}

		return chunksToGenerate;
	}

	private bool IsChunkGenerated(Vector2I chunkPosition) {
		return _model != null && OutputTileMapLayer.GetCellAtlasCoords(new Vector2I(
			chunkPosition.X + (_model.Mx - XSpacing),
			chunkPosition.Y + (_model.My - YSpacing)
		)) != Vector2I.One * -1;
	}

	public Vector2I WorldPositionToChunkPosition(Vector2 position) {
		var tilePosition = WorldPositionToTilePosition(position);
		return TilePositionToChunkPosition(tilePosition);
	}

	public Vector2I WorldPositionToTilePosition(Vector2 position) {
		return OutputTileMapLayer.LocalToMap(position);
	}

	private Vector2I TilePositionToChunkPosition(Vector2I tilePosition) {
		return new Vector2I(
			tilePosition.X - MathUtil.Mod(tilePosition.X, XSpacing),
			tilePosition.Y - MathUtil.Mod(tilePosition.Y, YSpacing));
	}
	
	private void Generate(Vector2I? startPosition = null) {
		var position = startPosition ?? Vector2I.Zero;
		position = TilePositionToChunkPosition(position);
		if (MathUtil.Mod(position.X,XSpacing * 2) == 0 && MathUtil.Mod(position.Y,YSpacing * 2) == 0) {
			_toGenerate.Enqueue(position);
		} else if (position.X % (XSpacing * 2) == 0) {
			_toGenerate.Enqueue(new Vector2I(position.X, position.Y - YSpacing));
			_toGenerate.Enqueue(new Vector2I(position.X, position.Y + YSpacing));
			_toGenerate.Enqueue(position);
		} else if (position.Y % (YSpacing * 2) == 0) {
			_toGenerate.Enqueue(new Vector2I(position.X - XSpacing, position.Y));
			_toGenerate.Enqueue(new Vector2I(position.X + XSpacing, position.Y));
			_toGenerate.Enqueue(position);
		} else {
			_toGenerate.Enqueue(new Vector2I(position.X - XSpacing, position.Y - YSpacing));
			_toGenerate.Enqueue(new Vector2I(position.X + XSpacing, position.Y - YSpacing));
			_toGenerate.Enqueue(new Vector2I(position.X - XSpacing, position.Y + YSpacing));
			_toGenerate.Enqueue(new Vector2I(position.X + XSpacing, position.Y + YSpacing));
			_toGenerate.Enqueue(new Vector2I(position.X - XSpacing, position.Y));
			_toGenerate.Enqueue(new Vector2I(position.X + XSpacing, position.Y));
			_toGenerate.Enqueue(new Vector2I(position.X, position.Y - YSpacing));
			_toGenerate.Enqueue(new Vector2I(position.X, position.Y + YSpacing));
			_toGenerate.Enqueue(position);
		}
	}

	private void GenerateLoop() {
		while (true) {
			switch (_toGenerate.Count) {
				case > 0 when _canGenerate: {
					var chunkPosition = _toGenerate.Dequeue();
					if (!IsChunkGenerated(chunkPosition)) {
						_canGenerate = false;
						ThreadedGenerate(chunkPosition);
					}

					break;
				}
				case > 0:
					Thread.Sleep(20);
					break;
				default:
					Thread.Sleep(1000);
					break;
			}
		}
	}

	private void ThreadedGenerate(Vector2I startPosition) {

		_model ??= BuildModel();

		// Checking at this position avoids the overlapping boundaries
		if (!IsChunkGenerated(startPosition)) {
			_model.RegisterPreSetTiles(startPosition);
			if (!ShowPatterns) {
				var seedGenerator = new Random(_noise.GetNoise2D(startPosition.X, startPosition.Y).GetHashCode());
				for (var i = 0; i < 10; i++) {
					var seed = seedGenerator.Next();
					var success = _model.Run(seed, Limit);
					if (success) {
						_model.Save(startPosition);
						break;
					}

					Console.WriteLine($"Failed Attempt {i + 1}, Chunk: {startPosition}");
				}
			} else {
				_model.SavePatterns();
			}
		}

		CallDeferred("emit_signal", SignalName.ChunkGenerated, startPosition);
	}

	private static int GetStableHashCode(string str) {
		unchecked {
			var hash1 = 5381;
			var hash2 = hash1;

			for (var i = 0; i < str.Length && str[i] != '\0'; i += 2) {
				hash1 = ((hash1 << 5) + hash1) ^ str[i];
				if (i == str.Length - 1 || str[i + 1] == '\0')
					break;
				hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
			}

			return hash1 + (hash2 * 1566083941);
		}
	}

	private OverlappingModel BuildModel() {
		var inputTileMapLayers = new TileMapLayer[InputLevels.Length];
		for (int i = 0; i < InputLevels.Length; i++) {
			inputTileMapLayers[i] = GetTileMapLayerFromPackedScene(InputLevels[i], PathToTileMapLayer);
		}

		var rotateMapping = GetTileMapLayerFromPackedScene(RotateMapping, PathToTileMapLayer);
		var reflectMapping = GetTileMapLayerFromPackedScene(ReflectMapping, PathToTileMapLayer);
		
		var model = new OverlappingModel(inputTileMapLayers, rotateMapping, reflectMapping, OutputTileMapLayer, N,
			Width, Height, PeriodicInput, Periodic, Symmetry, Ground, Heuristic, XSpacing, YSpacing);
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
