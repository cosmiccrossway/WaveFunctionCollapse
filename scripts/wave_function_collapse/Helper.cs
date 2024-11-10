// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

namespace WaveFunctionCollapse.scripts.wave_function_collapse;

static class Helper {
    public static int Random(this double[] weights, double r) {
        double sum = 0;
        foreach (var t in weights)
            sum += t;

        double threshold = r * sum;

        double partialSum = 0;
        for (int i = 0; i < weights.Length; i++) {
            partialSum += weights[i];
            if (partialSum >= threshold) return i;
        }

        return 0;
    }
}