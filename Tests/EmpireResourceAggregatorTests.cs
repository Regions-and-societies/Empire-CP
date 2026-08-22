// Tier-1 sandbox tests for the region resource aggregation (issue #3).
//
// EmpireResourceAggregator is pure — generic over the key, no RimWorld, Unity, Harmony or Empire
// types — so this suite compiles against nothing but the aggregator itself and runs anywhere a C#
// compiler exists. What is being checked is the acceptance identity in miniature: a region's total
// is the sum of its settlements' contributions, an empty or all-zero region totals to nothing, and
// a single settlement passes through unchanged.
using System;
using System.Collections.Generic;
using RegionsAndSocieties.EmpireCP;

namespace EmpireResourceAggregatorTests
{
    public static class Program
    {
        private static int failures;

        public static int Main()
        {
            Section("an empty region supplies nothing");
            Check("no contributions -> empty aggregate", EmpireResourceAggregator.Aggregate(Contribs()).Count == 0);
            Check("null input is treated as empty", EmpireResourceAggregator.Aggregate<string>(null).Count == 0);

            Section("a single settlement passes through unchanged");
            var single = EmpireResourceAggregator.Aggregate(Contribs(("wood", 12.0)));
            Check("one resource type", single.Count == 1);
            Check("its amount is carried through", Near(single["wood"], 12.0));

            Section("a region's total is the sum of its settlements");
            var region = EmpireResourceAggregate(
                Contribs(("wood", 10.0), ("steel", 4.0)),   // settlement A
                Contribs(("wood", 6.0), ("cloth", 3.0)),    // settlement B
                Contribs(("steel", 1.0)));                  // settlement C
            Check("three distinct resource types", region.Count == 3);
            Check("wood sums across A and B", Near(region["wood"], 16.0));
            Check("steel sums across A and C", Near(region["steel"], 5.0));
            Check("cloth comes from B alone", Near(region["cloth"], 3.0));
            Check("the grand total equals the sum of all parts", Near(Total(region), 24.0));

            Section("zero and negative producers are not supply");
            var filtered = EmpireResourceAggregator.Aggregate(Contribs(("wood", 0.0), ("steel", -5.0), ("cloth", 2.0)));
            Check("a zero producer is dropped", !filtered.ContainsKey("wood"));
            Check("a negative producer is dropped", !filtered.ContainsKey("steel"));
            Check("only the real producer survives", filtered.Count == 1 && Near(filtered["cloth"], 2.0));

            Section("a resource split across settlements still sums");
            var split = EmpireResourceAggregate(
                Contribs(("chemfuel", 2.5)),
                Contribs(("chemfuel", 2.5)),
                Contribs(("chemfuel", 2.5)));
            Check("three halves of the same resource merge", split.Count == 1 && Near(split["chemfuel"], 7.5));

            Console.WriteLine();
            if (failures == 0) { Console.WriteLine("ALL ASSERTIONS PASSED"); return 0; }
            Console.WriteLine(failures + " ASSERTION(S) FAILED");
            return 1;
        }

        // Aggregate several settlements' contributions the way a region does: flatten, then sum.
        private static Dictionary<string, double> EmpireResourceAggregate(params List<KeyValuePair<string, double>>[] settlements)
        {
            var all = new List<KeyValuePair<string, double>>();
            foreach (var s in settlements) all.AddRange(s);
            return EmpireResourceAggregator.Aggregate(all);
        }

        private static List<KeyValuePair<string, double>> Contribs(params (string key, double amount)[] entries)
        {
            var list = new List<KeyValuePair<string, double>>();
            foreach (var e in entries) list.Add(new KeyValuePair<string, double>(e.key, e.amount));
            return list;
        }

        private static double Total(Dictionary<string, double> d)
        {
            double t = 0; foreach (var v in d.Values) t += v; return t;
        }

        private static bool Near(double a, double b) { return Math.Abs(a - b) < 0.0001; }

        private static void Section(string name) { Console.WriteLine("\n== " + name + " =="); }

        private static void Check(string label, bool ok)
        {
            Console.WriteLine((ok ? "  PASS " : "  FAIL ") + label);
            if (!ok) failures++;
        }
    }
}
