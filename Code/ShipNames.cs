using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;

namespace Civilizations
{
    internal static class ShipNames
    {
        private const string KNamed = "civ_ship_named";
        private static float _tick;

        private static readonly string[] VirtuesPt = { "Valente", "Destemido", "Glorioso", "Invencível", "Audaz", "Resoluto", "Vigilante", "Intrépido", "Triunfo", "Esperança", "Liberdade", "Vitória", "Defensor", "Guardião", "Indomável", "Implacável", "Constante", "Leal", "Soberano", "Tenaz" };
        private static readonly string[] VirtuesEn = { "Valiant", "Dauntless", "Glorious", "Invincible", "Audacious", "Resolute", "Vigilant", "Intrepid", "Triumph", "Hope", "Liberty", "Victory", "Defender", "Guardian", "Indomitable", "Relentless", "Constant", "Loyal", "Sovereign", "Tenacious" };
        private static readonly string[] SeaPt = { "Tubarão", "Barracuda", "Orca", "Moreia", "Arraia", "Marlim", "Narval", "Lula", "Cachalote", "Peixe-Espada", "Polvo", "Golfinho", "Tainha", "Garoupa", "Robalo", "Anchova" };
        private static readonly string[] SeaEn = { "Shark", "Barracuda", "Orca", "Moray", "Stingray", "Marlin", "Narwhal", "Squid", "Sperm Whale", "Swordfish", "Octopus", "Dolphin", "Mullet", "Grouper", "Sea Bass", "Bluefish" };
        private static readonly string[] TradePt = { "Fortuna", "Estrela", "Brisa", "Aurora", "Pérola", "Sereia", "Andorinha", "Gaivota", "Bonança", "Prosperidade" };
        private static readonly string[] TradeEn = { "Fortune", "Star", "Breeze", "Aurora", "Pearl", "Mermaid", "Swallow", "Seagull", "Fair Wind", "Prosperity" };

        private static string Initials(Kingdom k)
        {
            string n = k.data.name ?? "";
            var sb = new StringBuilder();
            foreach (string w in n.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries))
            {
                char c = w[0];
                if (char.IsLetter(c)) sb.Append(char.ToUpperInvariant(c));
                if (sb.Length >= 3) break;
            }
            if (sb.Length == 0) sb.Append('R');
            return sb.ToString();
        }

        private static string Code(string duty, UnitDef d)
        {
            switch (duty)
            {
                case "ship_carrier": return "CV";
                case "ship_escort": return d.Id == "civ_missile_destroyer" ? "DDG" : "DD";
                case "ship_submarine": return "SS";
                case "ship_nuclear_submarine": return "SSN";
                default: return "BB";
            }
        }

        private static List<string> Rulers(Kingdom k)
        {
            var list = new List<string>();
            if (k.data.past_rulers != null) foreach (LeaderEntry e in k.data.past_rulers) if (e != null && !string.IsNullOrEmpty(e.name)) list.Add(e.name);
            if (k.king != null) list.Add(k.king.getName());
            return list;
        }

        private static List<string> Cities(Kingdom k)
        {
            return G.Cities(k).Select(c => c.data.name).Where(x => !string.IsNullOrEmpty(x)).ToList();
        }

        private static string Pick(List<string> pool, HashSet<string> used)
        {
            if (pool.Count == 0) return null;
            foreach (string n in pool.OrderBy(x => UnityEngine.Random.value)) if (!used.Contains(n)) return n;
            string b = pool[UnityEngine.Random.Range(0, pool.Count)];
            for (int i = 2; i < 30; i++)
            {
                string v = b + " " + Roman(i);
                if (!used.Contains(v)) return v;
            }
            return b;
        }

        private static string Roman(int n)
        {
            string[] r = { "", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            if (n <= 10) return r[n];
            return "X" + Roman(n - 10);
        }

        private static HashSet<string> Used(Kingdom k)
        {
            var set = new HashSet<string>();
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "ship") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null) continue;
                foreach (Actor s in asset.units) if (G.Ok(s) && s.kingdom == k && Store.Bool(s.data, KNamed)) set.Add(Store.Str(s.data, "civ_ship_base"));
            }
            return set;
        }

        public static void Name(Actor s, UnitDef d)
        {
            Kingdom k = s.kingdom;
            if (k == null || k.data == null) return;
            bool pt = G.Pt;
            string duty = VehicleRoster.Duty(d);
            int era = Research.Era(k);
            HashSet<string> used = Used(k);
            List<string> pool;
            if (duty == "ship_trade" || duty == "ship_fish")
            {
                string city = s.city != null ? s.city.data.name : Cities(k).FirstOrDefault() ?? "";
                pool = (pt ? TradePt : TradeEn).Select(v => city.Length > 0 ? (pt ? v + " de " + city : v + " of " + city) : v).ToList();
            }
            else if (duty == "ship_carrier") pool = Rulers(k).Count > 0 ? Rulers(k) : Cities(k);
            else if (duty == "ship_submarine" || duty == "ship_nuclear_submarine") pool = (pt ? SeaPt : SeaEn).ToList();
            else if (duty == "ship_escort") pool = Rulers(k).Concat(pt ? VirtuesPt : VirtuesEn).ToList();
            else if (era < 10) pool = (pt ? VirtuesPt : VirtuesEn).ToList();
            else pool = Cities(k).Count > 0 ? Cities(k) : (pt ? VirtuesPt : VirtuesEn).ToList();
            string baseName = Pick(pool, used) ?? (pt ? "Navio" : "Ship");
            string full = baseName;
            if (duty == "ship_fish")
            {
                if (era >= 11) full = (pt ? "B/P " : "F/V ") + baseName;
            }
            else if (duty == "ship_trade")
            {
                if (era >= 11) full = (pt ? "N/M " : "M/V ") + baseName;
            }
            else
            {
                if (era >= 9) full = Initials(k) + "N " + baseName;
                if (era >= 12)
                {
                    string code = Code(duty, d);
                    string key = "civ_hull_" + code;
                    int n = Store.Int(k.data, key, 0) + 1;
                    Store.Put(k.data, key, n);
                    full += " (" + code + "-" + n + ")";
                }
            }
            s.setName(full, false);
            Store.Put(s.data, "civ_ship_base", baseName);
            Store.Put(s.data, KNamed, true);
        }

        public static void Tick(float dt)
        {
            _tick += dt;
            if (_tick < 3f) return;
            _tick = 0f;
            int budget = 20;
            foreach (UnitDef d in Db.Units)
            {
                if (d.Cls != "ship") continue;
                ActorAsset asset = AssetManager.actor_library.get(d.Id);
                if (asset == null || asset.units == null || asset.units.Count == 0) continue;
                foreach (Actor s in asset.units.ToList())
                {
                    if (budget <= 0) return;
                    if (!G.Ok(s) || s.kingdom == null || !s.kingdom.isCiv() || Store.Bool(s.data, KNamed)) continue;
                    budget--;
                    Name(s, d);
                }
            }
        }

        [HarmonyPatch(typeof(UnitWindow), "checkMainTabTitle")]
        private static class Title
        {
            private static void Postfix(UnitWindow __instance)
            {
                Actor a = __instance.actor;
                if (a == null || a.asset == null || !a.asset.is_boat || !Db.UnitById.ContainsKey(a.asset.id)) return;
                __instance._main_tab_title.setKeyAndUpdate(a.asset.getLocaleID());
            }
        }
    }
}
