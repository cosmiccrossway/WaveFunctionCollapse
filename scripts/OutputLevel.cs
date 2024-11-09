using System;
using System.Collections.Generic;
using Godot;
using WaveFunctionCollapse.scripts.wave_function_collapse;

namespace WaveFunctionCollapse.scripts;

public partial class OutputLevel : Node2D {
	[Export] public PackedScene InputLevel { get; set; }
	[Export] public int N { get; set; } = 2;
	[Export] public int Width { get; set; } = 64;
	[Export] public int Height { get; set; } = 64;
	[Export] public bool PeriodicInput { get; set; } = true;
	[Export] public bool Periodic { get; set; } = false;
	[Export] public int Symmetry { get; set; } = 8;
	[Export] public bool Ground { get; set; } = false;
	[Export] public int Limit { get; set; } = 0;
	[Export] public string HeuristicString { get; set; } = "Entropy";
	[Export] public Godot.Collections.Dictionary<Vector2I, Vector2I> RotateDictionary { get; set; }
	[Export] public Godot.Collections.Dictionary<Vector2I, Vector2I> ReflectDictionary { get; set; }

	// Called when the node enters the scene tree for the first time.
	public override void _Ready() {
		Random random = new();
		var input = InputLevel.Instantiate() as Node2D;
		var heuristic = HeuristicString == "Scanline"
			? Model.Heuristic.Scanline
			: (HeuristicString == "MRV" ? Model.Heuristic.MRV : Model.Heuristic.Entropy);
		var model = new OverlappingModel(input, this, N, Width, Height, PeriodicInput, Periodic, Symmetry, Ground,
			heuristic, RotateDictionary, ReflectDictionary);
		for (var i = 0; i < 10; i++) {
			var seed = random.Next();
			var success = model.Run(seed, Limit);
			if (success) {
				model.Save();
				break;
			}
		}
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta) { }
}
