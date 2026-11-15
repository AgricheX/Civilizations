using System;
using System.Collections.Generic;
using System.Text;

namespace Civilizations
{
    internal static class Names
    {
        private static readonly Dictionary<string, string[]> SectorRoots = new Dictionary<string, string[]>
        {
            { "agro", new[] { "Agra", "Campos", "Messis", "Verdana", "Colheita" } },
            { "mining", new[] { "Petra", "Ferrum", "Minera", "Carbo", "Veios" } },
            { "textile", new[] { "Textil", "Fiora", "Tecela", "Lanna", "Sedas" } },
            { "trade", new[] { "Mercato", "Comercia", "Bazar", "Trato", "Escambo" } },
            { "shipping", new[] { "Navis", "Maris", "Velas", "Ancora", "Portos" } },
            { "finance", new[] { "Banco", "Aurum", "Credito", "Cofre", "Moeda" } },
            { "steel", new[] { "Acerix", "Forja", "Lingote", "Bigorna", "Fundix" } },
            { "rail", new[] { "Via", "Trilhos", "Ferrovia", "Linha", "Expresso" } },
            { "energy", new[] { "Volta", "Fulgor", "Watt", "Lumen", "Dinamo" } },
            { "oil", new[] { "Petrol", "Oleum", "Poco", "Negra", "Refina" } },
            { "auto", new[] { "Auto", "Rodas", "Motora", "Pistao", "Volante" } },
            { "arms", new[] { "Arma", "Bastiao", "Gladio", "Canhao", "Escudo" } },
            { "media", new[] { "Midia", "Voz", "Antena", "Gazeta", "Eco" } },
            { "pharma", new[] { "Farma", "Cura", "Remedia", "Saude", "Vita" } },
            { "aero", new[] { "Aero", "Asa", "Planar", "Celeste", "Rota" } },
            { "space", new[] { "Astra", "Orbita", "Cosmo", "Estelar", "Lua" } },
            { "tech", new[] { "Tekna", "Bit", "Chip", "Nexo", "Pixel" } },
            { "biotech", new[] { "Gena", "Bios", "Cela", "Helix", "Vivo" } },
            { "asteroid_mining", new[] { "Astero", "Kuiper", "Meteora", "Regolis", "Belta" } },
            { "fusion", new[] { "Helion", "Fusio", "Tokama", "Solaris", "Plasmo" } },
            { "orbital", new[] { "Orbix", "Lagrange", "Apogea", "Tethra", "Perigea" } },
            { "terraforming", new[] { "Gaia", "Verdis", "Biomea", "Edena", "Terrava" } },
            { "antimatter", new[] { "Positra", "Antix", "Annihil", "Paritas", "Mirrora" } },
            { "nanotech", new[] { "Nanix", "Quanta", "Motea", "Swarma", "Fabrix" } },
            { "exotic", new[] { "Casimir", "Exotica", "Warpa", "Krasnik", "Alcubia" } },
            { "megaworks", new[] { "Dysona", "Ringa", "Colossa", "Titana", "Shellix" } },
            { "dark", new[] { "Umbra", "Noxa", "Haloa", "Penumbra", "Obscura" } }
        };

        private static readonly string[] SuffixKeys = { "civ_co_sons", "civ_co_cia", "civ_co_sa", "civ_co_ltd", "civ_co_group", "civ_co_corp", "civ_co_holdings", "civ_co_industries", "civ_co_co" };
        private static readonly string[] Cosmic = { "tek", "nova", "ion", "yx", "os", "ara" };

        private static string Suffix(int i)
        {
            return " " + Txt.T(SuffixKeys[Math.Max(0, Math.Min(SuffixKeys.Length - 1, i))]);
        }
        private static readonly string[] Modern = { "X", "on", "ix", "ia", "ex", "ify", "o", "us" };

        private static readonly string[][] CurrencyByEra =
        {
            new[] { "shell", "bead", "pebble" },
            new[] { "shell", "bead", "pebble" },
            new[] { "ring", "grain", "bar" },
            new[] { "shekel", "talent", "ring" },
            new[] { "shekel", "talent", "mina" },
            new[] { "drachma", "obol", "stater" },
            new[] { "denarius", "sestertius", "aureus", "drachma" },
            new[] { "florin", "ducat", "solidus", "groat", "dinar" },
            new[] { "tostao", "ducat", "real", "escudo", "thaler" },
            new[] { "pound", "crown", "franc", "peso", "lira" },
            new[] { "mark", "ruble", "cruzado", "franc", "crown" },
            new[] { "cruzeiro", "peso", "mark", "dollar", "yen" },
            new[] { "cruzeiro", "rand", "dollar", "florin", "real" },
            new[] { "credit", "unit", "real", "euro" },
            new[] { "credit", "token", "bitcoin", "unit" },
            new[] { "orbital_credit", "lunar", "launch_bond" },
            new[] { "helion", "orbital_credit", "lunar" },
            new[] { "solar", "helion", "joule" },
            new[] { "ark_bond", "solar", "joule" },
            new[] { "positron", "joule", "ark_bond" },
            new[] { "warp_note", "positron", "lumen" },
            new[] { "lane_mark", "warp_note", "lumen" },
            new[] { "exon", "lane_mark", "lumen" },
            new[] { "gate_mark", "exon", "parsec" },
            new[] { "parsec", "gate_mark", "exon" },
            new[] { "neutron", "parsec", "pulsar" },
            new[] { "domain_crown", "pulsar", "neutron" },
            new[] { "core", "domain_crown", "pulsar" },
            new[] { "quanta", "core", "erg" },
            new[] { "galactic", "quanta", "erg" },
            new[] { "void_mark", "galactic", "quanta" },
            new[] { "satellite_mark", "galactic", "erg" },
            new[] { "merger_bond", "galactic", "quanta" },
            new[] { "quasar", "merger_bond", "erg" },
            new[] { "halo", "quasar", "lumen" },
            new[] { "shade", "halo", "quanta" },
            new[] { "filament", "shade", "erg" },
            new[] { "attractor", "filament", "quanta" },
            new[] { "laniakea", "attractor", "lumen" },
            new[] { "web", "laniakea", "quanta" },
            new[] { "vacuum", "web", "planck" },
            new[] { "omega", "planck", "vacuum" }
        };

        public static string Root(string name)
        {
            if (string.IsNullOrEmpty(name)) return "Nova";
            string w = name.Split(' ')[0];
            var sb = new StringBuilder();
            foreach (char c in w) if (char.IsLetter(c)) sb.Append(c);
            string r = sb.ToString();
            if (r.Length > 6) r = r.Substring(0, 6);
            return r.Length == 0 ? "Nova" : char.ToUpper(r[0]) + r.Substring(1).ToLower();
        }

        public static string Company(Company co, Actor founder, Kingdom k)
        {
            string[] roots;
            if (!SectorRoots.TryGetValue(co.Sector, out roots)) roots = new[] { Txt.T("civ_co_group") };
            string root = G.Pick(roots);
            int era = Research.Era(k);
            string person = Root(founder != null ? founder.getName() : "");
            string city = founder != null && founder.city != null ? Root(G.Name(founder.city)) : Root(G.Name(k));
            float r = UnityEngine.Random.value;
            if (era <= 9)
            {
                if (r < 0.4f) return person + Suffix(0);
                if (r < 0.7f) return Txt.F("civ_co_company_of", root, city);
                return root + " " + person + Suffix(UnityEngine.Random.Range(1, 4));
            }
            if (era <= 12)
            {
                if (r < 0.35f) return person + " " + root + Suffix(UnityEngine.Random.Range(3, SuffixKeys.Length));
                if (r < 0.7f) return city + root;
                return root + person + Suffix(UnityEngine.Random.Range(4, SuffixKeys.Length));
            }
            if (era >= 15)
            {
                if (r < 0.3f) return root + "-" + UnityEngine.Random.Range(2, 99);
                if (r < 0.6f) return root + G.Pick(Cosmic);
                if (r < 0.8f) return person + G.Pick(Cosmic) + Suffix(UnityEngine.Random.Range(4, SuffixKeys.Length));
                return city + root.ToLower();
            }
            if (co.Sector == "space" && r < 0.5f) return person.Substring(0, Math.Min(5, person.Length)) + "X";
            if (r < 0.5f) return root + G.Pick(Modern);
            if (r < 0.8f) return person + G.Pick(Modern);
            return root + city.Substring(0, Math.Min(3, city.Length)).ToLower();
        }

        public static string Currency(Kingdom k, int era)
        {
            era = Math.Max(0, Math.Min(CurrencyByEra.Length - 1, era));
            if (G.Chance(0.35f))
            {
                string root = Root(G.Name(k));
                string[] suf = { "ar", "o", "ina", "eta", "ão", "al" };
                if (root.Length > 4) root = root.Substring(0, 4);
                return root + G.Pick(suf);
            }
            return Txt.T("civ_cur_" + G.Pick(CurrencyByEra[era]));
        }

        public static string Symbol(string name)
        {
            if (string.IsNullOrEmpty(name)) return "¤";
            string s = name.Substring(0, 1).ToUpper();
            if (name.Length > 2) s += name.Substring(name.Length - 1, 1).ToLower();
            return s + "$";
        }

        public static string Handle(Actor a)
        {
            if (!G.Ok(a)) return "@anon";
            string n = Root(a.getName()).ToLower();
            long id = a.data.id;
            return "@" + n + (id % 97 == 0 ? "" : (id % 997).ToString());
        }

        public static string Handle(Kingdom k)
        {
            string root = k != null && k.data != null ? Store.Str(k.data, RealNames.KRoot) : "";
            return "@" + Root(root.Length > 0 ? root : G.Name(k)).ToLower() + "_" + Txt.T("civ_handle_official");
        }

        public static string Handle(Company c)
        {
            return "@" + c.Name.Replace(" ", "").Replace("&", "").Replace(".", "").ToLower();
        }
    }
}
