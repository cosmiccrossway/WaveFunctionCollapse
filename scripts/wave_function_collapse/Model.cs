// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;

namespace WaveFunctionCollapse.scripts.wave_function_collapse;

abstract class Model {
    private bool[][] _wave;

    protected int[][][] Propagator;
    private int[][][] _compatible;
    protected int[] Observed;

    private (int, int)[] _stack;
    private int _stackSize, _observedSoFar;

    protected readonly int Mx;
    protected readonly int My;
    protected int T;
    protected readonly int N;
    private readonly bool _periodic;
    protected bool Ground;

    protected double[] Weights;
    private double[] _weightLogWeights, _distribution;

    private int[] _sumsOfOnes;
    private double _sumOfWeights, _sumOfWeightLogWeights, _startingEntropy;
    private double[] _sumsOfWeights;
    private double[] _sumsOfWeightLogWeights;
    private double[] _entropies;

    public enum Heuristic {
        Entropy,
        Mrv,
        Scanline
    };

    private readonly Heuristic _heuristic;

    protected Model(int width, int height, int n, bool periodic, Heuristic heuristic) {
        Mx = width;
        My = height;
        this.N = n;
        this._periodic = periodic;
        this._heuristic = heuristic;
    }

    void Init() {
        _wave = new bool[Mx * My][];
        _compatible = new int[_wave.Length][][];
        for (int i = 0; i < _wave.Length; i++) {
            _wave[i] = new bool[T];
            _compatible[i] = new int[T][];
            for (int t = 0; t < T; t++) _compatible[i][t] = new int[4];
        }

        _distribution = new double[T];
        Observed = new int[Mx * My];

        _weightLogWeights = new double[T];
        _sumOfWeights = 0;
        _sumOfWeightLogWeights = 0;

        for (int t = 0; t < T; t++) {
            _weightLogWeights[t] = Weights[t] * Math.Log(Weights[t]);
            _sumOfWeights += Weights[t];
            _sumOfWeightLogWeights += _weightLogWeights[t];
        }

        _startingEntropy = Math.Log(_sumOfWeights) - _sumOfWeightLogWeights / _sumOfWeights;

        _sumsOfOnes = new int[Mx * My];
        _sumsOfWeights = new double[Mx * My];
        _sumsOfWeightLogWeights = new double[Mx * My];
        _entropies = new double[Mx * My];

        _stack = new (int, int)[_wave.Length * T];
        _stackSize = 0;
    }

    public bool Run(int seed, int limit) {
        if (_wave == null) Init();
        
        if (_wave == null) throw new Exception("Model Wave has not been initialized");

        Clear();
        Random random = new(seed);

        for (int l = 0; l < limit || limit <= 0; l++) {
            int node = NextUnobservedNode(random);
            if (node >= 0) {
                Observe(node, random);
                bool success = Propagate();
                if (!success) return false;
            } else {
                for (var i = 0; i < _wave.Length; i++)
                    for (var t = 0; t < T; t++)
                        if (_wave[i][t]) {
                            Observed[i] = t;
                            break;
                        }

                return true;
            }
        }

        return true;
    }

    int NextUnobservedNode(Random random) {
        if (_heuristic == Heuristic.Scanline) {
            for (int i = _observedSoFar; i < _wave.Length; i++) {
                if (!_periodic && (i % Mx + N > Mx || i / Mx + N > My)) continue;
                if (_sumsOfOnes[i] > 1) {
                    _observedSoFar = i + 1;
                    return i;
                }
            }

            return -1;
        }

        double min = 1E+4;
        int argMin = -1;
        for (int i = 0; i < _wave.Length; i++) {
            if (!_periodic && (i % Mx + N > Mx || i / Mx + N > My)) continue;
            int remainingValues = _sumsOfOnes[i];
            double entropy = _heuristic == Heuristic.Entropy ? _entropies[i] : remainingValues;
            if (remainingValues > 1 && entropy <= min) {
                double noise = 1E-6 * random.NextDouble();
                if (entropy + noise < min) {
                    min = entropy + noise;
                    argMin = i;
                }
            }
        }

        return argMin;
    }

    void Observe(int node, Random random) {
        var w = _wave[node];
        for (var t = 0; t < T; t++) {
            _distribution[t] = w[t] ? Weights[t] : 0.0;
        }
        var r = _distribution.Random(random.NextDouble());
        for (var t = 0; t < T; t++)
            if (w[t] != (t == r))
                Ban(node, t);
    }

    bool Propagate() {
        while (_stackSize > 0) {
            (int i1, int t1) = _stack[_stackSize - 1];
            _stackSize--;

            int x1 = i1 % Mx;
            int y1 = i1 / Mx;

            for (int d = 0; d < 4; d++) {
                int x2 = x1 + Dx[d];
                int y2 = y1 + Dy[d];
                if (!_periodic && (x2 < 0 || y2 < 0 || x2 + N > Mx || y2 + N > My)) continue;

                if (x2 < 0) x2 += Mx;
                else if (x2 >= Mx) x2 -= Mx;
                if (y2 < 0) y2 += My;
                else if (y2 >= My) y2 -= My;

                int i2 = x2 + y2 * Mx;
                int[] p = Propagator[d][t1];
                int[][] compat = _compatible[i2];

                foreach (var t2 in p) {
                    int[] comp = compat[t2];

                    comp[d]--;
                    if (comp[d] == 0) Ban(i2, t2);
                }
            }
        }

        return _sumsOfOnes[0] > 0;
    }

    void Ban(int i, int t) {
        _wave[i][t] = false;

        int[] comp = _compatible[i][t];
        for (int d = 0; d < 4; d++) comp[d] = 0;
        _stack[_stackSize] = (i, t);
        _stackSize++;

        _sumsOfOnes[i] -= 1;
        _sumsOfWeights[i] -= Weights[t];
        _sumsOfWeightLogWeights[i] -= _weightLogWeights[t];

        double sum = _sumsOfWeights[i];
        _entropies[i] = Math.Log(sum) - _sumsOfWeightLogWeights[i] / sum;
    }

    void Clear() {
        for (int i = 0; i < _wave.Length; i++) {
            for (int t = 0; t < T; t++) {
                _wave[i][t] = true;
                for (int d = 0; d < 4; d++) _compatible[i][t][d] = Propagator[Opposite[d]][t].Length;
            }

            _sumsOfOnes[i] = Weights.Length;
            _sumsOfWeights[i] = _sumOfWeights;
            _sumsOfWeightLogWeights[i] = _sumOfWeightLogWeights;
            _entropies[i] = _startingEntropy;
            Observed[i] = -1;
        }

        _observedSoFar = 0;

        if (Ground) {
            for (int x = 0; x < Mx; x++) {
                for (int t = 0; t < T - 1; t++) Ban(x + (My - 1) * Mx, t);
                for (int y = 0; y < My - 1; y++) Ban(x + y * Mx, T - 1);
            }

            Propagate();
        }
    }

    public abstract void Save();

    protected static readonly int[] Dx = { -1, 0, 1, 0 };
    protected static readonly int[] Dy = { 0, 1, 0, -1 };
    private static readonly int[] Opposite = { 2, 3, 0, 1 };
}