using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;

namespace Civilizations
{
    internal class FestivalDef
    {
        public string Id = "";
        public string Tech = "";
        public List<string> Need = new List<string>();
        public List<string> Any = new List<string>();
        public List<string> Cond = new List<string>();
        public Dictionary<string, float> Fx = new Dictionary<string, float>();
        public bool Big;
        public bool Fireworks;
        public float W = 1f;
    }

    internal static class Festivals
    {
        private const string KAdopted = "civ_festivals";
        private const string KPicked = "civ_fest_pick_y";
        private const string KWon = "civ_last_win_y";
        private const string KFx = "civ_fest_fx";
        private const string WGames = "civ_games_host";
        public static readonly List<FestivalDef> Defs = new List<FestivalDef>();
        public static readonly Dictionary<string, FestivalDef> ById = new Dictionary<string, FestivalDef>();

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.Festivals; }
        }

        public static void InitData()
        {
            Defs.Clear();
            ById.Clear();
            try
            {
                string p = Path.Combine(CivMod.Folder, "Data", "festivals.json");
                if (!File.Exists(p)) return;
                foreach (FestivalDef d in JsonConvert.DeserializeObject<List<FestivalDef>>(File.ReadAllText(p)))
                {
                    Defs.Add(d);
                    ById[d.Id] = d;
                }
            }
            catch (Exception e)
            {
                CivMod.Log("festivals data: " + e.Message);
            }
        }

        private const string KMarks = "civ_fest_marks";

        public static void Mark(Kingdom k, string flag)
        {
            if (!G.Ok(k)) return;
            HashSet<string> s = Store.Set(k.data, KMarks);
            if (!s.Add(flag)) return;
            Store.Set(k.data, KMarks, s);
            Store.Put(k.data, KPicked, -999);
        }

        public static bool Fireworks(Kingdom k)
        {
            return G.Ok(k) && Research.Has(k, "pyrotechnics");
        }

        public static List<string> Adopted(Kingdom k)
        {
            return G.Ok(k) ? Store.Set(k.data, KAdopted).ToList() : new List<string>();
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var m = new Dictionary<string, float>();
            if (!G.Ok(k)) return m;
            if (Store.Int(k.data, KFx + "_y", -1) != G.Year) return m;
            foreach (var kv in Store.Map(k.data, KFx)) m[kv.Key] = kv.Value;
            return m;
        }

        private static string Climate(Kingdom k)
        {
            int frozen = 0, cold = 0, warm = 0, temp = 0;
            foreach (City c in G.Cities(k).Take(5))
            {
                WorldTile t = G.Tile(c);
                if (t == null || t.Type == null) continue;
                string tid = t.Type.id ?? "";
                BiomeAsset b = t.getBiome();
                string bid = b != null ? b.id : "";
                if (tid.Contains("snow") || bid.Contains("permafrost")) frozen++;
                else if (bid.Contains("birch") || bid.Contains("maple") || bid.Contains("garlic")) cold++;
                else if (bid.Contains("savanna") || bid.Contains("jungle") || bid.Contains("desert") || bid.Contains("sand") || bid.Contains("lemon") || bid.Contains("swamp") || bid.Contains("infernal")) warm++;
                else temp++;
            }
            int max = Math.Max(Math.Max(frozen, cold), Math.Max(warm, temp));
            if (max == 0) return "temperate";
            if (frozen == max) return "frozen";
            if (cold == max) return "cold";
            if (warm == max) return "warm";
            return "temperate";
        }

        private static bool Plains(Kingdom k)
        {
            WorldTile t = k.capital != null ? G.Tile(k.capital) : null;
            BiomeAsset b = t != null ? t.getBiome() : null;
            string id = b != null ? b.id : "";
            return id.Contains("grass") || id.Contains("savanna") || id.Contains("flower") || id.Contains("clover");
        }

        private static bool Fits(FestivalDef d, Kingdom k, KProfile p, string climate)
        {
            if (!Research.Has(k, d.Tech)) return false;
            foreach (string t in d.Need) if (!p.Is(t)) return false;
            if (d.Any.Count > 0 && !d.Any.Any(p.Is)) return false;
            foreach (string c in d.Cond)
            {
                switch (c)
                {
                    case "farms": if (Bonus.Sum(k, "food") < 0f && p.Era < 2) return false; break;
                    case "cold": if (climate != "cold" && climate != "frozen") return false; break;
                    case "frozen": if (climate != "frozen") return false; break;
                    case "temperate": if (climate != "temperate") return false; break;
                    case "temperate_or_cold": if (climate == "warm") return false; break;
                    case "warm_or_temperate": if (climate == "frozen") return false; break;
                    case "river_or_coast": if (!p.Coastal && !Research.Has(k, "river_boats")) return false; break;
                    case "plains_or_coast": if (!p.Coastal && !Plains(k)) return false; break;
                    case "rich": if (p.Gold < p.Cities * 120) return false; break;
                    case "king": if (!k.hasKing()) return false; break;
                    case "era6": if (p.Era < 6) return false; break;
                    case "era10": if (p.Era < 10) return false; break;
                    case "era_games": if (p.Era > 7 && p.Era < 10 && !Research.Has(k, "modern_games")) return false; break;
                    case "after_world_war":
                        SaveCustomData w = Store.World();
                        if (w == null || G.Year - Store.Int(w, "civ_world_war_year", -9999) > 60) return false;
                        break;
                    case "victory":
                    case "jubilee":
                        break;
                    default:
                        if (c.StartsWith("tech_any:") && !c.Substring(9).Split(':').Any(x => Research.Has(k, x))) return false;
                        if (c.StartsWith("mark:") && !Store.Set(k.data, KMarks).Contains(c.Substring(5))) return false;
                        if (c.StartsWith("cosmos:") && CosmosSim.CachedScale(k) < int.Parse(c.Substring(7))) return false;
                        break;
                }
            }
            return true;
        }

        private static void Pick(Kingdom k, KProfile p)
        {
            string climate = Climate(k);
            var keep = new List<string>();
            HashSet<string> cur = Store.Set(k.data, KAdopted);
            foreach (string id in cur)
            {
                FestivalDef d;
                if (ById.TryGetValue(id, out d) && Fits(d, k, p, climate)) keep.Add(id);
            }
            int max = p.Era < 4 ? 2 : p.Era < 9 ? 3 : 4;
            var cands = Defs.Where(d => !keep.Contains(d.Id) && Fits(d, k, p, climate)).ToList();
            var foreign = new HashSet<string>();
            foreach (Kingdom o in Economy.TradePartners(k)) if (G.Ok(o)) foreach (string id in Store.Set(o.data, KAdopted)) foreign.Add(id);
            while (keep.Count < max && cands.Count > 0)
            {
                var w = cands.Select(d => d.W * (foreign.Contains(d.Id) ? 1.6f : 1f) * (0.6f + UnityEngine.Random.value)).ToList();
                int i = G.Weighted(w);
                if (i < 0) break;
                keep.Add(cands[i].Id);
                cands.RemoveAt(i);
            }
            Store.Set(k.data, KAdopted, keep);
            Store.Put(k.data, KPicked, G.Year);
        }

        public static void Register()
        {
            Hub.On(Hub.WarEnded, e =>
            {
                if (e.Id == "draw" || e.Id == "Peace" || e.Id == "Nobody" || e.Id == "Merged") return;
                if (G.Ok(e.K))
                {
                    Store.Put(e.K.data, KWon, G.Year);
                    foreach (City c in G.Cities(e.K)) Moods.Apply(c, "civ_victory", 0.3f);
                    if (Fireworks(e.K) && Research.Era(e.K) >= 10) Show(e.K, e.K.capital, 4);
                    else if (Fireworks(e.K)) Show(e.K, e.K.capital, 1);
                }
                if (G.Ok(e.O)) foreach (City c in G.Cities(e.O)) Moods.Apply(c, "civ_mourning", 0.3f);
            });
            Hub.On(Hub.EraChanged, e =>
            {
                if (G.Ok(e.K)) Store.Put(e.K.data, KPicked, -999);
            });
        }

        public static void Show(Kingdom k, City c, int n)
        {
            if (c == null || c.buildings == null || c.buildings.Count == 0) return;
            for (int i = 0; i < n; i++)
            {
                Building b = c.buildings[UnityEngine.Random.Range(0, c.buildings.Count)];
                if (b == null || !b.isAlive() || b.isUnderConstruction() || b.current_tile == null) continue;
                EffectsLibrary.spawn("fx_fireworks", b.current_tile);
            }
            Hub.Raise(Hub.Fireworks, k, null, c, null, "", "", n);
        }

        private static bool Troubled(Kingdom k, KProfile p, City c)
        {
            if (c != null && Economy.IsFamine(c)) return true;
            foreach (global::War w in k.getWars())
            {
                if (w.isDefender(k) && w.getMainAttacker() != null && c != null && c.isInDanger()) return true;
            }
            return false;
        }

        public static void KingdomYear(Kingdom k, int yrs)
        {
            if (!On || !G.Ok(k) || Defs.Count == 0) return;
            KProfile p = Profile.Get(k);
            if (G.Year - Store.Int(k.data, KPicked, -999) >= 20) Pick(k, p);
            List<string> list = Adopted(k);
            if (list.Count == 0) return;
            var fx = new Dictionary<string, float>();
            City cap = k.capital;
            bool small = Troubled(k, p, cap);
            float joy = Math.Min(1.8f, Rarities.FestivalMult(k));
            int most = joy > 1f ? 4 : 3;
            int held = 0;
            foreach (string id in list)
            {
                FestivalDef d;
                if (!ById.TryGetValue(id, out d)) continue;
                if (d.Cond.Contains("victory") && G.Year - Store.Int(k.data, KWon, -999) > 2) continue;
                if (d.Cond.Contains("jubilee") && (!k.hasKing() || k.king.getAge() % 25 != 0 && G.Year % 25 != 0)) continue;
                if (d.Id == "great_games" && !Games(k)) continue;
                City c = PlaceFor(k, d);
                float scale = (small ? 0.4f : 1f) * joy;
                Hold(k, d, c, scale, fx);
                held++;
                if (held >= most) break;
            }
            Store.Map(k.data, KFx, fx);
            Store.Put(k.data, KFx + "_y", G.Year);
            Bonus.Dirty(k);
        }

        private static bool Games(Kingdom k)
        {
            SaveCustomData w = Store.World();
            if (w == null) return false;
            int last = Store.Int(w, WGames + "_y", -999);
            if (G.Year - last < 4) return false;
            if (!G.Chance(0.35f)) return false;
            Store.Put(w, WGames + "_y", G.Year);
            Store.Put(w, WGames, G.Id(k));
            return true;
        }

        private static City PlaceFor(Kingdom k, FestivalDef d)
        {
            List<City> cs = G.Cities(k);
            if (cs.Count == 0) return null;
            if (d.Cond.Contains("river_or_coast")) return cs.FirstOrDefault(c => c.hasBuildingType("type_docks")) ?? k.capital ?? cs[0];
            if (d.Big || d.Fireworks || cs.Count == 1) return k.capital ?? cs[0];
            return cs[UnityEngine.Random.Range(0, cs.Count)];
        }

        public static void Force(Kingdom k, City c, string id)
        {
            FestivalDef d;
            if (!G.Ok(k) || !ById.TryGetValue(id, out d)) return;
            Hold(k, d, c ?? k.capital, 1f, new Dictionary<string, float>());
        }

        private static void Hold(Kingdom k, FestivalDef d, City c, float scale, Dictionary<string, float> fx)
        {
            float v;
            if (d.Fx.TryGetValue("stability", out v)) Politics.AddStability(k, v * scale);
            if (d.Fx.TryGetValue("legit", out v)) Store.Put(k.data, Legitimacy.KLegit, G.Clamp(Legitimacy.Get(k) + v * scale, 0f, 1f));
            if (d.Fx.TryGetValue("mood", out v))
            {
                if (c != null) Moods.Apply(c, "civ_festival", v * scale);
                if (d.Big) foreach (City o in G.Cities(k)) if (o != c) Moods.Apply(o, "civ_festival", v * 0.5f * scale);
            }
            if (d.Fx.TryGetValue("food", out v) && c != null) Economy.TakeUpTo(c, "bread", (int)(-v * scale));
            if (d.Fx.TryGetValue("gold", out v) && c != null && v < 0f) Economy.TakeUpTo(c, "gold", (int)(-v * scale));
            foreach (string key in new[] { "trade", "culture", "prod", "research", "loyalty", "prestige" })
            {
                if (d.Fx.TryGetValue(key, out v)) Store.Add(fx, key, v * scale);
            }
            if (d.Fx.TryGetValue("aggression", out v)) Store.Add(fx, "attack", v * 2f);
            if (d.Fireworks && Fireworks(k) && scale >= 1f && c != null) Show(k, c, Research.Era(k) >= 10 ? 3 : 1);
            Hub.Raise(Hub.Festival, k, null, c, null, d.Id, d.Big ? "big" : "", d.Big ? 1f : 0f);
            string line = Txt.Has("civ_fest_" + d.Id + "_log") ? Txt.F("civ_fest_" + d.Id + "_log", G.Name(k), c != null ? G.Name(c) : G.Name(k)) : "";
            if (line.Length > 0) Internet.Official(k, "festival", line, d.Big);
        }

        [HarmonyPatch(typeof(BehLaunchFireworks), nameof(BehLaunchFireworks.execute))]
        private static class Gate
        {
            private static bool Prefix(Actor pActor, ref ai.behaviours.BehResult __result)
            {
                try
                {
                    Kingdom k = pActor != null ? pActor.kingdom : null;
                    if (!G.Ok(k) || !k.isCiv()) return true;
                    if (Fireworks(k)) return true;
                    pActor.finishStatusEffect("festive_spirit");
                    pActor.changeHappiness("just_made_peace");
                    __result = ai.behaviours.BehResult.Stop;
                    return false;
                }
                catch (Exception)
                {
                    return true;
                }
            }
        }
    }
}
