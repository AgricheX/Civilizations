using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static class Player
    {
        public const string KPlayer = "civ_player";
        private const string KTech = "civ_player_tech";
        private const string KMega = "civ_player_mega";
        private const string KTax = "civ_player_tax";
        private const string KBudget = "civ_player_budget";
        private const string KStance = "civ_player_stance";
        private const string KWins = "civ_player_wins";
        public static readonly string[] Layers = { "research", "politics", "economy", "war", "space" };
        public static readonly string[] Budgets = { "balanced", "military", "science", "growth", "trade" };
        public static readonly string[] Stances = { "balanced", "aggressive", "defensive" };
        public static readonly string[] Victories = { "conquest", "science", "culture", "diplomacy", "cosmic" };
        private static long _id = -1;
        private static Kingdom _k;
        private static int _frame = -1;
        public static bool Ordering;

        public static Kingdom K
        {
            get
            {
                if (_id < 0) return null;
                if (_frame == UnityEngine.Time.frameCount && (_k == null || G.Ok(_k))) return _k;
                _frame = UnityEngine.Time.frameCount;
                _k = null;
                foreach (Kingdom k in G.Kingdoms())
                {
                    if (k.data != null && k.data.id == _id)
                    {
                        _k = k;
                        break;
                    }
                }
                return _k;
            }
        }

        public static bool Active { get { return G.Ok(K); } }

        public static bool Is(Kingdom k)
        {
            return _id >= 0 && k != null && k.data != null && k.data.id == _id;
        }

        public static bool Auto(Kingdom k, string layer)
        {
            return k == null || k.data == null || Store.Bool(k.data, "civ_auto_" + layer, false);
        }

        public static void SetAuto(Kingdom k, string layer, bool on)
        {
            if (k != null && k.data != null) Store.Put(k.data, "civ_auto_" + layer, on);
        }

        public static bool Locks(Kingdom k, string layer)
        {
            return Is(k) && !Auto(k, layer);
        }

        public static void Load()
        {
            _id = -1;
            _k = null;
            _frame = -1;
            SaveCustomData d = Store.World();
            if (d == null) return;
            long id;
            if (long.TryParse(Store.Str(d, KPlayer), out id)) _id = id;
            if (!Active) _id = -1;
        }

        public static void Control(Kingdom k)
        {
            if (!G.Ok(k) || !k.isCiv()) return;
            _id = k.data.id;
            _frame = -1;
            SaveCustomData d = Store.World();
            if (d != null) Store.Put(d, KPlayer, _id.ToString());
            Sandbox.Disarm();
            Bonus.Clear();
            Fog.Version++;
            Notify.Show(Txt.F("civ_ctrl_start", G.Name(k)));
            Chronicle.Log("player_control", k, G.Name(k));
        }

        public static void Release()
        {
            _id = -1;
            _k = null;
            SaveCustomData d = Store.World();
            if (d != null) Store.Put(d, KPlayer, "");
            Bonus.Clear();
            Fog.Version++;
        }

        public static void Register()
        {
            Hub.On(Hub.KingdomGone, e =>
            {
                if (e.K == null || e.K.data == null || e.K.data.id != _id) return;
                string name = G.Name(e.K);
                Release();
                Notify.Show(Txt.F("civ_ctrl_lost", name));
            });
        }

        public static int Tax(Kingdom k)
        {
            return Store.Int(k.data, KTax, 1);
        }

        public static void SetTax(Kingdom k, int v)
        {
            Store.Put(k.data, KTax, Math.Max(0, Math.Min(3, v)));
            Bonus.Dirty(k);
        }

        public static string Budget(Kingdom k)
        {
            string b = Store.Str(k.data, KBudget);
            return b.Length > 0 ? b : "balanced";
        }

        public static void SetBudget(Kingdom k, string b)
        {
            Store.Put(k.data, KBudget, b);
            Bonus.Dirty(k);
        }

        public static string Stance(Kingdom k)
        {
            string b = Store.Str(k.data, KStance);
            return b.Length > 0 ? b : "balanced";
        }

        public static void SetStance(Kingdom k, string s)
        {
            Store.Put(k.data, KStance, s);
            Bonus.Dirty(k);
        }

        public static Dictionary<string, float> Effects(Kingdom k)
        {
            var fx = new Dictionary<string, float>();
            if (_id < 0 || k == null || k.data == null) return fx;
            if (!Is(k))
            {
                float ai = CivMod.C != null ? CivMod.C.CtrlAi : 0f;
                if (ai != 0f) foreach (string s in new[] { "research", "prod", "gold", "attack", "defense" }) fx[s] = ai;
                return fx;
            }
            float me = CivMod.C != null ? CivMod.C.CtrlPlayer : 0f;
            if (me != 0f) foreach (string s in new[] { "research", "prod", "gold", "attack", "defense" }) fx[s] = me;
            if (!Auto(k, "economy"))
            {
                switch (Tax(k))
                {
                    case 0: Add(fx, "gold:-20 stability:8 growth:5 loyalty:5"); break;
                    case 2: Add(fx, "gold:20 stability:-8 growth:-3"); break;
                    case 3: Add(fx, "gold:40 stability:-18 loyalty:-10 growth:-6"); break;
                }
                switch (Budget(k))
                {
                    case "military": Add(fx, "attack:10 defense:10 research:-5 growth:-3"); break;
                    case "science": Add(fx, "research:12 prod:-5 attack:-4"); break;
                    case "growth": Add(fx, "growth:10 health:5 attack:-5"); break;
                    case "trade": Add(fx, "trade:12 gold:6 defense:-5"); break;
                }
            }
            if (!Auto(k, "war"))
            {
                switch (Stance(k))
                {
                    case "aggressive": Add(fx, "attack:8 speed:4 defense:-4"); break;
                    case "defensive": Add(fx, "defense:12 attack:-4"); break;
                }
            }
            return fx;
        }

        private static void Add(Dictionary<string, float> fx, string s)
        {
            foreach (string p in s.Split(' '))
            {
                int i = p.IndexOf(':');
                Store.Add(fx, p.Substring(0, i), float.Parse(p.Substring(i + 1), System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        public static TechDef ResearchPick(Kingdom k)
        {
            if (!Locks(k, "research")) return null;
            TechDef t = Db.Tech(Store.Str(k.data, KTech));
            if (t == null) return null;
            Store.Put(k.data, KTech, "");
            KState s = Research.State(k);
            return s != null && !s.Known.Has(t.Index) && Research.Available(k, s, t, true) ? t : null;
        }

        public static string Queued(Kingdom k)
        {
            return Store.Str(k.data, KTech);
        }

        public static void Queue(Kingdom k, TechDef t)
        {
            if (t == null) return;
            TechDef cur = Research.Current(k);
            if (cur == null) Research.Choose(k, t);
            else Store.Put(k.data, KTech, t.Id);
        }

        public static List<TechDef> Cards(Kingdom k)
        {
            var list = new List<TechDef>();
            HashSet<string> seen = new HashSet<string>();
            TechDef cur = Research.Current(k);
            if (cur != null) seen.Add(cur.Id);
            var r = new System.Random((int)(k.data.id * 31 + (cur != null ? cur.Index : 0)));
            List<TechDef> pool = Research.Candidates(k);
            int guard = 0;
            while (list.Count < 3 && pool.Count > 0 && guard++ < 40)
            {
                TechDef t = pool[r.Next(0, pool.Count)];
                if (seen.Add(t.Id)) list.Add(t);
            }
            return list;
        }

        public static MegaDef MegaPick(Kingdom k)
        {
            if (!Locks(k, "space")) return null;
            return Megastructures.Def(Store.Str(k.data, KMega));
        }

        public static void ClearMega(Kingdom k)
        {
            Store.Put(k.data, KMega, "");
        }

        public static void QueueMega(Kingdom k, MegaDef d)
        {
            Store.Put(k.data, KMega, d != null ? d.Id : "");
        }

        public static void DeclareWar(Kingdom o)
        {
            Kingdom k = K;
            if (!G.Ok(k) || !G.Ok(o) || o == k || World.world.wars.isInWarWith(k, o)) return;
            Ordering = true;
            try
            {
                World.world.diplomacy.startWar(k, o, WarTypeLibrary.normal);
            }
            finally
            {
                Ordering = false;
            }
        }

        public static bool OfferPeace(Kingdom o)
        {
            Kingdom k = K;
            if (!G.Ok(k) || !G.Ok(o)) return false;
            global::War war = null;
            foreach (global::War w in World.world.wars.getWars(k))
            {
                if (w == null || w.isRekt()) continue;
                if (w.getOppositeSideKingdom(k).Contains(o))
                {
                    war = w;
                    break;
                }
            }
            if (war == null) return false;
            float mine = Strength(k), theirs = Strength(o);
            float chance = 0.25f + 0.5f * mine / Math.Max(1f, mine + theirs);
            if (Profile.Get(o).Fam.Contains("military")) chance -= 0.15f;
            chance += Statecraft.PeaceBonus(k, o);
            if (!G.Chance(chance))
            {
                Notify.Show(Txt.F("civ_ctrl_peace_no", G.Name(o)));
                return false;
            }
            World.world.wars.endWar(war, WarWinner.Peace);
            Notify.Show(Txt.F("civ_ctrl_peace_yes", G.Name(o)));
            return true;
        }

        public static float Strength(Kingdom k)
        {
            float s = 0f;
            foreach (Actor a in k.units) if (G.Ok(a) && a.isWarrior()) s += 1f;
            return s + k.getPopulationPeople() * 0.05f;
        }

        public static void Year()
        {
            Kingdom k = K;
            if (!G.Ok(k))
            {
                if (_id >= 0 && G.Kingdoms().All(x => x.data.id != _id)) Release();
                return;
            }
            SaveCustomData d = Store.World();
            if (d == null) return;
            HashSet<string> won = Store.Set(d, KWins);
            foreach (string v in Victories)
            {
                if (won.Contains(v) || !Won(k, v)) continue;
                won.Add(v);
                Store.Set(d, KWins, won);
                Notify.Show(Txt.F("civ_ctrl_victory", Txt.T("civ_ctrl_vic_" + v)));
                Chronicle.Log("player_victory", k, G.Name(k), Txt.T("civ_ctrl_vic_" + v));
            }
        }

        public static bool HasWon(string v)
        {
            SaveCustomData d = Store.World();
            return d != null && Store.Set(d, KWins).Contains(v);
        }

        public static float VictoryProgress(Kingdom k, string v)
        {
            switch (v)
            {
                case "conquest":
                    {
                        int mine = G.Cities(k).Count(), all = G.Cities().Count(c => c.kingdom != null && c.kingdom.isCiv());
                        return all <= 0 ? 0f : Math.Min(1f, mine / (all * 0.6f));
                    }
                case "science":
                    return Math.Min(1f, Research.Era(k) / (float)Db.LastEra);
                case "culture":
                    {
                        float mine = Bonus.Sum(k, "prestige") + Bonus.Sum(k, "culture");
                        float best = 0f;
                        foreach (Kingdom o in G.Kingdoms()) if (o != k && o.isCiv()) best = Math.Max(best, Bonus.Sum(o, "prestige") + Bonus.Sum(o, "culture"));
                        return mine <= 0f ? 0f : Math.Min(1f, mine / Math.Max(1f, best * 2f));
                    }
                case "diplomacy":
                    {
                        WorldOrder o = WorldOrders.Active.FirstOrDefault(x => x.Leader == k.data.id);
                        if (o == null) return 0f;
                        return Math.Min(1f, 0.5f + (G.Year - o.Since) / 100f);
                    }
                case "cosmic":
                    return Cosmos.St != null && Cosmos.St.Ended ? 1f : Math.Min(0.99f, Kardashev.Type(k) / 5f);
            }
            return 0f;
        }

        private static bool Won(Kingdom k, string v)
        {
            return VictoryProgress(k, v) >= 1f;
        }
    }
}
