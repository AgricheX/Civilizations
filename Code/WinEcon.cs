using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal class WinEcon : AbstractWindow<WinEcon>
    {
        public const string Id = "civ_econ";
        private static readonly string[] TabKeys = { "econ_tab_companies", "econ_tab_rich", "econ_tab_currency", "econ_tab_energy", "econ_tab_trade" };
        private static readonly string[] TabIcons = { "ui/Icons/civ/econ/companies", "ui/Icons/civ/econ/rich", "ui/Icons/civ/econ/currency", "ui/Icons/civ/econ/energy", "ui/Icons/civ/econ/trade" };
        private static int _tab;
        private static int _company = -1;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open()
        {
            UiKit.Open(Id);
        }

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
        }

        public override void OnNormalEnable()
        {
            Render();
        }

        private void Render()
        {
            Transform c = ContentTransform;
            UiKit.Clear(c);
            if (!G.Loaded) return;
            UiKit.Tabs(c, TabKeys, TabIcons, _tab, i =>
            {
                _tab = i;
                _company = -1;
                Render();
            });
            if (_company >= 0)
            {
                CompanyPage(c);
                return;
            }
            switch (_tab)
            {
                case 0:
                    CompaniesTab(c);
                    break;
                case 1:
                    Rich(c);
                    break;
                case 2:
                    Currencies(c);
                    break;
                case 3:
                    EnergyTab(c);
                    break;
                case 4:
                    Trade(c);
                    break;
            }
        }

        private void CompaniesTab(Transform c)
        {
            if (CivMod.C != null && !CivMod.C.CompaniesOn)
            {
                UiKit.Label(c, Txt.T("econ_companies_off"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            UiKit.Header(c, Txt.T("econ_forbes"), UiKit.Gold, "ui/Icons/civ/econ/companies");
            List<Company> list = Companies.Ranking(30);
            if (list.Count == 0)
            {
                UiKit.Label(c, Txt.T("econ_no_companies"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            int rank = 1;
            foreach (Company co in list)
            {
                Company cc = co;
                RectTransform box = UiKit.Box(c, "Company", 28f);
                UiKit.Panel(box, 0.85f);
                UiKit.Text(box, rank++ + ".", new Vector2(-UiKit.W / 2f + 10f, 0f), new Vector2(18f, 12f), 8, TextAnchor.MiddleCenter, UiKit.Dim);
                UiKit.IconBtn(box, UiKit.Icon("ui/Icons/civ/sector/" + co.Sector), () =>
                {
                    _company = cc.Id;
                    Render();
                }, 22f, new Vector2(-UiKit.W / 2f + 32f, 0f), co.Name, Txt.T("civ_sector_" + co.Sector));
                UiKit.Text(box, co.Name, new Vector2(10f, 6f), new Vector2(110f, 11f), 9, TextAnchor.MiddleLeft, Color.white);
                UiKit.Text(box, Txt.T("civ_sector_" + co.Sector) + " · " + co.HomeName, new Vector2(10f, -6f), new Vector2(110f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Dim);
                Kingdom home = G.KingdomById(co.Home);
                UiKit.Text(box, Currency.Format(home, co.Value), new Vector2(UiKit.W / 2f - 28f, 4f), new Vector2(52f, 11f), 8, TextAnchor.MiddleRight, UiKit.Gold);
                float trend = co.History.Count >= 2 ? co.History[co.History.Count - 1] - co.History[co.History.Count - 2] : 0f;
                UiKit.Text(box, trend >= 0 ? "▲" : "▼", new Vector2(UiKit.W / 2f - 8f, -7f), new Vector2(12f, 10f), 8, TextAnchor.MiddleCenter, trend >= 0 ? UiKit.Good : UiKit.Bad);
                UiKit.Text(box, co.Status == "state" ? Txt.T("econ_state") : co.Branches.Count > 0 ? Txt.F("econ_branches", co.Branches.Count) : "", new Vector2(UiKit.W / 2f - 36f, -7f), new Vector2(50f, 10f), 7, TextAnchor.MiddleRight, UiKit.Cyan);
            }
        }

        private void CompanyPage(Transform c)
        {
            Company co = Companies.Get(_company);
            UiKit.Btn(UiKit.Box(c, "Back", 22f), Txt.T("net_back"), null, () =>
            {
                _company = -1;
                Render();
            }, new Vector2(70f, 20f), new Vector2(-UiKit.W / 2f + 36f, 0f));
            if (co == null) return;
            Kingdom home = G.KingdomById(co.Home);
            UiKit.Header(c, co.Name, UiKit.Gold, "ui/Icons/civ/sector/" + co.Sector);
            UiKit.Stat(c, "ui/Icons/civ/sector/" + co.Sector, Txt.T("econ_sector"), Txt.T("civ_sector_" + co.Sector));
            UiKit.Stat(c, null, Txt.T("econ_home"), co.HomeName);
            UiKit.Stat(c, null, Txt.T("econ_founded"), Txt.F("net_year", co.Founded) + " · " + co.FounderName);
            UiKit.Stat(c, null, Txt.T("econ_ceo"), co.CeoName);
            UiKit.Stat(c, null, Txt.T("econ_value"), Currency.Format(home, co.Value), UiKit.Gold);
            UiKit.Stat(c, null, Txt.T("econ_cash"), Currency.Format(home, co.Cash));
            UiKit.Stat(c, null, Txt.T("econ_staff"), co.Staff.ToString());
            UiKit.Stat(c, null, Txt.T("econ_status"), Txt.T("econ_status_" + co.Status));
            if (!string.IsNullOrEmpty(co.Route)) UiKit.Stat(c, WorldPrefs.Icon(co.Route), Txt.T("econ_route"), Txt.T("civ_route_" + co.Route));
            if (co.History.Count > 1)
            {
                UiKit.Header(c, Txt.T("econ_history"), UiKit.Soft);
                Chart(c, co.History, new Color(1f, 0.82f, 0.35f));
            }
            if (co.Branches.Count > 0)
            {
                UiKit.Header(c, Txt.T("econ_branches_h"), UiKit.Soft);
                UiKit.Paragraph(c, string.Join(", ", co.Branches.Select(b => G.Name(G.KingdomById(b))).ToArray()), 8, UiKit.Soft);
            }
            if (co.Patents.Count > 0)
            {
                UiKit.Header(c, Txt.T("econ_patents"), UiKit.Soft);
                foreach (string p in co.Patents)
                {
                    TechDef t = Db.Tech(p);
                    if (t == null) continue;
                    UiKit.Stat(c, t.Icon, Db.TechName(t.Id), Db.EraName(t.Era), t.Rare ? new Color(0.9f, 0.6f, 1f) : UiKit.Soft);
                }
            }
        }

        private void Chart(Transform c, List<float> values, Color color)
        {
            RectTransform box = UiKit.Box(c, "Chart", 44f);
            UiKit.Panel(box, 0.7f);
            int n = Math.Min(40, values.Count);
            List<float> v = values.Skip(values.Count - n).ToList();
            float max = Math.Max(0.0001f, v.Max());
            float min = Math.Min(0f, v.Min());
            float bw = (UiKit.W - 10f) / n;
            for (int i = 0; i < n; i++)
            {
                float h = Math.Max(1f, 38f * (v[i] - min) / (max - min));
                UiKit.Img(box, UiKit.S("ui/special/windowInnerSliced"), new Vector2(Math.Max(1f, bw - 1f), h), new Vector2(-UiKit.W / 2f + 5f + bw * i + bw / 2f, -19f + h / 2f), color).preserveAspect = false;
            }
        }

        private void Rich(Transform c)
        {
            UiKit.Header(c, Txt.T("econ_richest"), UiKit.Gold, "ui/Icons/civ/econ/rich");
            int rank = 1;
            foreach (Actor a in Companies.Richest(20))
            {
                RectTransform box = UiKit.Box(c, "Rich", 26f);
                UiKit.Panel(box, 0.8f);
                UiKit.Text(box, rank++ + ".", new Vector2(-UiKit.W / 2f + 10f, 0f), new Vector2(18f, 12f), 8, TextAnchor.MiddleCenter, UiKit.Dim);
                UiKit.Avatar(box, a, new Vector2(-UiKit.W / 2f + 32f, 0f), 0.42f);
                UiKit.Text(box, a.getName(), new Vector2(8f, 5f), new Vector2(110f, 11f), 9, TextAnchor.MiddleLeft);
                UiKit.Text(box, G.Name(a.kingdom) + (Roles.Of(a).Length > 0 ? " · " + Txt.T("civ_role_" + Roles.Of(a)) : ""), new Vector2(8f, -6f), new Vector2(110f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Dim);
                UiKit.Text(box, Currency.Format(a.kingdom, a.money), new Vector2(UiKit.W / 2f - 28f, 0f), new Vector2(54f, 12f), 8, TextAnchor.MiddleRight, UiKit.Gold);
            }
        }

        private void Currencies(Transform c)
        {
            UiKit.Header(c, Txt.T("econ_currencies"), UiKit.Gold, "ui/Icons/civ/econ/currency");
            List<Kingdom> ks = G.Kingdoms().Where(k => k.isCiv() && Currency.Has(k)).OrderByDescending(Currency.Rate).ToList();
            if (ks.Count == 0)
            {
                UiKit.Label(c, Txt.T("econ_no_currency"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            foreach (Kingdom k in ks)
            {
                RectTransform box = UiKit.Box(c, "Cur", 28f);
                UiKit.Panel(box, 0.8f);
                UiKit.Banner(box, k, new Vector2(-UiKit.W / 2f + 14f, 0f), 22f);
                Kingdom issuer = Currency.Issuer(k);
                string name = Currency.Name(k) + " (" + Currency.Symbol(k) + ")" + (issuer != null && issuer != k ? " ⇄" : "");
                UiKit.Text(box, name, new Vector2(4f, 6f), new Vector2(130f, 11f), 9, TextAnchor.MiddleLeft, UiKit.Gold);
                UiKit.Text(box, G.Name(k), new Vector2(4f, -6f), new Vector2(130f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Dim);
                float infl = Currency.Inflation(k);
                UiKit.Text(box, Currency.Rate(k).ToString("0.###"), new Vector2(UiKit.W / 2f - 22f, 5f), new Vector2(40f, 11f), 8, TextAnchor.MiddleRight);
                UiKit.Text(box, Txt.F("econ_infl", (infl * 100f).ToString("0.#")), new Vector2(UiKit.W / 2f - 22f, -6f), new Vector2(44f, 10f), 7, TextAnchor.MiddleRight, infl > 0.2f ? UiKit.Bad : infl > 0.05f ? UiKit.Gold : UiKit.Good);
            }
        }

        private void EnergyTab(Transform c)
        {
            UiKit.Header(c, Txt.T("econ_energy"), UiKit.Gold, "ui/Icons/civ/econ/energy");
            List<Kingdom> ks = G.Kingdoms().Where(k => k.isCiv() && Energy.Started(k)).OrderByDescending(Energy.KingdomSupply).ToList();
            if (ks.Count == 0)
            {
                UiKit.Label(c, Txt.T("econ_no_energy"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            foreach (Kingdom k in ks)
            {
                float sup = Energy.KingdomSupply(k);
                float dem = Energy.KingdomDemand(k);
                float ratio = dem > 0f ? sup / dem : 1f;
                RectTransform box = UiKit.Box(c, "Energy", 40f);
                UiKit.Panel(box, 0.85f);
                UiKit.Banner(box, k, new Vector2(-UiKit.W / 2f + 14f, 6f), 20f);
                UiKit.Text(box, G.Name(k), new Vector2(4f, 13f), new Vector2(130f, 11f), 9, TextAnchor.MiddleLeft);
                UiKit.Text(box, G.Short(sup) + " / " + G.Short(dem) + " MW", new Vector2(UiKit.W / 2f - 34f, 13f), new Vector2(64f, 10f), 7, TextAnchor.MiddleRight, ratio >= 1f ? UiKit.Good : UiKit.Bad);
                Dictionary<string, float> mix = Energy.Mix(k);
                float total = Math.Max(0.001f, mix.Values.Sum());
                float x = -UiKit.W / 2f + 30f;
                float width = UiKit.W - 36f;
                foreach (var kv in mix.OrderByDescending(m => m.Value))
                {
                    float w = width * kv.Value / total;
                    if (w < 1f) continue;
                    Color col = PlantColor(kv.Key);
                    Image(box, new Vector2(w - 1f, 8f), new Vector2(x + w / 2f, 0f), col);
                    if (w > 16f) UiKit.Img(box, UiKit.Icon("ui/Icons/civ/plant/" + kv.Key), new Vector2(10f, 10f), new Vector2(x + w / 2f, -11f));
                    x += w;
                }
                if (Energy.Grid(k)) UiKit.Text(box, Txt.T("econ_grid"), new Vector2(UiKit.W / 2f - 30f, -13f), new Vector2(56f, 9f), 7, TextAnchor.MiddleRight, UiKit.Cyan);
            }
        }

        private static void Image(Transform box, Vector2 size, Vector2 pos, Color c)
        {
            UiKit.Img(box, UiKit.S("ui/special/windowInnerSliced"), size, pos, c).preserveAspect = false;
        }

        public static Color PlantColor(string id)
        {
            switch (id)
            {
                case "civ_plant_biomass": return new Color(0.6f, 0.4f, 0.2f);
                case "civ_plant_coal": return new Color(0.32f, 0.32f, 0.35f);
                case "civ_plant_oil": return new Color(0.25f, 0.2f, 0.3f);
                case "civ_plant_diesel": return new Color(0.55f, 0.4f, 0.25f);
                case "civ_plant_gas": return new Color(0.9f, 0.6f, 0.2f);
                case "civ_plant_nuclear": return new Color(0.5f, 1f, 0.4f);
                case "civ_plant_atomic": return new Color(0.3f, 0.95f, 0.75f);
                case "civ_plant_hydro": return new Color(0.3f, 0.6f, 1f);
                case "civ_plant_wind": return new Color(0.82f, 0.95f, 1f);
                case "civ_plant_solar": return new Color(1f, 0.9f, 0.3f);
                case "civ_plant_tesla": return new Color(0.55f, 0.75f, 1f);
                case "civ_plant_aether": return new Color(0.85f, 0.65f, 0.4f);
                case "civ_plant_bio": return new Color(0.45f, 0.85f, 0.35f);
                case "civ_plant_fusion": return new Color(1f, 0.5f, 0.9f);
                default: return new Color(0.7f, 0.5f, 0.9f);
            }
        }

        private void Trade(Transform c)
        {
            UiKit.Header(c, Txt.T("econ_trade"), UiKit.Gold, "ui/Icons/civ/econ/trade");
            foreach (Kingdom k in G.Kingdoms().Where(x => x.isCiv()).OrderByDescending(G.Pop).Take(20))
            {
                Dictionary<string, float> depot = Economy.Depot(k);
                if (depot.Count == 0) continue;
                RectTransform box = UiKit.Box(c, "Depot", 50f);
                UiKit.Panel(box, 0.8f);
                UiKit.Banner(box, k, new Vector2(-UiKit.W / 2f + 14f, 10f), 20f);
                UiKit.Text(box, G.Name(k), new Vector2(4f, 16f), new Vector2(150f, 11f), 9, TextAnchor.MiddleLeft);
                int i = 0;
                foreach (var kv in depot.OrderByDescending(d => d.Value).Take(8))
                {
                    string art;
                    Db.ResourceArt.TryGetValue(kv.Key, out art);
                    float x = -UiKit.W / 2f + 38f + (i % 4) * 44f;
                    float y = i < 4 ? 0f : -14f;
                    UiKit.Img(box, UiKit.Icon("ui/Icons/civ/res/" + (art ?? kv.Key)), new Vector2(11f, 11f), new Vector2(x, y));
                    Text t = UiKit.Text(box, G.Short(kv.Value), new Vector2(x + 21f, y), new Vector2(30f, 10f), 7, TextAnchor.MiddleLeft, UiKit.Soft);
                    t.resizeTextForBestFit = false;
                    i++;
                }
            }
        }
    }
}
