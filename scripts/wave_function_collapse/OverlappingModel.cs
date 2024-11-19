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
    private readonly Dictionary<Vector2I, byte> _presetColors = new Dictionary<Vector2I, byte>();

    private readonly TileMapLayer _outputTileMapLayer;

    private readonly int _xSpacing;
    private readonly int _ySpacing;

    public OverlappingModel(TileMapLayer[] inputTileMapLayers, TileMapLayer rotateMapping, TileMapLayer reflectMapping, 
        TileMapLayer outputTileMapLayer, int n, int width, int height, bool periodicInput,
        bool periodic, int symmetry, bool ground, Heuristic heuristic, int xSpacing, int ySpacing)
        : base(width, height, n, periodic, heuristic) {
        
        _xSpacing = xSpacing;
        _ySpacing = ySpacing;

        var sample = new byte[inputTileMapLayers.Length][];
        _outputTileMapLayer = outputTileMapLayer;
        _colors = new List<Vector2I>();
        
        for (var inputReference = 0; inputReference < inputTileMapLayers.Length; inputReference++) {
            var inputTileMapLayer = inputTileMapLayers[inputReference];
            var dimensions = inputTileMapLayer.GetUsedRect();
            var sx = dimensions.Size.X;
            var sy = dimensions.Size.Y;
            
            sample[inputReference] = new byte[sx * sy];
            for (var y = 0; y < sy; y++) {
                for (var x = 0; x < sx; x++) {
                    Vector2I color = inputTileMapLayer.GetCellAtlasCoords(new Vector2I(x, y));
                    var i = _colors.FindIndex(c => c == color);
                    if (i == -1) {
                        _colors.Add(color);
                        i = _colors.Count - 1;
                    }

                    sample[inputReference][x + y * sx] = (byte)i;
                }
            }
        }

        _patterns = new List<byte[]>();
        Dictionary<long, int> patternIndices = new();
        List<double> weightList = new();

        var colorsCount = _colors.Count;
        
        var rotateDictionary = BuildDictionaryFromMapping(rotateMapping);
        var reflectDictionary = BuildDictionaryFromMapping(reflectMapping);
        for (var i = 0; i < _colors.Count; i++) {
            var atlasCoordsKey = _colors[i];
            for (int j = 0; j < _colors.Count; j++) {
                if (rotateDictionary.ContainsKey(atlasCoordsKey) && _colors[j] == rotateDictionary[atlasCoordsKey]) {
                    _rotateColors.Add((byte)i, (byte)j);
                }

                if (reflectDictionary.ContainsKey(atlasCoordsKey) && _colors[j] == reflectDictionary[atlasCoordsKey]) {
                    _reflectColors.Add((byte)i, (byte)j);
                }
            }
        }

        for (var inputReference = 0; inputReference < inputTileMapLayers.Length; inputReference++) {
            var inputTileMapLayer = inputTileMapLayers[inputReference];
            var dimensions = inputTileMapLayer.GetUsedRect();
            var sx = dimensions.Size.X;
            var sy = dimensions.Size.Y;
            var xMax = periodicInput ? sx : sx - n + 1;
            var yMax = periodicInput ? sy : sy - n + 1;
            for (var y = 0; y < yMax; y++) {
                for (var x = 0; x < xMax; x++) {
                    var ps = new byte[8][];

                    var x1 = x;
                    var y1 = y;
                    var inputRef = inputReference;
                    ps[0] = Pattern((dx, dy) => sample[inputRef][(x1 + dx) % sx + (y1 + dy) % sy * sx], n);
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

    public void RegisterPreSetTiles(Vector2I startPosition) {
        _presetColors.Clear();
        for (var y = 0; y < My; y++) {
            for (var x = 0; x < Mx; x++) {
                var location = new Vector2I(x, y);
                var offsetLocation = new Vector2I(x + startPosition.X, y + startPosition.Y);
                var color = _outputTileMapLayer.GetCellAtlasCoords(offsetLocation);
                if (color != new Vector2I(-1, -1)) {
                    _presetColors[location] = (byte)_colors.FindIndex(c => c == color);
                }
            }
        }
    }

    private static Dictionary<Vector2I, Vector2I> BuildDictionaryFromMapping(TileMapLayer mapping) {
        var dictionary = new Dictionary<Vector2I, Vector2I>();
        var rect = mapping.GetUsedRect();
        for (var y = rect.Position.Y; y < rect.Position.Y + rect.Size.Y; y++) {
            for (var x = rect.Position.X; x < rect.Position.X + rect.Size.X; x++) {
                var atlasCoordsForKey = mapping.GetCellAtlasCoords(new Vector2I(x, y));
                if (atlasCoordsForKey == new Vector2I(-1, -1)) continue;
                
                var atlasCoordsForValue = mapping.GetCellAtlasCoords(new Vector2I(x + 1, y));
                if (atlasCoordsForValue == new Vector2I(-1, -1)) continue;
                    
                dictionary.Add(atlasCoordsForKey, atlasCoordsForValue);
                x++; //Increment x to skip past the value of the key value pair
            }
        }

        return dictionary;
    }

    protected override void PreBan() {
        foreach (var (x, y) in _presetColors.Keys) {
            var color = _presetColors[new Vector2I(x, y)];
            for (var x1 = x - N + 1; x1 <= x; x1++) {
                if (x1 < 0 || x1 > Mx - N) continue;
                for (var y1 = y - N + 1; y1 <= y; y1++) {
                    if (y1 < 0 || y1 > My - N) continue;
                    var nodePosition = x1 + y1 * Mx;
                    
                    var patternX = x - x1;
                    var patternY = y - y1;
                    var patternPosition = patternX + patternY * N;

                    for (var patternReference = 0; patternReference < T; patternReference++) {
                        if (_patterns[patternReference][patternPosition] != color) {
                            if (Wave[nodePosition][patternReference]) {
                                Ban(nodePosition, patternReference);
                            }
                        }
                    }
                }
            }
        }
    }

    public override void Save(Vector2I startPosition) {
        if (Observed[0] >= 0) {
            for (var y = 0; y < My; y++) {
                var dy = y < My - N + 1 ? 0 : N - 1;
                for (var x = 0; x < Mx; x++) {
                    if (y < N - 1 || y >= My - (N - 1) || x < N - 1 || x >= Mx - (N - 1)) {
                        var xMod = MathUtil.Mod(startPosition.X, _xSpacing * 2) == 0;
                        var yMod = MathUtil.Mod(startPosition.Y, _ySpacing * 2) == 0;
                        if (!(xMod && yMod) && (xMod || yMod)) {
                            continue;
                        }
                    }
                    var dx = x < Mx - N + 1 ? 0 : N - 1;
                    
                    var c = _colors[_patterns[Observed[x - dx + (y - dy) * Mx]][dx + dy * N]];
                    _outputTileMapLayer.CallDeferred("set_cell", new Vector2I(x + startPosition.X, y + startPosition.Y), _outputTileMapLayer.TileSet.GetSourceId(0), c);
                }
            }
        }
    }

    public void SavePatterns() {
        var x = 0;
        var y = 0;

        var sourceId = _outputTileMapLayer.TileSet.GetSourceId(0);
        for (var patternRef = 0; patternRef < T; patternRef++) {
            var pattern = _patterns[patternRef];

            for (var i = 0; i < N * N; i++) {
                var x1 = x + (i % N);
                var y1 = y + (i / N);
                _outputTileMapLayer.SetCell(new Vector2I(x1, y1), sourceId, _colors[pattern[i]]);
            }

            if (x < (N + 1) * 5) {
                x += N + 1;
            } else {
                x = 0;
                y += N + 1;
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
        return Pattern((x, y) => {
            var color = p[n - 1 - y + x * n];
            return _rotateColors.GetValueOrDefault(color, color);
        }, n);
    }

    private byte[] Reflect(byte[] p, int n) {
        return Pattern((x, y) => {
            var color = p[n - 1 - x + y * n];
            return _reflectColors.GetValueOrDefault(color, color);
        }, n);
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