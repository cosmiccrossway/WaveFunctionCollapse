using Godot;

namespace WaveFunctionCollapse.scripts;

[GlobalClass]
public partial class OutputLevel : Node2D {
	[Signal]
	public delegate void LevelGeneratedEventHandler();

	private WaveFunctionCollapseGenerator _wfcGenerator;
	private bool _firstAttemptedChunkGeneration = true;
	
	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		_wfcGenerator = GetNode<WaveFunctionCollapseGenerator>("WaveFunctionCollapseGenerator");
	}

	public void AttemptChunkGeneration(Vector2 position) {
		var positionForGeneration = _wfcGenerator.InfiniteGeneration ? position : Vector2.Zero;
		var chunksToGenerate = _wfcGenerator.AttemptChunkGeneration(positionForGeneration);
		if (_firstAttemptedChunkGeneration) {
			_firstAttemptedChunkGeneration = false;
			
			WaveFunctionCollapseGenerator.ChunkGeneratedEventHandler handler = null;
			handler = chunkPosition => {
				if (chunksToGenerate.Contains(chunkPosition)) {
					chunksToGenerate.Remove(chunkPosition);

					if (chunksToGenerate.Count == 0) {
						EmitSignal(SignalName.LevelGenerated);
						_wfcGenerator.ChunkGenerated -= handler;
					}
				}
			};
			_wfcGenerator.ChunkGenerated += handler;
		}
	}

	public Vector2 GetTilePosition(Vector2 position) {
		return _wfcGenerator.WorldPositionToTilePosition(position);
	}

	public Vector2 GetChunkPosition(Vector2 position) {
		return _wfcGenerator.WorldPositionToChunkPosition(position);
	}

	public string GetSeed() {
		return _wfcGenerator.Seed;
	}
}
