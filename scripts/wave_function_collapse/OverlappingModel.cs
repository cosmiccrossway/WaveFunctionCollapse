// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;
using System.Collections.Generic;
using Godot;

namespace WaveFunctionCollapse.scripts.wave_function_collapse;

internal class OverlappingModel : Model {
    private readonly List<byte[]> _patterns;
    private readonly List<Vector2I> _colors;
    
    private readonly Dictionary<byte, byte> _rotateColors = new Dictionary<byte, byte>();
    private readonly Dictionary<byte, byte> _reflectColors = new Dictionary<byte, byte>();

    private readonly TileMapLayer _outputGroundLayer;

    public OverlappingModel(Node2D inputLevel, Node2D outputLevel, int n, int width, int height, bool periodicInput,
        bool periodic, int symmetry, bool ground, Heuristic heuristic,
        Godot.Collections.Dictionary<Vector2I, Vector2I> rotateDictionary,
        Godot.Collections.Dictionary<Vector2I, Vector2I> reflectDictionary)
        : base(width, height, n, periodic, heuristic) {
        
        if (inputLevel.FindChild("TileMapLayers").FindChild("Ground") is not TileMapLayer groundLayer) {
            throw new Exception("Ground layer not found");
        }
        var dimensions = groundLayer.GetUsedRect();
        var sx = dimensions.Size.X;
        var sy = dimensions.Size.Y;

        _outputGroundLayer = outputLevel.FindChild("TileMapLayers").FindChild("Ground") as TileMapLayer;

        byte[] sample = new byte[sx * sy];
        _colors = new List<Vector2I>();
        for (var y = 0; y < sy; y++) {
            for (var x = 0; x < sx; x++) {
                Vector2I color = groundLayer.GetCellAtlasCoords(new Vector2I(x, y));
                var i = _colors.FindIndex(c => c == color);
                if (i == -1) {
                    _colors.Add(color);
                    i = _colors.Count - 1;
                }

                sample[x + y * sx] = (byte)i;
            }
        }

        _patterns = new List<byte[]>();
        Dictionary<long, int> patternIndices = new();
        List<double> weightList = new();

        var colorsCount = _colors.Count;

        for (var i = 0; i < _colors.Count; i++) {
            var atlasCoordsKey = _colors[i];
            for (int j = 0; j < _colors.Count; j++) {
                if (_colors[j] == rotateDictionary[atlasCoordsKey]) {
                    _rotateColors.Add((byte)i, (byte)j);
                }

                if (_colors[j] == reflectDictionary[atlasCoordsKey]) {
                    _reflectColors.Add((byte)i, (byte)j);
                }
            }
        }

        var xMax = periodicInput ? sx : sx - n + 1;
        var yMax = periodicInput ? sy : sy - n + 1;
        for (var y = 0; y < yMax; y++)
            for (var x = 0; x < xMax; x++) {
                var ps = new byte[8][];

                var x1 = x;
                var y1 = y;
                ps[0] = Pattern((dx, dy) => sample[(x1 + dx) % sx + (y1 + dy) % sy * sx], n);
                ps[1] = Reflect(ps[0], n);
                ps[2] = Rotate(ps[0], n);
                ps[3] = Reflect(ps[2], n);
                ps[4] = Rotate(ps[2], n);
                ps[5] = Reflect(ps[4], n);
                ps[6] = Rotate(ps[4], n);
                ps[7] = Reflect(ps[6], n);

                for (var k = 0; k < symmetry; k++) {
                    var p = ps[k];
                    var h = Hash(p, colorsCount);
                    if (patternIndices.TryGetValue(h, out var index)) {
                        weightList[index] += 1;
                    } else {
                        patternIndices.Add(h, weightList.Count);
                        weightList.Add(1.0);
                        _patterns.Add(p);
                    }
                }
            }

        Weights = weightList.ToArray();
        T = Weights.Length;
        this.Ground = ground;

        Propagator = new int[4][][];
        for (var d = 0; d < 4; d++) {
            Propagator[d] = new int[T][];
            for (var t = 0; t < T; t++) {
                List<int> list = new();
                for (var t2 = 0; t2 < T; t2++)
                    if (Agrees(_patterns[t], _patterns[t2], Dx[d], Dy[d], n))
                        list.Add(t2);
                Propagator[d][t] = new int[list.Count];
                for (var c = 0; c < list.Count; c++) Propagator[d][t][c] = list[c];
            }
        }
    }

    public override void Save() {
        if (Observed[0] >= 0) {
            for (var y = 0; y < My; y++) {
                var dy = y < My - N + 1 ? 0 : N - 1;
                for (var x = 0; x < Mx; x++) {
                    var dx = x < Mx - N + 1 ? 0 : N - 1;
                    var c = _colors[_patterns[Observed[x - dx + (y - dy) * Mx]][dx + dy * N]];
                    _outputGroundLayer.SetCell(new Vector2I(x, y), _outputGroundLayer.TileSet.GetSourceId(0), c);
                }
            }
        }
    }
    
    private static byte[] Pattern(Func<int, int, byte> f, int n) {
        var result = new byte[n * n];
        for (var y = 0; y < n; y++)
            for (var x = 0; x < n; x++)
                result[x + y * n] = f(x, y);
        return result;
    }

    private static long Hash(byte[] p, int c) {
        long result = 0, power = 1;
        for (var i = 0; i < p.Length; i++) {
            result += p[p.Length - 1 - i] * power;
            power *= c;
        }

        return result;
    }

    private byte[] Rotate(byte[] p, int n) {
        return Pattern((x, y) => _rotateColors.GetValueOrDefault(p[n - 1 - y + x * n]), n);
    }

    private byte[] Reflect(byte[] p, int n) {
        return Pattern((x, y) => _reflectColors.GetValueOrDefault(p[n - 1 - x + y * n]), n);
    }

    private static bool Agrees(byte[] p1, byte[] p2, int dx, int dy, int n) {
        var xMin = dx < 0 ? 0 : dx;
        var xMax = dx < 0 ? dx + n : n;
        var yMin = dy < 0 ? 0 : dy;
        var yMax = dy < 0 ? dy + n : n;
        for (var y = yMin; y < yMax; y++)
            for (var x = xMin; x < xMax; x++)
                if (p1[x + n * y] != p2[x - dx + n * (y - dy)])
                    return false;
        return true;
    }
}