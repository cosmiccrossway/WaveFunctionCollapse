// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;
using System.Collections.Generic;
using Godot;

namespace WaveFunctionCollapse.scripts.wave_function_collapse;

class OverlappingModel : Model {
    List<byte[]> patterns;
    List<Vector2I> colors;

    private TileMapLayer _outputGroundLayer;

    public OverlappingModel(Node2D inputLevel, Node2D outputLevel, int N, int width, int height, bool periodicInput,
        bool periodic,
        int symmetry, bool ground, Heuristic heuristic,
        Godot.Collections.Dictionary<Vector2I, Vector2I> rotateDictionary,
        Godot.Collections.Dictionary<Vector2I, Vector2I> reflectDictionary) : base(width, height, N, periodic,
        heuristic) {
        var groundLayer = inputLevel.FindChild("TileMapLayers").FindChild("Ground") as TileMapLayer;
        var dimensions = groundLayer.GetUsedRect();
        var SX = dimensions.Size.X;
        var SY = dimensions.Size.Y;

        _outputGroundLayer = outputLevel.FindChild("TileMapLayers").FindChild("Ground") as TileMapLayer;

        byte[] sample = new byte[SX * SY];
        colors = new List<Vector2I>();
        for (int y = 0; y < SY; y++)
            for (int x = 0; x < SX; x++) {
                Vector2I color = groundLayer.GetCellAtlasCoords(new Vector2I(x, y));
                var i = colors.FindIndex(c => c == color);
                if (i == -1) {
                    colors.Add(color);
                    i = colors.Count - 1;
                }

                sample[x + y * SX] = (byte)i;
            }

        static byte[] pattern(Func<int, int, byte> f, int N) {
            byte[] result = new byte[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                    result[x + y * N] = f(x, y);
            return result;
        }

        static long hash(byte[] p, int C) {
            long result = 0, power = 1;
            for (int i = 0; i < p.Length; i++) {
                result += p[p.Length - 1 - i] * power;
                power *= C;
            }

            return result;
        }

        patterns = new();
        Dictionary<long, int> patternIndices = new();
        List<double> weightList = new();

        int C = colors.Count;

        var rotateColors = new Dictionary<byte, byte>();
        var reflectColors = new Dictionary<byte, byte>();

        for (int i = 0; i < colors.Count; i++) {
            var atlasCoordsKey = colors[i];
            for (int j = 0; j < colors.Count; j++) {
                if (colors[j] == rotateDictionary[atlasCoordsKey]) {
                    rotateColors.Add((byte)i, (byte)j);
                }

                if (colors[j] == reflectDictionary[atlasCoordsKey]) {
                    reflectColors.Add((byte)i, (byte)j);
                }
            }
        }

        byte[] rotate(byte[] p, int N) => pattern((x, y) => rotateColors.GetValueOrDefault(p[N - 1 - y + x * N]), N);
        byte[] reflect(byte[] p, int N) => pattern((x, y) => reflectColors.GetValueOrDefault(p[N - 1 - x + y * N]), N);

        int xmax = periodicInput ? SX : SX - N + 1;
        int ymax = periodicInput ? SY : SY - N + 1;
        for (int y = 0; y < ymax; y++)
            for (int x = 0; x < xmax; x++) {
                byte[][] ps = new byte[8][];

                ps[0] = pattern((dx, dy) => sample[(x + dx) % SX + (y + dy) % SY * SX], N);
                ps[1] = reflect(ps[0], N);
                ps[2] = rotate(ps[0], N);
                ps[3] = reflect(ps[2], N);
                ps[4] = rotate(ps[2], N);
                ps[5] = reflect(ps[4], N);
                ps[6] = rotate(ps[4], N);
                ps[7] = reflect(ps[6], N);

                for (int k = 0; k < symmetry; k++) {
                    byte[] p = ps[k];
                    long h = hash(p, C);
                    if (patternIndices.TryGetValue(h, out int index)) weightList[index] = weightList[index] + 1;
                    else {
                        patternIndices.Add(h, weightList.Count);
                        weightList.Add(1.0);
                        patterns.Add(p);
                    }
                }
            }

        weights = weightList.ToArray();
        T = weights.Length;
        this.ground = ground;

        static bool agrees(byte[] p1, byte[] p2, int dx, int dy, int N) {
            int xmin = dx < 0 ? 0 : dx, xmax = dx < 0 ? dx + N : N, ymin = dy < 0 ? 0 : dy, ymax = dy < 0 ? dy + N : N;
            for (int y = ymin; y < ymax; y++)
                for (int x = xmin; x < xmax; x++)
                    if (p1[x + N * y] != p2[x - dx + N * (y - dy)])
                        return false;
            return true;
        }

        ;

        propagator = new int[4][][];
        for (var d = 0; d < 4; d++) {
            propagator[d] = new int[T][];
            for (var t = 0; t < T; t++) {
                List<int> list = new();
                for (var t2 = 0; t2 < T; t2++)
                    if (agrees(patterns[t], patterns[t2], dx[d], dy[d], N))
                        list.Add(t2);
                propagator[d][t] = new int[list.Count];
                for (var c = 0; c < list.Count; c++) propagator[d][t][c] = list[c];
            }
        }
    }

    public override void Save() {
        if (observed[0] >= 0) {
            for (var y = 0; y < MY; y++) {
                var dy = y < MY - N + 1 ? 0 : N - 1;
                for (var x = 0; x < MX; x++) {
                    var dx = x < MX - N + 1 ? 0 : N - 1;
                    var c = colors[patterns[observed[x - dx + (y - dy) * MX]][dx + dy * N]];
                    _outputGroundLayer.SetCell(new Vector2I(x, y), _outputGroundLayer.TileSet.GetSourceId(0), c, 0);
                }
            }
        }
    }
}