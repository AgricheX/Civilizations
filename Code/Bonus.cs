using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class Bonus
    {
        private static readonly Dictionary<long, Dictionary<string, float>> _cache = new Dictionary<long, Dictionary<string, float>>();
        private static Dictionary<string, float>[] _expected;

        public static void Clear()
        {
            _cache.Clear();
        }

        public static void Dirty(Kingdom k)
        {
            if (k != null && k.data != null) _cache.Remove(k.data.id);
        }

        public static Dictionary<string, float> Sums(Kingdom k)
        {
            if (k == null || k.data == null || !k.isCiv()) return new Dictionary<string, float>();
            Dictionary<string, float> m;
            if (_cache.TryGetValue(k.data.id, out m)) return m;
            m = new Dictionary<string, float>();
            KState s = Research.State(k);
            if (s != null)
            {
                foreach (TechDef t in s.Known.All())
                {
                    foreach (var kv in t.Eff)
                    {
                        if (kv.Key.StartsWith("route_")) continue;
                        Store.Add(m, kv.Key, kv.Value);
                    }
                }
            }
            foreach (PolicyDef p in Policies.All(k))
            {
                foreach (var kv in p.Eff) Store.Add(m, kv.Key, kv.Value);
            }
            foreach (var kv in Companies.Effects(k)) Store.Add(m, kv.Key, kv.Value);
            _cache[k.data.id] = m;
            return m;
        }

        public static float Sum(Kingdom k, string key)
        {
            return Store.Get(Sums(k), key);
        }

        public static float Soft(float x)
        {
            if (x >= 0f) return x / (1f + x / 160f);
            return Math.Max(-60f, x / (1f - x / 120f));
        }

        public static float Mult(Kingdom k, string key)
        {
            return 1f + Soft(Sum(k, key)) / 100f;
        }

        public static float Mult(City c, string key)
        {
            return c == null ? 1f : Mult(c.kingdom, key);
        }

        public static float Mult(Actor a, string key)
        {
            return a == null ? 1f : Mult(a.kingdom, key);
        }

        public static float Expected(int era, string key)
        {
            if (_expected == null)
            {
                _expected = new Dictionary<string, float>[Db.Eras.Count];
                var acc = new Dictionary<string, float>();
                for (int e = 0; e < Db.Eras.Count; e++)
                {
                    _expected[e] = new Dictionary<string, float>(acc);
                    foreach (TechDef t in Db.Eras[e].Techs)
                    {
                        if (t.Rare) continue;
                        float w = t.IsCore ? 0.7f : 0.25f;
                        foreach (var kv in t.Eff) Store.Add(acc, kv.Key, kv.Value * w);
                    }
                }
            }
            era = Math.Max(0, Math.Min(era, _expected.Length - 1));
            return 1f + Soft(Store.Get(_expected[era], key)) / 100f;
        }

        public static float Relative(Kingdom k, string key)
        {
            return Mult(k, key) / Expected(Research.Era(k), key);
        }
    }
}
