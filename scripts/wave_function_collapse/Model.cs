// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System;

namespace WaveFunctionCollapse.scripts.wave_function_collapse;

internal abstract class Model {
    protected bool[][] Wave;

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

    private void Init() {
        Wave = new bool[Mx * My][];
        _compatible = new int[Wave.Length][][];
        for (var nodeLocation = 0; nodeLocation < Wave.Length; nodeLocation++) {
            Wave[nodeLocation] = new bool[T];
            _compatible[nodeLocation] = new int[T][];
            for (var patternReference = 0; patternReference < T; patternReference++) {
                _compatible[nodeLocation][patternReference] = new int[4];
            }
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

        _stack = new (int, int)[Wave.Length * T];
        _stackSize = 0;
    }

    public bool Run(int seed, int limit) {
        if (Wave == null) Init();
        if (Wave == null) throw new Exception("Model Wave has not been initialized");

        Clear();
        Random random = new(seed);

        PreBan();
        var success = Propagate();
        if (!success) return false;
        for (int l = 0; l < limit || limit <= 0; l++) {
            int node = NextUnobservedNode(random);
            // So long as there are unobserved nodes
            if (node >= 0) {
                // Observe a node (determine what tile it should be based on weighted randomness
                Observe(node, random);
                // Then propagate those changes by visiting nearby tiles and determining what choices are
                // eliminated by the selection made in the observed node.
                success = Propagate();
                // If propagation failed, then it was unable to create a valid pattern.
                // return false so the that we can start over and rerun the algorithm.
                if (!success) return false;
            } else {
                // Once there are no longer any unobserved nodes, then we take the results in the _wave variable
                // and condense them into a tighter format in the Observed variable.
                for (var i = 0; i < Wave.Length; i++)
                    for (var t = 0; t < T; t++)
                        if (Wave[i][t]) {
                            Observed[i] = t;
                            break;
                        }

                return true;
            }
        }

        return true;
    }

    private int NextUnobservedNode(Random random) {
        if (_heuristic == Heuristic.Scanline) {
            for (int i = _observedSoFar; i < Wave.Length; i++) {
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
        for (int i = 0; i < Wave.Length; i++) {
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


    private void Observe(int node, Random random) {
        // determine which pattern to use for this location based on what's patterns are still available for the
        // location and what weights each pattern has.
        var patternsAvailableAtLocation = Wave[node];
        for (var t = 0; t < T; t++) {
            _distribution[t] = patternsAvailableAtLocation[t] ? Weights[t] : 0.0;
        }
        var chosenPatternForLocation = _distribution.Random(random.NextDouble());
        
        // Ban all patterns other than the chosen pattern for the given location
        for (var patternReference = 0; patternReference < T; patternReference++)
            if (patternsAvailableAtLocation[patternReference] != (patternReference == chosenPatternForLocation))
                Ban(node, patternReference);
    }

    protected bool Propagate() {
        while (_stackSize > 0) {
            // Get node location and pattern reference from off the stack (which Ban added to the stack).
            var (nodeLocation, patternReference) = _stack[_stackSize - 1];
            _stackSize--;

            // Get the x and y values for the node location.
            var xOfNode = nodeLocation % Mx;
            var yOfNode = nodeLocation / Mx;

            // For each of the four node neighbors
            for (var directionReference = 0; directionReference < 4; directionReference++) {
                // Get the neighbor x and y values
                var xOfNeighbor = xOfNode + Dx[directionReference];
                var yOfNeighbor = yOfNode + Dy[directionReference];
                // If periodic (the output will be tileable), then move to the next line without further checks.
                // If not periodic, then check that the x and y values of the neighbor wouldn't result in an out-of-
                // bounds pattern (patterns are of the size NxN).
                // 
                // For example, if Mx and My are both 3, and N is 2, then valid x/y values for the neighbor would include:
                //   (0, 0), (0, 1), (1, 0), (1, 1)
                //
                //   + + -
                //   + + -
                //   - - -
                //
                // Any values outside of those would be invalid, and we would skip over the processing of those neighbors.
                // So then if the x/y values of the original node is (1, 1), then the only neighbors that would be
                // processed from the example above would be (1, 0) and (0, 1), because the neighbors (2, 1) and (1, 2)
                // would not be able to reference a pattern without it going out of bounds.
                if (!_periodic && (xOfNeighbor < 0 || yOfNeighbor < 0 || xOfNeighbor + N > Mx || yOfNeighbor + N > My)) continue;

                // Do some extra math to allow periodic references to wrap around.
                if (xOfNeighbor < 0) xOfNeighbor += Mx;
                else if (xOfNeighbor >= Mx) xOfNeighbor -= Mx;
                if (yOfNeighbor < 0) yOfNeighbor += My;
                else if (yOfNeighbor >= My) yOfNeighbor -= My;

                
                var neighborLocation = xOfNeighbor + yOfNeighbor * Mx;
                // for the given direction reference (left, down, right, up) and pattern reference,
                // get the available patterns.
                var agreeablePatternsForDirection = Propagator[directionReference][patternReference];
                var compatiblePatternsForEachDirectionForNeighbor = _compatible[neighborLocation];

                foreach (var agreeablePatternForDirection in agreeablePatternsForDirection) {
                    var compatiblePatternCountsForEachDirectionForGivenNeighborPattern = 
                        compatiblePatternsForEachDirectionForNeighbor[agreeablePatternForDirection];

                    // Decrement the compatible pattern count for the given neighbor/pattern/direction reference.
                    compatiblePatternCountsForEachDirectionForGivenNeighborPattern[directionReference]--;
                    // If that becomes zero, then that pattern cannot be used by this neighbor, so ban the pattern.
                    if (compatiblePatternCountsForEachDirectionForGivenNeighborPattern[directionReference] == 0) {
                        Ban(neighborLocation, agreeablePatternForDirection);
                    }
                }
            }
        }

        // If we detect that the number of patterns available for a location falls below 1, then the generation has
        // failed, otherwise, propagation succeeded, and we continue the process.
        return _sumsOfOnes[0] > 0;
    }

    protected void Ban(int nodeLocation, int patternReference) {
        // Set the pattern to not used for the given location.
        Wave[nodeLocation][patternReference] = false;

        // For the node location, set the pattern reference as unavailable for all four directions
        // that might try to reference it.
        int[] comp = _compatible[nodeLocation][patternReference];
        for (int d = 0; d < 4; d++) {
            comp[d] = 0;
        }
        
        // Add the banned nodeLocation and patternReference to the stack for further evaluation at the propagate step.
        _stack[_stackSize] = (nodeLocation, patternReference);
        _stackSize++;

        // Mathematically remove the pattern reference from the high level accounting number for the location.
        _sumsOfOnes[nodeLocation] -= 1;
        _sumsOfWeights[nodeLocation] -= Weights[patternReference];
        _sumsOfWeightLogWeights[nodeLocation] -= _weightLogWeights[patternReference];

        double sum = _sumsOfWeights[nodeLocation];
        _entropies[nodeLocation] = Math.Log(sum) - _sumsOfWeightLogWeights[nodeLocation] / sum;
    }

    void Clear() {
        for (int nodeLocation = 0; nodeLocation < Wave.Length; nodeLocation++) {
            for (int patternReference = 0; patternReference < T; patternReference++) {
                Wave[nodeLocation][patternReference] = true;
                for (int directionReference = 0; directionReference < 4; directionReference++) {
                    // For a given pattern reference at a node location, store the number of compatible patterns
                    // for each direction (neighbor). This tells neighbors whether the pattern can be used when
                    // the neighbors try to reference this location.
                    _compatible[nodeLocation][patternReference][directionReference] = 
                        Propagator[Opposite[directionReference]][patternReference].Length;
                }
            }

            _sumsOfOnes[nodeLocation] = Weights.Length;
            _sumsOfWeights[nodeLocation] = _sumOfWeights;
            _sumsOfWeightLogWeights[nodeLocation] = _sumOfWeightLogWeights;
            _entropies[nodeLocation] = _startingEntropy;
            Observed[nodeLocation] = -1;
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

    protected virtual void PreBan() {
        // Default do nothing
    }

    protected static readonly int[] Dx = { -1, 0, 1, 0 };
    protected static readonly int[] Dy = { 0, 1, 0, -1 };
    private static readonly int[] Opposite = { 2, 3, 0, 1 };
}