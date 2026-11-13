using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal class KProfile
    {
        public int Year = int.MinValue;
        public int Era;
        public string Route = "";
        public string Gov = "";
        public string Ideo = "";
        public string Econ = "";
        public string Xeno = "";
        public string Species = "";
        public string Religion = "";
        public int Pop;
        public int Cities;
        public int Warriors;
        public int Gold;
        public float Stability;
        public float Legit;
        public bool AtWar;
        public bool Coastal;
        public readonly HashSet<string> Fam = new HashSet<string>();
        public readonly HashSet<string> Tags = new HashSet<string>();

        public bool Is(string tag)
        {
            return Tags.Contains(tag);
        }

        public float Strength
        {
            get { return Pop * 0.5f + Warriors * 3f + Cities * 10f + Era * 15f; }
        }
    }

    internal static class Profile
    {
        private static readonly Dictionary<long, KProfile> _cache = new Dictionary<long, KProfile>();

        public static void Clear()
        {
            _cache.Clear();
        }

        public static KProfile Get(Kingdom k)
        {
            if (!G.Ok(k)) return new KProfile();
            KProfile p;
            if (_cache.TryGetValue(k.data.id, out p) && p.Year == G.Year) return p;
            p = Build(k, p);
            _cache[k.data.id] = p;
            return p;
        }

        public static void Invalidate(Kingdom k)
        {
            if (G.Ok(k)) _cache.Remove(k.data.id);
        }

        private static KProfile Build(Kingdom k, KProfile old)
        {
            var p = new KProfile { Year = G.Year };
            p.Era = Research.Era(k);
            p.Route = Research.Route(k) ?? "";
            p.Gov = Policies.Id(k, "gov") ?? "";
            p.Ideo = Policies.Id(k, "ideo") ?? "";
            p.Econ = Policies.Id(k, "econ") ?? "";
            p.Xeno = Policies.Id(k, "xeno") ?? "";
            try
            {
                p.Species = k.getSpecies() ?? "";
            }
            catch (Exception)
            {
                p.Species = "";
            }
            try
            {
                Religion r = k.getReligion();
                p.Religion = r != null && r.data != null ? r.data.id.ToString() : "";
            }
            catch (Exception)
            {
                p.Religion = "";
            }
            List<City> cities = G.Cities(k);
            p.Cities = cities.Count;
            p.Pop = G.Pop(k);
            try
            {
                p.Warriors = k.countTotalWarriors();
            }
            catch (Exception)
            {
                p.Warriors = 0;
            }
            foreach (City c in cities)
            {
                p.Gold += c.getResourcesAmount("gold");
                if (!p.Coastal && c.hasBuildingType("type_docks")) p.Coastal = true;
            }
            p.Stability = Politics.Stability(k);
            p.Legit = Legitimacy.Get(k);
            p.AtWar = World.world.wars.hasWars(k);
            Coherence.Families(p.Gov, p.Ideo, p.Econ, p.Fam);
            p.Tags.Add("era:" + p.Era);
            p.Tags.Add("gov:" + p.Gov);
            p.Tags.Add("ideo:" + p.Ideo);
            p.Tags.Add("econ:" + p.Econ);
            p.Tags.Add("xeno:" + p.Xeno);
            if (p.Route.Length > 0) p.Tags.Add("route:" + p.Route);
            if (p.Species.Length > 0) p.Tags.Add("species:" + p.Species);
            foreach (string f in p.Fam) p.Tags.Add("fam:" + f);
            if (p.AtWar) p.Tags.Add("war");
            if (p.Coastal) p.Tags.Add("coast");
            if (k.getReligion() != null) p.Tags.Add("religious");
            if (p.Stability < 0.3f) p.Tags.Add("unstable");
            if (p.Legit < 0.3f) p.Tags.Add("illegitimate");
            return p;
        }
    }
}
