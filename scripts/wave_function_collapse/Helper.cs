// Copyright (C) 2016 Maxim Gumin, The MIT License (MIT)

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace WaveFunctionCollapse.scripts.wave_function_collapse;

static class Helper {
    public static int Random(this double[] weights, double r) {
        double sum = 0;
        for (int i = 0; i < weights.Length; i++) sum += weights[i];
        double threshold = r * sum;

        double partialSum = 0;
        for (int i = 0; i < weights.Length; i++) {
            partialSum += weights[i];
            if (partialSum >= threshold) return i;
        }

        return 0;
    }

    public static long ToPower(this int a, int n) {
        long product = 1;
        for (int i = 0; i < n; i++) product *= a;
        return product;
    }

    public static T Get<T>(this XElement xelem, string attribute, T defaultT = default) {
        XAttribute a = xelem.Attribute(attribute);
        return a == null ? defaultT : (T)TypeDescriptor.GetConverter(typeof(T)).ConvertFromInvariantString(a.Value);
    }

    public static IEnumerable<XElement> Elements(this XElement xelement, params string[] names) =>
        xelement.Elements().Where(e => names.Any(n => n == e.Name));
}