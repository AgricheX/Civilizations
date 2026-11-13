using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace Civilizations
{
    internal class FlagSpec
    {
        public string Shape = "";
        public string Div = "plain";
        public string Emblem = "";
        public string Charge = "";
        public string Layout = "";
        public string Pal = "";
        public string Ov = "";
        public string Un = "";

        public FlagSpec Copy()
        {
            return (FlagSpec)MemberwiseClone();
        }

        public override string ToString()
        {
            return "f2|" + Shape + "|" + Div + "|" + Emblem + "|" + Charge + "|" + Layout + "|" + Pal + "|" + Ov + "|" + Un;
        }

        public static FlagSpec Parse(string s)
        {
            if (string.IsNullOrEmpty(s) || !s.StartsWith("f2|")) return null;
            string[] p = s.Split('|');
            if (p.Length < 9) return null;
            return new FlagSpec { Shape = p[1], Div = p[2], Emblem = p[3], Charge = p[4], Layout = p[5], Pal = p[6], Ov = p[7], Un = p[8] };
        }
    }

    internal static class Flags
    {
        public const string KFlag = "civ_flag";
        public const string KYear = "civ_flag_y";
        public const string KReason = "civ_flag_r";
        public const string KHist = "civ_flag_h";
        private static readonly int[] ReformEras = { 3, 6, 7, 8, 10, 12, 14 };
        private static readonly Dictionary<long, FlagSpec> _spec = new Dictionary<long, FlagSpec>();
        private static bool _registered;

        private static readonly string[][] DivByGroup =
        {
            new[] { "plain", "border", "fess", "chief", "base", "pale", "wavy", "hoist_band" },
            new[] { "plain", "border", "chief", "base", "fess", "pale", "lozenge", "disc", "checky" },
            new[] { "plain", "border", "chief", "fess", "pale", "disc", "base", "bars3" },
            new[] { "pale2", "fess2", "quarterly", "cross", "saltire", "bend", "per_bend", "per_bend_sinister", "chevron", "chief", "border", "checky", "pall", "per_saltire", "pile", "gyronny", "base", "lozenge", "fess", "pale" },
            new[] { "plain", "cross", "nordic", "quarterly", "saltire", "canton", "cross_border", "border", "tri_v", "fess2", "per_saltire", "pale2" },
            new[] { "tri_v", "tri_h", "stripes5", "stripes7", "stripes_canton", "canton", "triangle_hoist", "tri_hoist_bicolor", "fess2", "pale2", "nordic", "hoist_tri", "bars3", "diag_stripes", "rising_sun", "saltire" },
            new[] { "tri_h", "tri_v", "fess2", "disc", "triangle_hoist", "per_bend", "hoist_band", "fess_border", "plain", "canton", "rising_sun" },
            new[] { "plain", "per_bend", "disc", "triangle_hoist", "fess", "hoist_band", "per_bend_sinister", "diag_stripes", "lozenge", "pale" },
            new[] { "plain", "disc", "per_bend", "triangle_hoist", "hoist_band", "rising_sun", "lozenge", "fess" }
        };

        private static readonly Dictionary<string, string[]> DivByFam = new Dictionary<string, string[]>
        {
            { "republic", new[] { "tri_v", "tri_h", "stripes7", "stripes_canton", "canton", "fess2", "pale2" } },
            { "democratic", new[] { "tri_v", "tri_h", "nordic", "stripes5", "canton" } },
            { "monarchy", new[] { "quarterly", "plain", "cross", "border", "per_saltire", "cross_border" } },
            { "revolutionary", new[] { "plain", "canton", "triangle_hoist", "fess2" } },
            { "military", new[] { "plain", "fess2", "tri_h", "fess_border" } },
            { "theocratic", new[] { "cross", "plain", "border", "gyronny", "pall" } },
            { "corporate", new[] { "plain", "per_bend", "disc", "diag_stripes" } },
            { "technocratic", new[] { "disc", "per_bend", "plain", "lozenge" } },
            { "tribal", new[] { "plain", "border", "wavy" } }
        };

        private static readonly string[] Layouts = { "one", "row3", "ring", "corners", "arc", "hoist", "below" };

        public static bool On
        {
            get { return FlagArt.Ready; }
        }

        public static void Register()
        {
            if (_registered) return;
            _registered = true;
            FlagArt.Load();
            Hub.On(Hub.PolicyChanged, e =>
            {
                if (!G.Ok(e.K)) return;
                string[] a = (e.Arg ?? "").Split('|');
                if (a.Length < 2 || a[1] != "gov") return;
                bool rev = a.Length > 2 && (a[2] == "revolution" || a[2] == "coup");
                Reform(e.K, rev || Side(a[0]) != Side(e.Id) ? "revolution" : "government");
            });
            Hub.On(Hub.EraChanged, e =>
            {
                if (!G.Ok(e.K)) return;
                int era = (int)e.Value;
                int before;
                int.TryParse(e.Arg, out before);
                if (era <= before) return;
                if (Array.IndexOf(ReformEras, era) < 0) return;
                if (!G.Chance(0.8f)) return;
                Reform(e.K, "era");
            });
            Hub.On(Hub.Independence, e =>
            {
                if (G.Ok(e.K)) Reform(e.K, "independence", e.O);
            });
            Hub.On(Hub.Dependent, e =>
            {
                if (G.Ok(e.K)) Reform(e.K, "dependent");
            });
            Hub.On(Hub.KingdomFounded, e =>
            {
                if (!G.Ok(e.K) || Has(e.K)) return;
                Reform(e.K, "founded", G.Ok(e.O) && e.O != e.K ? e.O : null);
            });
            Hub.On(Hub.CivilWar, e =>
            {
                if (!G.Ok(e.O)) return;
                FlagSpec mine = Spec(e.O), theirs = G.Ok(e.K) ? Spec(e.K) : null;
                if (mine == null || theirs == null || mine.ToString() == theirs.ToString()) Reform(e.O, "rebellion", e.K);
            });
            Hub.On(Hub.TreatySigned, e =>
            {
                if (e.Id == "union" || e.Id == "personal_union" || e.Id == "royal_union") Union(e.K, e.O);
            });
            Hub.On(Hub.KingdomGone, e =>
            {
                if (e.K != null && e.K.data != null) _spec.Remove(e.K.data.id);
            });
        }

        public static void Clear()
        {
            _spec.Clear();
            FlagArt.Clear();
        }

        public static bool Has(Kingdom k)
        {
            return G.Ok(k) && Store.Str(k.data, KFlag).Length > 0;
        }

        public static FlagSpec Spec(Kingdom k)
        {
            if (!G.Ok(k)) return null;
            FlagSpec f;
            string code = Store.Str(k.data, KFlag);
            if (code.Length == 0) return null;
            if (_spec.TryGetValue(k.data.id, out f) && f != null && f.ToString() == code) return f;
            f = FlagSpec.Parse(code);
            _spec[k.data.id] = f;
            return f;
        }

        public static string Code(Kingdom k)
        {
            return G.Ok(k) ? Store.Str(k.data, KFlag) : "";
        }

        public static string Reason(Kingdom k)
        {
            return G.Ok(k) ? Store.Str(k.data, KReason) : "";
        }

        public static Color32 Main(Kingdom k)
        {
            if (k == null || k.getColor() == null) return new Color32(180, 160, 140, 255);
            Color32 c = k.getColor().getColorMain32();
            c.a = 255;
            return c;
        }

        public static Sprite Sprite(Kingdom k)
        {
            if (!On || !G.Ok(k)) return null;
            FlagSpec f = Spec(k);
            return f == null ? null : FlagArt.Get(f, Main(k));
        }

        public static Sprite SpriteOf(string code, Kingdom k)
        {
            FlagSpec f = FlagSpec.Parse(code);
            return f == null || !On ? null : FlagArt.Get(f, Main(k));
        }

        public static Sprite SpriteOf(string code, Color32 main)
        {
            FlagSpec f = FlagSpec.Parse(code);
            return f == null || !On ? null : FlagArt.Get(f, main);
        }

        public static List<KeyValuePair<int, string>> History(Kingdom k)
        {
            var list = new List<KeyValuePair<int, string>>();
            if (!G.Ok(k)) return list;
            foreach (string e in Store.Str(k.data, KHist).Split(';'))
            {
                int i = e.IndexOf('@');
                int y;
                if (i <= 0 || !int.TryParse(e.Substring(0, i), out y)) continue;
                list.Add(new KeyValuePair<int, string>(y, e.Substring(i + 1)));
            }
            return list;
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!On || !G.Ok(k) || !k.isCiv()) return;
            FlagSpec f = Spec(k);
            if (f == null)
            {
                Convert(k);
                return;
            }
            KProfile p = Profile.Get(k);
            int g = Group(p.Era);
            FlagPart sh;
            if (FlagArt.ShapeById.TryGetValue(f.Shape, out sh) && sh.Groups.Count > 0)
            {
                int far = sh.Groups.Min(x => Math.Abs(x - g));
                if (far >= 2 && G.Year - Store.Int(k.data, KYear, G.Year) >= 5)
                {
                    Reform(k, "era");
                    return;
                }
            }
            Kingdom lord = Diplomacy.Overlord(k);
            bool hasOv = f.Ov.Length > 0;
            if (lord != null && !hasOv) Reform(k, "dependent");
            else if (lord == null && hasOv) Reform(k, "independence");
        }

        public static void Union(Kingdom a, Kingdom b)
        {
            if (!On || !G.Ok(a) || !G.Ok(b)) return;
            FlagSpec fb = Spec(b) ?? Design(b, null, "founded", null, new System.Random(Seed(b)));
            FlagSpec fa = Spec(a) ?? Design(a, null, "founded", null, new System.Random(Seed(a)));
            FlagSpec n = fa.Copy();
            n.Un = fb.Emblem + ":" + FlagArt.Hex(Main(b));
            n.Div = "plain";
            n.Charge = "";
            Apply(a, n, "union");
        }

        private static int Seed(Kingdom k)
        {
            return k == null || k.data == null ? 7 : (int)(k.data.id * 7919L % int.MaxValue);
        }

        private static string Side(string gov)
        {
            if (string.IsNullOrEmpty(gov)) return "";
            if (Coherence.Is(gov, "monarchy")) return "crown";
            if (Coherence.Is(gov, "theocratic")) return "faith";
            if (Coherence.Is(gov, "tribal")) return "tribe";
            if (Coherence.Is(gov, "authoritarian")) return "state";
            return "people";
        }

        public static int Group(int era)
        {
            if (era <= 2) return 0;
            if (era <= 5) return 1;
            if (era == 6) return 2;
            if (era == 7) return 3;
            if (era <= 9) return 4;
            if (era <= 11) return 5;
            if (era <= 13) return 6;
            if (era < Research.PlanetEras) return 7;
            return 8;
        }

        private static void Convert(Kingdom k)
        {
            var rnd = new System.Random(Seed(k) ^ (k.data.banner_icon_id * 131 + k.data.banner_background_id * 17));
            FlagSpec f = Design(k, null, "converted", null, rnd);
            Store.Put(k.data, KFlag, f.ToString());
            Store.Put(k.data, KYear, G.Year);
            Store.Put(k.data, KReason, "converted");
            _spec.Remove(k.data.id);
        }

        public static void Reform(Kingdom k, string reason, Kingdom parent = null)
        {
            if (!On || !G.Ok(k) || !k.isCiv()) return;
            FlagSpec old = Spec(k);
            if (old != null && reason == "era" && Store.Int(k.data, KYear, int.MinValue) == G.Year) return;
            FlagSpec pf = G.Ok(parent) && parent != k ? Spec(parent) : null;
            FlagSpec n = Design(k, old, reason, pf, new System.Random(Seed(k) ^ G.Year * 31 ^ UnityEngine.Random.Range(0, 99999)));
            Apply(k, n, reason);
        }

        private static void Apply(Kingdom k, FlagSpec n, string reason)
        {
            string oldCode = Code(k);
            string code = n.ToString();
            if (code == oldCode) return;
            Store.Put(k.data, KFlag, code);
            Store.Put(k.data, KYear, G.Year);
            Store.Put(k.data, KReason, reason);
            _spec.Remove(k.data.id);
            if (oldCode.Length > 0)
            {
                var hist = Store.Str(k.data, KHist).Split(';').Where(x => x.Length > 0).ToList();
                hist.Add(G.Year + "@" + oldCode);
                while (hist.Count > 6) hist.RemoveAt(0);
                Store.Put(k.data, KHist, string.Join(";", hist.ToArray()));
            }
            if (oldCode.Length > 0 || reason == "founded") Hub.Raise(Hub.FlagReform, k, null, k.capital, null, code, oldCode);
        }

        private static float Pick<T>(System.Random rnd, List<KeyValuePair<T, float>> opts, out T result)
        {
            result = default(T);
            float total = 0f;
            foreach (var o in opts) if (o.Value > 0f) total += o.Value;
            if (total <= 0f) return 0f;
            double r = rnd.NextDouble() * total;
            foreach (var o in opts)
            {
                if (o.Value <= 0f) continue;
                r -= o.Value;
                if (r <= 0)
                {
                    result = o.Key;
                    return o.Value;
                }
            }
            result = opts.Last(x => x.Value > 0f).Key;
            return 1f;
        }

        private static string SpeciesTag(string sp)
        {
            if (string.IsNullOrEmpty(sp)) return "sp:human";
            if (sp == "human" || sp == "elf" || sp == "dwarf" || sp == "orc") return "sp:" + sp;
            return "sp:beast";
        }

        private static bool Royal(KProfile p)
        {
            return p.Fam.Contains("monarchy");
        }

        private static bool Sacred(KProfile p)
        {
            return p.Fam.Contains("theocratic");
        }

        public static float EmblemScore(FlagPart e, KProfile p, int g)
        {
            var t = e.Tags;
            if (t.Contains("royal") && !Royal(p)) return 0f;
            if (t.Contains("sacred") && !Sacred(p)) return 0f;
            if (t.Contains("industrial") && p.Era < 9) return 0f;
            if (t.Contains("corporate") && !p.Fam.Contains("corporate") && !(p.Econ == "digital_economy" || p.Econ == "laissez_faire")) return 0f;
            if (t.Contains("corporate") && (p.Fam.Contains("leftist") || p.Fam.Contains("revolutionary"))) return 0f;
            if (t.Contains("pacifist") && p.Fam.Contains("military")) return 0f;
            if (t.Contains("fam:tribal") && g >= 3 && !p.Fam.Contains("tribal")) return 0f;
            float s = 0.5f;
            bool hasEra = false;
            int bestDiff = 99;
            foreach (string tag in t)
            {
                if (!tag.StartsWith("eg:")) continue;
                hasEra = true;
                int eg;
                if (int.TryParse(tag.Substring(3), out eg)) bestDiff = Math.Min(bestDiff, Math.Abs(eg - g));
            }
            if (hasEra)
            {
                if (bestDiff == 0) s += 3f;
                else if (bestDiff == 1) s += 1f;
                else if (bestDiff >= 3) s -= 2.5f;
                else s -= 1f;
            }
            foreach (string f in p.Fam) if (t.Contains("fam:" + f)) s += 3.5f;
            if (t.Contains(SpeciesTag(p.Species))) s += 2.5f;
            if (t.Contains("agrarian") && (p.Econ == "planned" || p.Econ == "reciprocity" || p.Econ == "manorialism" || p.Fam.Contains("tribal") || p.Fam.Contains("green"))) s += 2f;
            if (t.Contains("industrial") && (p.Fam.Contains("planned") || p.Fam.Contains("corporate"))) s += 1.5f;
            if (t.Contains("royal")) s += 2f;
            if (t.Contains("sacred")) s += 3f;
            if (t.Contains("corporate")) s += 2.5f;
            return Math.Max(0f, s);
        }

        private static bool StillFits(FlagPart e, KProfile p, int g)
        {
            return e != null && EmblemScore(e, p, g) >= 1.5f;
        }

        private static float PaletteScore(FlagPalette pal, KProfile p, int g)
        {
            if (pal.Tags.Contains("royal") && !Royal(p)) return 0f;
            if (pal.Tags.Contains("corporate") && !p.Fam.Contains("corporate")) return 0.1f;
            float s = 0.4f;
            foreach (string tag in pal.Tags)
            {
                if (tag == "eg:" + g) s += 2f;
                else if (tag.StartsWith("eg:"))
                {
                    int eg;
                    if (int.TryParse(tag.Substring(3), out eg) && Math.Abs(eg - g) >= 3) s -= 0.3f;
                }
                if (tag.StartsWith("fam:") && p.Fam.Contains(tag.Substring(4))) s += 3f;
            }
            if (g <= 1 && !pal.Tags.Contains("eg:0") && !pal.Tags.Contains("eg:1")) s *= 0.35f;
            return Math.Max(0.05f, s);
        }

        private static List<string> DivOptions(KProfile p, int g)
        {
            var list = new List<string>(DivByGroup[g]);
            foreach (string f in p.Fam)
            {
                string[] extra;
                if (!DivByFam.TryGetValue(f, out extra)) continue;
                foreach (string d in extra)
                {
                    bool fits = g >= 4 || DivByGroup[g].Contains(d);
                    if (fits) list.Add(d);
                }
            }
            return list;
        }

        public static FlagSpec Design(Kingdom k, FlagSpec prev, string reason, FlagSpec parent, System.Random rnd)
        {
            KProfile p = Profile.Get(k);
            int g = Group(p.Era);
            var n = new FlagSpec();
            FlagSpec basis = prev ?? parent;
            float keepEmb = reason == "era" ? 0.75f : reason == "government" ? 0.6f : reason == "revolution" ? 0.25f : reason == "independence" ? 0.4f : reason == "dependent" || reason == "union" ? 1f : reason == "rebellion" || reason == "founded" ? 0.35f : 0.7f;
            float keepDiv = reason == "dependent" || reason == "union" ? 1f : reason == "era" ? 0.5f : reason == "revolution" ? 0.2f : 0.45f;
            float keepPal = reason == "revolution" ? 0.35f : reason == "rebellion" || reason == "founded" ? 0.3f : 0.75f;
            if (reason == "rebellion" || (reason == "founded" && parent != null))
            {
                keepDiv = 0.3f;
                keepEmb = 0.25f;
            }
            var shapes = FlagArt.Shapes.Where(s => s.Groups.Contains(g)).ToList();
            if (shapes.Count == 0) shapes = FlagArt.Shapes;
            FlagPart ps = null;
            if (basis != null) FlagArt.ShapeById.TryGetValue(basis.Shape, out ps);
            if (ps != null && shapes.Contains(ps) && rnd.NextDouble() < 0.85) n.Shape = ps.Id;
            else n.Shape = shapes[rnd.Next(shapes.Count)].Id;
            FlagPart pe = null;
            if (basis != null) FlagArt.EmblemById.TryGetValue(basis.Emblem, out pe);
            if (pe != null && StillFits(pe, p, g) && rnd.NextDouble() < keepEmb) n.Emblem = pe.Id;
            else
            {
                var opts = FlagArt.Emblems.Select(e => new KeyValuePair<FlagPart, float>(e, EmblemScore(e, p, g))).Where(x => x.Value > 0f && (pe == null || x.Key != pe || reason == "converted")).OrderByDescending(x => x.Value).Take(18).Select(x => new KeyValuePair<FlagPart, float>(x.Key, x.Value * x.Value)).ToList();
                FlagPart pick;
                Pick(rnd, opts, out pick);
                n.Emblem = pick != null ? pick.Id : FlagArt.Emblems[rnd.Next(FlagArt.Emblems.Count)].Id;
            }
            List<string> divs = DivOptions(p, g);
            if (basis != null && divs.Contains(basis.Div) && rnd.NextDouble() < keepDiv) n.Div = basis.Div;
            else
            {
                string d = divs[rnd.Next(divs.Count)];
                if (basis != null && d == basis.Div && divs.Count > 1 && reason != "converted") d = divs[rnd.Next(divs.Count)];
                n.Div = d;
            }
            FlagPalette pp = null;
            if (basis != null) FlagArt.PaletteById.TryGetValue(basis.Pal, out pp);
            if (pp != null && PaletteScore(pp, p, g) >= 0.4f && rnd.NextDouble() < keepPal) n.Pal = pp.Id;
            else
            {
                FlagPalette pick;
                Pick(rnd, FlagArt.Palettes.Select(x => new KeyValuePair<FlagPalette, float>(x, PaletteScore(x, p, g) * PaletteScore(x, p, g))).ToList(), out pick);
                n.Pal = (pick ?? FlagArt.Palettes[0]).Id;
            }
            double chargeChance = g == 0 ? 0.25 : g <= 2 ? 0.3 : g == 3 ? 0.35 : g == 5 ? 0.55 : 0.4;
            if (n.Div == "canton" || n.Div == "stripes_canton") chargeChance = 0.6;
            if (basis != null && basis.Charge.Length > 0 && reason != "revolution" && rnd.NextDouble() < 0.6)
            {
                n.Charge = basis.Charge;
                n.Layout = basis.Layout;
            }
            else if (rnd.NextDouble() < chargeChance)
            {
                var copts = FlagArt.Charges.Select(c => new KeyValuePair<FlagPart, float>(c, ChargeScore(c, p, g))).ToList();
                FlagPart pick;
                Pick(rnd, copts, out pick);
                if (pick != null)
                {
                    n.Charge = pick.Id;
                    n.Layout = (n.Div == "canton" || n.Div == "stripes_canton") && rnd.NextDouble() < 0.7 ? "canton" : Layouts[rnd.Next(Layouts.Length)];
                }
            }
            Kingdom lord = Diplomacy.Overlord(k);
            if (lord != null && reason != "independence")
            {
                FlagSpec lf = Spec(lord);
                if (lf != null) n.Ov = lf.Emblem + ":" + FlagArt.Hex(Main(lord));
            }
            if (prev != null && prev.Un.Length > 0 && reason != "revolution" && reason != "independence") n.Un = prev.Un;
            return n;
        }

        private static float ChargeScore(FlagPart c, KProfile p, int g)
        {
            var t = c.Tags;
            if (t.Contains("royal") && !Royal(p)) return 0f;
            if (t.Contains("sacred") && !Sacred(p)) return 0f;
            if (t.Contains("corporate") && !p.Fam.Contains("corporate")) return 0f;
            if (t.Contains("industrial") && p.Era < 9) return 0f;
            float s = t.Contains("any") ? (g >= 4 ? 3f : 0.6f) : 0.3f;
            foreach (string tag in t)
            {
                if (tag == "eg:" + g) s += 2.5f;
                if (tag.StartsWith("fam:") && p.Fam.Contains(tag.Substring(4))) s += 2f;
            }
            if (t.Contains(SpeciesTag(p.Species))) s += 1.5f;
            return s;
        }

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getElementBackground))]
        private static class BackgroundPatch
        {
            private static void Postfix(Kingdom __instance, ref Sprite __result)
            {
                try
                {
                    Sprite s = Sprite(__instance);
                    if (s != null) __result = s;
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(Kingdom), nameof(Kingdom.getElementIcon))]
        private static class IconPatch
        {
            private static void Postfix(Kingdom __instance, ref Sprite __result)
            {
                try
                {
                    if (On && Has(__instance)) __result = FlagArt.Blank;
                }
                catch (Exception)
                {
                }
            }
        }

        private static void White(Kingdom k, UnityEngine.UI.Image bg, UnityEngine.UI.Image icon)
        {
            if (!On || !Has(k)) return;
            if (bg != null) bg.color = Color.white;
            if (icon != null) icon.color = Color.white;
        }

        [HarmonyPatch(typeof(KingdomBanner), "setupBanner")]
        private static class KingdomBannerPatch
        {
            private static void Postfix(KingdomBanner __instance)
            {
                try
                {
                    White(__instance.meta_object, __instance.part_background, __instance.part_icon);
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(ArmyBanner), "setupBanner")]
        private static class ArmyBannerPatch
        {
            private static void Postfix(ArmyBanner __instance)
            {
                try
                {
                    if (__instance.meta_object != null) White(__instance.meta_object.getKingdom(), __instance.part_background, __instance.part_icon);
                }
                catch (Exception)
                {
                }
            }
        }

        [HarmonyPatch(typeof(CityBanner), "setupBanner")]
        private static class CityBannerPatch
        {
            private static void Postfix(CityBanner __instance)
            {
                try
                {
                    if (__instance.meta_object != null) White(__instance.meta_object.kingdom, __instance.part_background, __instance.part_icon);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
