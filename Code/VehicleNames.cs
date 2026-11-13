using System;
using System.Text;

namespace Civilizations
{
    internal static class VehicleNames
    {
        public static void Apply(Actor v)
        {
            try
            {
                if (!G.Ok(v) || v.asset == null || v.data == null) return;
                UnitDef d;
                if (!Db.UnitById.TryGetValue(v.asset.id, out d)) return;
                Kingdom k = v.kingdom;
                if (k == null || k.data == null || !k.isCiv()) return;
                if (d.Cls == "ship" || d.Cls == "space")
                {
                    ShipNames.Name(v, d);
                    return;
                }
                string n = d.Cls == "soldier" ? Soldier(v) : Machine(v, d, k);
                if (!string.IsNullOrEmpty(n)) v.setName(n, false);
            }
            catch (Exception e)
            {
                CivMod.Log("vehicle names: " + e.Message);
            }
        }

        private static string Soldier(Actor v)
        {
            string n = RealNames.Person(v);
            if (!string.IsNullOrEmpty(n)) return n.Trim();
            Culture c = v.city != null ? v.city.getCulture() : null;
            if (c == null && v.kingdom != null) c = v.kingdom.getCulture();
            if (c == null) return null;
            OnomasticsData o = c.getOnomasticData(MetaType.Unit);
            return o != null ? o.generateName(v.data.sex, 0, v.data.id) : null;
        }

        private static string[] Pool(string key)
        {
            return Txt.T(key).Split(new[] { ',', '，', '、' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private static string Pick(string key, Actor v)
        {
            string[] p = Pool(key);
            if (p.Length == 0) return "";
            return p[(int)((v.data.id * 31 + 7) % p.Length + p.Length) % p.Length].Trim();
        }

        private static int Serial(Kingdom k, string group)
        {
            string key = "civ_vserial_" + group;
            int n = Store.Int(k.data, key, 0) + 1;
            Store.Put(k.data, key, n);
            return n;
        }

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
            if (sb.Length == 0) sb.Append('K');
            return sb.ToString();
        }

        private static string Home(Actor v, Kingdom k)
        {
            if (v.city != null && v.city.data != null && !string.IsNullOrEmpty(v.city.data.name)) return v.city.data.name;
            if (k.capital != null && k.capital.data != null) return k.capital.data.name;
            return k.data.name;
        }

        private static string OfCity(string what, Actor v, Kingdom k)
        {
            return Txt.F("civ_vn_of", what, Home(v, k));
        }

        private static string Machine(Actor v, UnitDef d, Kingdom k)
        {
            int era = Research.Era(k);
            string a = d.Art ?? "";
            if (d.Cls == "air")
            {
                if (a == "balloon" || a == "steam_airship" || a == "zeppelin") return OfCity(Pick("civ_vn_heavy", v), v, k);
                if (a == "drone" || a == "light_drone") return Initials(k) + "-UAV-" + Serial(k, "uav");
                if (a.Contains("heli")) return Pick("civ_vn_rotor", v) + " " + Serial(k, "rotor");
                if (d.Role == "bomber") return Pick("civ_vn_heavy", v) + " " + Serial(k, "heavy");
                if (a == "wyvern") return Pick("civ_vn_mount", v);
                return Pick("civ_vn_bird", v) + " " + Serial(k, "bird");
            }
            if (d.Cls == "mount" || a == "chariot" || a == "horse") return Pick("civ_vn_mount", v);
            if (d.Cls == "siege")
            {
                if (era < 10) return OfCity(Pick("civ_vn_siege", v), v, k);
                return Pick("civ_vn_siege", v) + " " + Serial(k, "gun");
            }
            if (d.Cls == "aa") return Pick("civ_vn_guard", v) + " " + Serial(k, "aa");
            if (d.Cls == "tank") return Pick("civ_vn_beast", v) + " " + Serial(k, "tank");
            if (era < 10 || a == "cart" || a == "carriage") return OfCity(Txt.T(d.Id), v, k);
            int s = Serial(k, "plate");
            return Initials(k) + "-" + (era < 12 ? (100 + s % 900).ToString() : (1000 + s % 9000).ToString());
        }
    }
}
