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
            { "biotech", new[] { "Gena", "Bios", "Cela", "Helix", "Vivo" } }
        };

        private static readonly string[] Suffixes = { " & Filhos", " Cia.", " S.A.", " Ltda.", " Group", " Corp.", " Holdings", " Industrias", " Co." };
        private static readonly string[] Modern = { "X", "on", "ix", "ia", "ex", "ify", "o", "us" };

        private static readonly string[][] CurrencyByEra =
        {
            new[] { "Concha", "Conta", "Pedrinha" },
            new[] { "Concha", "Conta", "Pedrinha" },
            new[] { "Anel", "Grão", "Barra" },
            new[] { "Siclo", "Talento", "Anel" },
            new[] { "Siclo", "Talento", "Mina" },
            new[] { "Dracma", "Óbolo", "Estáter" },
            new[] { "Denário", "Sestércio", "Áureo", "Dracma" },
            new[] { "Florim", "Ducado", "Soldo", "Grosso", "Dinar" },
            new[] { "Tostão", "Ducado", "Real", "Escudo", "Táler" },
            new[] { "Libra", "Coroa", "Franco", "Peso", "Lira" },
            new[] { "Marco", "Rublo", "Cruzado", "Franco", "Coroa" },
            new[] { "Cruzeiro", "Peso", "Marco", "Dólar", "Iene" },
            new[] { "Cruzeiro", "Rand", "Dólar", "Florim", "Real" },
            new[] { "Crédito", "Unidade", "Real", "Euro" },
            new[] { "Crédito", "Token", "Bitmoeda", "Unidade" }
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
            if (!SectorRoots.TryGetValue(co.Sector, out roots)) roots = new[] { "Grupo" };
            string root = G.Pick(roots);
            int era = Research.Era(k);
            string person = Root(founder != null ? founder.getName() : "");
            string city = founder != null && founder.city != null ? Root(G.Name(founder.city)) : Root(G.Name(k));
            float r = UnityEngine.Random.value;
            if (era <= 9)
            {
                if (r < 0.4f) return person + Suffixes[0];
                if (r < 0.7f) return "Companhia " + root + " de " + city;
                return root + " " + person + Suffixes[UnityEngine.Random.Range(1, 4)];
            }
            if (era <= 12)
            {
                if (r < 0.35f) return person + " " + root + Suffixes[UnityEngine.Random.Range(3, Suffixes.Length)];
                if (r < 0.7f) return city + root;
                return root + person + Suffixes[UnityEngine.Random.Range(4, Suffixes.Length)];
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
            return G.Pick(CurrencyByEra[era]);
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
            return "@" + Root(root.Length > 0 ? root : G.Name(k)).ToLower() + "_oficial";
        }

        public static string Handle(Company c)
        {
            return "@" + c.Name.Replace(" ", "").Replace("&", "").Replace(".", "").ToLower();
        }
    }
}
