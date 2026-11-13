using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Civilizations
{
    internal class Company
    {
        public int Id;
        public string Name = "";
        public string Sector = "";
        public string Home = "";
        public string HomeName = "";
        public string Hq = "";
        public string Founder = "";
        public string FounderName = "";
        public string Ceo = "";
        public string CeoName = "";
        public string Clan = "";
        public int Founded;
        public float Cash;
        public float Value;
        public int Staff;
        public string Status = "active";
        public string Route = "";
        public List<string> Branches = new List<string>();
        public List<string> Patents = new List<string>();
        public List<float> History = new List<float>();
        public int Red;
        public int Color;
        public string Logo = "";
        public float Rd;
    }

    internal static class Companies
    {
        private const string KData = "civ_companies";
        private static List<Company> _list = new List<Company>();
        private static int _next = 1;
        private static readonly Dictionary<string, List<Company>> _byHome = new Dictionary<string, List<Company>>();

        private static readonly Dictionary<string, Dictionary<string, float>> SectorEff = new Dictionary<string, Dictionary<string, float>>
        {
            { "agro", new Dictionary<string, float> { { "food", 3f } } },
            { "mining", new Dictionary<string, float> { { "prod", 3f } } },
            { "textile", new Dictionary<string, float> { { "prod", 2f }, { "trade", 1f } } },
            { "trade", new Dictionary<string, float> { { "trade", 3f }, { "gold", 1f } } },
            { "shipping", new Dictionary<string, float> { { "naval", 2f }, { "trade", 2f } } },
            { "finance", new Dictionary<string, float> { { "gold", 4f } } },
            { "steel", new Dictionary<string, float> { { "prod", 3f }, { "attack", 1f } } },
            { "rail", new Dictionary<string, float> { { "trade", 3f } } },
            { "energy", new Dictionary<string, float> { { "prod", 3f } } },
            { "oil", new Dictionary<string, float> { { "prod", 2f }, { "gold", 2f } } },
            { "auto", new Dictionary<string, float> { { "trade", 2f }, { "prod", 1f } } },
            { "arms", new Dictionary<string, float> { { "attack", 3f }, { "defense", 2f } } },
            { "media", new Dictionary<string, float> { { "culture", 4f } } },
            { "pharma", new Dictionary<string, float> { { "health", 4f } } },
            { "aero", new Dictionary<string, float> { { "air", 4f }, { "trade", 1f } } },
            { "space", new Dictionary<string, float> { { "prestige", 5f }, { "research", 2f } } },
            { "tech", new Dictionary<string, float> { { "research", 4f } } },
            { "biotech", new Dictionary<string, float> { { "health", 2f }, { "research", 2f } } }
        };

        private static readonly Dictionary<string, string> SectorCat = new Dictionary<string, string>
        {
            { "agro", "agri" }, { "mining", "metal" }, { "textile", "craft" }, { "trade", "econ" }, { "shipping", "naval" }, { "finance", "econ" },
            { "steel", "metal" }, { "rail", "trans" }, { "energy", "energy" }, { "oil", "energy" }, { "auto", "trans" }, { "arms", "mil" },
            { "media", "comm" }, { "pharma", "med" }, { "aero", "air" }, { "space", "space" }, { "tech", "comp" }, { "biotech", "med" }
        };

        public static IEnumerable<Company> All()
        {
            return _list;
        }

        public static List<Company> Active()
        {
            var l = new List<Company>();
            foreach (Company c in _list) if (c.Status != "bankrupt") l.Add(c);
            return l;
        }

        public static List<Company> Of(Kingdom k)
        {
            List<Company> l;
            if (k == null || k.data == null) return new List<Company>();
            return _byHome.TryGetValue(k.data.id.ToString(), out l) ? l : new List<Company>();
        }

        public static void Load()
        {
            _list = new List<Company>();
            _next = 1;
            SaveCustomData d = Store.World();
            string s = d != null ? Store.Str(d, KData) : "";
            if (s.Length > 0)
            {
                try
                {
                    _list = JsonConvert.DeserializeObject<List<Company>>(s) ?? new List<Company>();
                }
                catch (Exception)
                {
                    _list = new List<Company>();
                }
            }
            foreach (Company c in _list) _next = Math.Max(_next, c.Id + 1);
            Reindex();
        }

        public static void Save()
        {
            SaveCustomData d = Store.World();
            if (d == null) return;
            if (_list.Count > 400)
            {
                _list.RemoveAll(c => c.Status == "bankrupt" && G.Year - c.Founded > 50);
            }
            Store.Put(d, KData, JsonConvert.SerializeObject(_list));
        }

        private static void Reindex()
        {
            _byHome.Clear();
            foreach (Company c in _list)
            {
                if (c.Status == "bankrupt") continue;
                List<Company> l;
                if (!_byHome.TryGetValue(c.Home, out l)) _byHome[c.Home] = l = new List<Company>();
                l.Add(c);
            }
        }

        public static int OwnedBy(Clan clan)
        {
            if (clan == null || clan.data == null) return 0;
            string id = clan.data.id.ToString();
            int n = 0;
            foreach (Company c in _list) if (c.Clan == id && c.Status == "active") n++;
            return n;
        }

        public static int Tier(Company c)
        {
            return c.Value > 4000f ? 3 : c.Value > 900f ? 2 : 1;
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var m = new Dictionary<string, float>();
            foreach (Company c in Of(k))
            {
                Dictionary<string, float> e;
                if (!SectorEff.TryGetValue(c.Sector, out e)) continue;
                float t = Tier(c) * (c.Status == "state" ? 0.8f : 1f);
                foreach (var kv in e) Store.Add(m, kv.Key, kv.Value * t * 0.5f);
            }
            if (k != null && k.data != null)
            {
                string id = k.data.id.ToString();
                foreach (Company c in _list)
                {
                    if (c.Status != "active" || c.Home == id || !c.Branches.Contains(id)) continue;
                    Store.Add(m, "trade", 1f);
                }
            }
            return m;
        }

        public static float ResearchBoost(Kingdom k)
        {
            float b = 0f;
            foreach (Company c in Of(k)) b += c.Rd * 0.0004f;
            return Math.Min(0.4f, b);
        }

        public static float RareResearch(Kingdom k)
        {
            float b = 0f;
            foreach (Company c in Of(k))
            {
                if (c.Sector == "tech" || c.Sector == "space" || c.Sector == "biotech" || c.Sector == "energy" || c.Sector == "aero") b += Tier(c);
            }
            return b;
        }

        public static float RouteSupport(Kingdom k, string route)
        {
            float s = 0f;
            foreach (Company c in Of(k)) if (c.Route == route) s += 0.4f * Tier(c);
            return s;
        }

        public static List<Company> SpaceLaunchers(Kingdom k)
        {
            var l = new List<Company>();
            foreach (Company c in Of(k)) if (c.Sector == "space" && c.Status != "bankrupt") l.Add(c);
            return l;
        }

        public static bool HqWanted(City c)
        {
            if (!G.Ok(c)) return false;
            string id = G.Id(c);
            int hqs = c.countBuildingsType("type_civ_company_hq");
            int want = 0;
            foreach (Company co in _list) if (co.Hq == id && co.Status != "bankrupt" && Tier(co) >= 2) want++;
            return hqs < Math.Min(2, want);
        }

        public static Company Get(int id)
        {
            foreach (Company c in _list) if (c.Id == id) return c;
            return null;
        }

        public static void Year()
        {
            foreach (Kingdom k in G.Kingdoms()) Found(k);
            _rivals.Clear();
            foreach (Company c in _list)
            {
                if (c.Status == "bankrupt") continue;
                Sane(c);
                string key = c.Home + "|" + c.Sector;
                float v;
                _rivals.TryGetValue(key, out v);
                _rivals[key] = v + Math.Max(1f, c.Value);
            }
            foreach (Company c in _list.ToArray())
            {
                if (c.Status == "bankrupt") continue;
                Operate(c);
                Sane(c);
            }
            Mergers();
            Reindex();
            if (_list.Count > 400) _list.RemoveAll(c => c.Status == "bankrupt" && G.Year - c.Founded > 50);
        }

        private static readonly Dictionary<string, float> _rivals = new Dictionary<string, float>();
        public const float MaxValue = 1e9f;

        private static void Sane(Company c)
        {
            if (float.IsNaN(c.Value) || float.IsInfinity(c.Value)) c.Value = MaxValue * 0.5f;
            if (float.IsNaN(c.Cash) || float.IsInfinity(c.Cash)) c.Cash = 0f;
            c.Value = Mathf.Clamp(c.Value, 0f, MaxValue);
            c.Cash = Mathf.Clamp(c.Cash, -MaxValue, MaxValue);
            if (float.IsNaN(c.Rd) || float.IsInfinity(c.Rd)) c.Rd = 0f;
            c.Rd = Mathf.Clamp(c.Rd, 0f, MaxValue);
        }

        private static List<string> Sectors(Kingdom k)
        {
            var l = new List<string>();
            foreach (var kv in Db.Sectors) if (Research.Has(k, kv.Value)) l.Add(kv.Key);
            return l;
        }

        private static void Found(Kingdom k)
        {
            bool guilds = Research.HasFeature(k, "guilds");
            bool modern = Research.HasFeature(k, "companies");
            if (!guilds && !modern) return;
            string econ = Policies.Id(k, "econ");
            float rate = 0.015f * Math.Max(1, k.countCities());
            if (modern) rate *= 2f;
            if (Research.HasFeature(k, "corporations")) rate *= 1.5f;
            if (econ == "laissez_faire" || econ == "corporatist" || econ == "digital_economy") rate *= 2f;
            if (econ == "planned") rate *= 0.15f;
            if (econ == "manorialism") rate *= 0.5f;
            if (Of(k).Count >= 6 + k.countCities()) rate *= 0.2f;
            if (!G.Chance(rate)) return;
            List<string> sectors = Sectors(k);
            if (sectors.Count == 0) return;
            Actor founder = null;
            int best = -1;
            foreach (City c in G.Cities(k))
            {
                foreach (Actor a in c.units)
                {
                    if (!G.Ok(a) || !a.isAdult() || !Xeno.CanHoldOffice(a)) continue;
                    int score = a.money + (Roles.Of(a) == Roles.Merchant ? 60 : 0) + (Roles.Of(a) == Roles.Scientist ? 40 : 0) + G.Range(0, 40);
                    if (score > best)
                    {
                        best = score;
                        founder = a;
                    }
                }
            }
            if (founder == null) return;
            var w = new List<float>();
            foreach (string s in sectors)
            {
                float x = 1f + Db.EraOf(SectorTech(s)) * 0.25f;
                if (Roles.Of(founder) == Roles.Scientist && (s == "tech" || s == "biotech" || s == "pharma" || s == "space" || s == "aero")) x += 3f;
                if (Roles.Of(founder) == Roles.Merchant && (s == "trade" || s == "shipping" || s == "finance")) x += 2f;
                foreach (Company o in Of(k)) if (o.Sector == s) x *= 0.5f;
                w.Add(x);
            }
            string sector = sectors[G.Weighted(w)];
            var co = new Company
            {
                Id = _next++,
                Sector = sector,
                Home = k.data.id.ToString(),
                HomeName = G.Name(k),
                Hq = founder.city != null ? founder.city.data.id.ToString() : "",
                Founder = founder.data.id.ToString(),
                FounderName = founder.getName(),
                Ceo = founder.data.id.ToString(),
                CeoName = founder.getName(),
                Clan = founder.clan != null && founder.clan.data != null ? founder.clan.data.id.ToString() : "",
                Founded = G.Year,
                Cash = 60f + founder.money * 0.6f,
                Value = 100f,
                Staff = 5,
                Route = Research.Route(k),
                Color = G.Range(0, 12),
                Status = econ == "planned" ? "state" : "active"
            };
            co.Name = Names.Company(co, founder, k);
            co.Logo = sector;
            founder.data.money = Math.Max(0, founder.money / 3);
            _list.Add(co);
            Internet.OnCompanyFounded(co, k, founder);
            Chronicle.Log("company_founded", k, co.Name, G.Name(k), null, k, null, founder.city, null, false);
        }

        public static string SectorTech(string sector)
        {
            foreach (var kv in Db.Sectors) if (kv.Key == sector) return kv.Value;
            return "";
        }

        private static void Operate(Company c)
        {
            Kingdom k = G.KingdomById(c.Home);
            if (k == null)
            {
                Bankrupt(c, "home_lost");
                return;
            }
            c.HomeName = G.Name(k);
            Actor ceo = G.ActorById(c.Ceo);
            if (ceo == null)
            {
                ceo = Successor(c, k);
                if (ceo != null)
                {
                    c.Ceo = ceo.data.id.ToString();
                    c.CeoName = ceo.getName();
                    Internet.OnCeoChange(c, ceo);
                }
            }
            int era = Research.Era(k);
            float stab = Politics.Stability(k);
            float demand = 1f + era * 0.35f + Math.Min(6, k.countCities()) * 0.2f;
            float mult = Bonus.Mult(k, "trade") * (0.6f + stab * 0.8f);
            if (World.world.wars.hasWars(k)) mult *= c.Sector == "arms" ? 1.6f : 0.8f;
            if (c.Sector == "space" && !Research.HasFeature(k, "rocket")) mult *= 0.5f;
            string econ = Policies.Id(k, "econ");
            if (econ == "laissez_faire" || econ == "corporatist") mult *= 1.2f;
            if (econ == "welfare") mult *= 0.9f;
            float market = (G.Pop(k) * (2f + era * 0.8f) + k.countCities() * 60f) * mult;
            float rivals;
            if (!_rivals.TryGetValue(c.Home + "|" + c.Sector, out rivals) || rivals < 1f) rivals = Math.Max(1f, c.Value);
            float share = Mathf.Clamp01(Math.Max(1f, c.Value) / rivals);
            float organic = c.Value * 0.18f * mult * demand / (1f + era * 0.3f);
            float revenue = Math.Min(organic, market * share) + 6f + Math.Min(12, c.Branches.Count) * 4f * demand;
            float scale = 0.05f + 0.03f * Mathf.Log10(1f + c.Value / 1000f);
            float costs = c.Staff * 0.6f + c.Value * scale;
            float profit = revenue - costs + UnityEngine.Random.Range(-0.15f, 0.15f) * revenue;
            if (profit > 0f)
            {
                float tax = profit * (econ == "welfare" || econ == "planned" ? 0.35f : econ == "laissez_faire" ? 0.1f : 0.2f);
                City home = k.capital;
                if (home != null && tax >= 1f) Economy.Give(home, "gold", (int)Math.Min(500f, tax * 0.1f));
                profit -= tax;
            }
            c.Value *= 0.97f;
            c.Cash += profit;
            float rd = 0f;
            if (c.Sector == "tech" || c.Sector == "space" || c.Sector == "biotech" || c.Sector == "pharma" || c.Sector == "aero" || c.Sector == "energy" || c.Sector == "auto" || c.Sector == "arms")
            {
                rd = Math.Max(0f, c.Cash * 0.15f);
                c.Cash -= rd;
                c.Rd = c.Rd * 0.8f + rd;
                Invent(c, k);
            }
            if (profit > 0f)
            {
                float dividend = profit * 0.3f;
                c.Cash -= dividend;
                c.Value += (profit - dividend) * 0.8f + rd * 0.3f;
                c.Staff = Math.Max(3, Math.Min(5000, (int)(c.Value / 40f)));
                if (ceo != null) ceo.data.money = (int)Math.Min(1000000f, ceo.data.money + Math.Min(400f, dividend * 0.3f));
                c.Red = 0;
            }
            else
            {
                c.Value = Math.Max(10f, c.Value + profit);
                c.Red++;
            }
            c.History.Add(c.Value);
            while (c.History.Count > 40) c.History.RemoveAt(0);
            if (c.Cash < -c.Value * 0.3f || c.Red >= 5)
            {
                if (econ == "welfare" || econ == "mixed_economy" ? G.Chance(0.5f) : false)
                {
                    c.Cash = c.Value * 0.2f;
                    c.Red = 0;
                    Internet.OnBailout(c, k);
                }
                else
                {
                    Bankrupt(c, "debt");
                    return;
                }
            }
            if (c.Status == "active" && (econ == "planned" || Policies.Id(k, "ideo") == "communism") && G.Chance(0.25f))
            {
                c.Status = "state";
                Internet.OnNationalized(c, k);
                Chronicle.Log("nationalized", k, c.Name, G.Name(k), null, k, null, null, null, false);
            }
            else if (c.Status == "state" && (econ == "laissez_faire" || econ == "mixed_economy") && G.Chance(0.1f))
            {
                c.Status = "active";
                Internet.OnPrivatized(c, k);
            }
            Expand(c, k);
        }

        private static Actor Successor(Company c, Kingdom k)
        {
            Clan clan = null;
            foreach (Clan cl in Clans.All()) if (cl.data.id.ToString() == c.Clan) clan = cl;
            if (clan != null)
            {
                foreach (Actor a in clan.units) if (G.Ok(a) && a.isAdult() && Xeno.CanHoldOffice(a)) return a;
            }
            City hq = G.CityById(c.Hq);
            if (hq == null) hq = k.capital;
            if (hq == null) return null;
            foreach (Actor a in hq.units) if (G.Ok(a) && a.isAdult() && Xeno.CanHoldOffice(a) && G.Chance(0.3f)) return a;
            return null;
        }

        private static void Invent(Company c, Kingdom k)
        {
            if (c.Rd < 40f) return;
            string cat;
            SectorCat.TryGetValue(c.Sector, out cat);
            KState s = Research.State(k);
            int era = Research.Era(k);
            float p = Mathf.Sqrt(c.Rd) * 0.01f;
            foreach (TechDef t in Db.Techs)
            {
                if (t.Era > era + 1 || s.Known.Has(t.Index)) continue;
                if (t.Cat != cat && !(c.Sector == "tech" && t.Cat == "sci")) continue;
                if (!t.Rare && !(!string.IsNullOrEmpty(t.Route) && t.Route == c.Route)) continue;
                if (t.Rare && !s.Found.Has(t.Index))
                {
                    if (G.Chance(p * 0.5f)) Research.Discover(k, t);
                    continue;
                }
                if (!Research.Available(k, s, t, true)) continue;
                if (G.Chance(p * 0.3f))
                {
                    c.Patents.Add(t.Id);
                    c.Value += 200f;
                    Research.Learn(k, t, "company");
                    Internet.OnPatent(c, k, t);
                    return;
                }
            }
        }

        private static void Expand(Company c, Kingdom k)
        {
            if (c.Status != "active" || c.Value < 600f) return;
            if (!Research.HasFeature(k, "multinational") && !(Research.HasFeature(k, "companies") && c.Sector == "shipping")) return;
            if (!G.Chance(0.1f)) return;
            foreach (Kingdom o in Economy.TradePartners(k))
            {
                if (o == null || o == k || World.world.wars.isInWarWith(k, o)) continue;
                string oid = o.data.id.ToString();
                if (c.Branches.Contains(oid)) continue;
                if (Policies.Id(o, "econ") == "planned") continue;
                c.Branches.Add(oid);
                Internet.OnBranch(c, k, o);
                return;
            }
        }

        private static void Mergers()
        {
            var bySector = new Dictionary<string, List<Company>>();
            foreach (Company c in _list)
            {
                if (c.Status != "active") continue;
                string key = c.Home + ":" + c.Sector;
                List<Company> l;
                if (!bySector.TryGetValue(key, out l)) bySector[key] = l = new List<Company>();
                l.Add(c);
            }
            foreach (var kv in bySector)
            {
                if (kv.Value.Count < 2 || !G.Chance(0.05f)) continue;
                kv.Value.Sort((a, b) => b.Value.CompareTo(a.Value));
                Company big = kv.Value[0];
                Company small = kv.Value[kv.Value.Count - 1];
                big.Value += small.Value * 0.8f;
                big.Cash += small.Cash;
                small.Status = "bankrupt";
                foreach (string b in small.Branches) if (!big.Branches.Contains(b)) big.Branches.Add(b);
                big.Patents.AddRange(small.Patents);
                Internet.OnMerger(big, small);
                Chronicle.Log("merger", G.KingdomById(big.Home), big.Name, small.Name, null, null, null, null, null, false);
            }
        }

        private static void Bankrupt(Company c, string reason)
        {
            c.Status = "bankrupt";
            c.Value = 0f;
            Internet.OnBankrupt(c, reason);
            if (c.Value > 500f) Chronicle.Log("company_bankrupt", G.KingdomById(c.Home), c.Name, null, null, null, null, null, null, false);
        }

        public static void OnConquest(Kingdom winner, Kingdom loser)
        {
            if (winner == null || loser == null) return;
            string lid = loser.data.id.ToString();
            foreach (Company c in _list)
            {
                if (c.Home != lid || c.Status == "bankrupt") continue;
                if (loser.countCities() <= 0)
                {
                    c.Home = winner.data.id.ToString();
                    c.HomeName = G.Name(winner);
                }
            }
            Reindex();
        }

        public static List<Company> Ranking(int max)
        {
            var l = Active();
            l.Sort((a, b) => b.Value.CompareTo(a.Value));
            if (l.Count > max) l.RemoveRange(max, l.Count - max);
            return l;
        }

        public static List<Actor> Richest(int max)
        {
            var l = new List<Actor>();
            foreach (Kingdom k in G.Kingdoms())
            {
                foreach (City c in G.Cities(k))
                {
                    foreach (Actor a in c.units) if (G.Ok(a) && a.money > 50) l.Add(a);
                }
            }
            l.Sort((a, b) => b.money.CompareTo(a.money));
            if (l.Count > max) l.RemoveRange(max, l.Count - max);
            return l;
        }
    }
}
