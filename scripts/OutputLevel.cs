using System;
using Godot;
using JetBrains.Annotations;
using WaveFunctionCollapse.scripts.wave_function_collapse;

namespace WaveFunctionCollapse.scripts;

public partial class OutputLevel : Node2D {
    [UsedImplicitly] [Export] public PackedScene InputLevel { get; set; }
    [UsedImplicitly] [Export] public int N { get; set; } = 2;
    [UsedImplicitly] [Export] public int Width { get; set; } = 64;
    [UsedImplicitly] [Export] public int Height { get; set; } = 64;
    [UsedImplicitly] [Export] public bool PeriodicInput { get; set; } = true;
    [UsedImplicitly] [Export] public bool Periodic { get; set; }
    [UsedImplicitly] [Export] public int Symmetry { get; set; } = 8;
    [UsedImplicitly] [Export] public bool Ground { get; set; }
    [UsedImplicitly] [Export] public int Limit { get; set; }
    [UsedImplicitly] [Export] public string HeuristicString { get; set; } = "Entropy";
    [UsedImplicitly] [Export] public Godot.Collections.Dictionary<Vector2I, Vector2I> RotateDictionary { get; set; }
    [UsedImplicitly] [Export] public Godot.Collections.Dictionary<Vector2I, Vector2I> ReflectDictionary { get; set; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        Random random = new();
        var input = InputLevel.Instantiate() as Node2D;
        var heuristic = HeuristicString == "Scanline"
            ? Model.Heuristic.Scanline
            : (HeuristicString == "MRV" ? Model.Heuristic.Mrv : Model.Heuristic.Entropy);
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