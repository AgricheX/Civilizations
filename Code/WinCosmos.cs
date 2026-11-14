using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.api;
using NeoModLoader.General;
using NeoModLoader.General.UI.Prefabs;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal class WinCosmos : AbstractWindow<WinCosmos>
    {
        public const string Id = "civ_cosmos";
        private static string _path;
        private static int _page;
        private static int _drawnYear = int.MinValue;
        private static Sprite _last;
        private static bool _open;
        private static float _anim;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Open()
        {
            if (Cosmos.Ready && _path == null) _path = CosmosGen.ParentPath(Cosmos.St.Home);
            UiKit.Open(Id);
        }

        public static void Focus(string path)
        {
            _path = path;
            _page = 0;
            Open();
        }

        private static float _zoom = 1f;
        private static float _cw = UiKit.W;
        private static string _sec = "places";

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
            Vector2 size = WinSize.Set(BackgroundTransform, ContentTransform, 452f, 320f);
            _cw = Mathf.Max(UiKit.W, size.x - 18f);
            _zoom = 1f;
        }

        private static bool Wide
        {
            get { return _cw >= UiKit.W * 2f + 6f; }
        }

        private static RectTransform Column(RectTransform box, float x)
        {
            var go = new GameObject("Col", typeof(RectTransform));
            go.transform.SetParent(box, false);
            RectTransform r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 1f);
            r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(UiKit.W, 10f);
            r.anchoredPosition = new Vector2(x, 0f);
            VerticalLayoutGroup vl = go.AddComponent<VerticalLayoutGroup>();
            OT.InitializeNoActionVerticalLayoutGroup(vl);
            vl.spacing = 3;
            vl.childAlignment = TextAnchor.UpperCenter;
            return r;
        }

        private static Transform[] Cols(Transform c, string name)
        {
            if (!Wide) return new[] { c, c };
            RectTransform box = UiKit.Box(c, name, 10f, _cw);
            float x = _cw / 2f - UiKit.W / 2f - 2f;
            return new Transform[] { Column(box, -x), Column(box, x) };
        }

        private static float Height(Transform col)
        {
            float h = 0f;
            int n = 0;
            for (int i = 0; i < col.childCount; i++)
            {
                Transform t = col.GetChild(i);
                if (!t.gameObject.activeSelf) continue;
                LayoutElement le = t.GetComponent<LayoutElement>();
                RectTransform r = t as RectTransform;
                h += le != null && le.preferredHeight > 0f ? le.preferredHeight : r != null ? r.sizeDelta.y : 0f;
                n++;
            }
            return h + Math.Max(0, n - 1) * 3f;
        }

        private static void Fit(Transform[] cols, float min = 0f)
        {
            if (cols[0] == cols[1]) return;
            float h = Math.Max(min, Math.Max(Height(cols[0]), Height(cols[1])));
            foreach (Transform t in cols) ((RectTransform)t).sizeDelta = new Vector2(UiKit.W, h);
            RectTransform box = (RectTransform)cols[0].parent;
            box.sizeDelta = new Vector2(_cw, h);
            LayoutElement le = box.GetComponent<LayoutElement>();
            if (le != null) le.preferredHeight = h;
        }

        public override void OnNormalEnable()
        {
            _open = true;
            Render();
        }

        public override void OnNormalDisable()
        {
            _open = false;
            Release();
        }

        private static void Release()
        {
            if (_last != null)
            {
                Texture2D t = _last.texture;
                UnityEngine.Object.Destroy(_last);
                if (t != null) UnityEngine.Object.Destroy(t);
                _last = null;
            }
        }

        private void Update()
        {
            if (!_open || !G.Loaded || !Cosmos.Ready) return;
            if (G.Year != _drawnYear) Render();
            CNode n = CosmosGen.Get(_path);
            if (n != null && n.Level == 1 && Cosmos.St.Voyages.Count > 0)
            {
                _anim += Time.unscaledDeltaTime;
                if (_anim > 1.5f)
                {
                    _anim = 0f;
                    CosmosArt.Phase += 0.15f;
                    Render();
                }
            }
        }

        private void Go(string path)
        {
            _path = path;
            _page = 0;
            Render();
        }

        private void Render()
        {
            Transform c = ContentTransform;
            UiKit.Clear(c);
            _drawnYear = G.Year;
            if (!G.Loaded || !Cosmos.Ready)
            {
                UiKit.Label(c, Txt.T("cos_not_ready"), 30f, 9, TextAnchor.MiddleCenter, UiKit.Dim);
                return;
            }
            if (_path == null) _path = CosmosGen.ParentPath(Cosmos.St.Home);
            CNode n = CosmosGen.Get(_path);
            string title = n.Name.Length > 0 ? n.Name : Txt.T("cos_level_universe");
            UiKit.Header(c, title, UiKit.Gold, NodeIcon(n));
            UiKit.Label(c, CosmosGen.Describe(n), 12f, 8, TextAnchor.MiddleCenter, UiKit.Soft);
            if (CosmosPlanet.Active != null)
            {
                CNode an = CosmosGen.Get(CosmosPlanet.Active);
                UiKit.Btn(UiKit.Box(c, "Back", 26f), Txt.F("cos_return", an.Name.Length > 0 ? an.Name : CosmosGen.Describe(an)), UiKit.Icon("ui/Icons/civ/cos/return"), CosmosPlanet.Return, new Vector2(UiKit.W - 10f, 24f), Vector2.zero, Txt.T("cos_return_t"), Txt.T("cos_return_d"));
                string up = CosmosScale.Scale(CosmosPlanet.Active) ? CosmosScale.Up(CosmosPlanet.Active) : null;
                if (up != null) UiKit.Btn(UiKit.Box(c, "Up", 24f), Txt.T("cos_scale_up"), UiKit.Icon("ui/Icons/civ/cos/scale_up"), () => CosmosPlanet.Open(up), new Vector2(UiKit.W - 20f, 20f), Vector2.zero, Txt.T("cos_scale_up"), Txt.T("cos_scale_up_d"));
            }
            Nav(c, n);
            Transform[] body = Cols(c, "Body");
            View(body[0], n);
            Info(body[1], n);
            Fit(body, CosmosArt.H * _zoom + 4f);
            var secs = new List<string>();
            var icons = new List<string>();
            var keys = new List<string>();
            if (CosmosGen.Children(n).Count > 0)
            {
                secs.Add("places");
                keys.Add("cos_contains_" + CosmosGen.LevelIds[Math.Max(0, n.Level - 1)]);
                icons.Add("ui/Icons/civ/cos/locate");
            }
            if (RealmKingdom() != null)
            {
                secs.Add("realm");
                keys.Add("cos_tab_realm");
                icons.Add("ui/Icons/civ/cos/home");
            }
            if (n.Level >= 1 && PowerList(n).Count > 0)
            {
                secs.Add("powers");
                keys.Add("cos_powers_here");
                icons.Add("ui/Icons/civ/world/cosmos");
            }
            if (Cosmos.Shared)
            {
                secs.Add("worlds");
                keys.Add("cos_worlds");
                icons.Add("ui/Icons/civ/cfg/shared");
            }
            if (secs.Count == 0) return;
            if (!secs.Contains(_sec)) _sec = secs[0];
            UiKit.Tabs(c, keys.ToArray(), icons.ToArray(), secs.IndexOf(_sec), i =>
            {
                _sec = secs[i];
                _page = 0;
                Render();
            });
            UiKit.Header(c, Txt.T(keys[secs.IndexOf(_sec)]), UiKit.Gold);
            switch (_sec)
            {
                case "places": Children(c, n); break;
                case "realm": Realm(c); break;
                case "powers": Powers(c, n); break;
                case "worlds": Worlds(c); break;
            }
        }

        private static bool _armReset;

        private void Worlds(Transform c)
        {
            UiKit.Header(c, Txt.T("cos_worlds"), UiKit.Cyan, "ui/Icons/civ/cfg/shared");
            UniWorld me = Universe.Me;
            foreach (UniWorld w in Universe.Worlds())
            {
                if (w.Lost) continue;
                float rw = Wide ? _cw : UiKit.W;
                RectTransform row = UiKit.Box(c, "World" + w.Tag, 22f, rw);
                UiKit.Panel(row, 0.5f);
                string name = string.IsNullOrEmpty(w.Name) ? Txt.F("cos_world_n", w.Tag) : w.Name;
                string state = w == me ? Txt.T("cos_world_here") : w.Frozen ? Txt.T("cos_world_frozen") : Txt.F("cos_world_away", Cosmos.St.Clock - w.Seen);
                UiKit.Text(row, "<color=" + w.Color + ">■</color> " + name + "  ·  " + Db.EraName(w.Era) + "  ·  " + state, new Vector2(-40f, 0f), new Vector2(rw - 100f, 20f), 8, TextAnchor.MiddleLeft, w == me ? UiKit.Gold : UiKit.Soft);
                if (w == me) continue;
                UniWorld target = w;
                UiKit.Btn(row, w.Frozen ? Txt.T("cos_world_thaw") : Txt.T("cos_world_freeze"), UiKit.Icon(w.Frozen ? "ui/Icons/civ/cos/thaw" : "ui/Icons/civ/cos/freeze"), () =>
                {
                    Universe.SetFrozen(target, !target.Frozen);
                    Render();
                }, new Vector2(58f, 18f), new Vector2(rw / 2f - 34f, 0f), Txt.T("cos_world_freeze"), Txt.T("cos_world_freeze_d"));
                if (w.Home != null) UiKit.IconBtn(row, UiKit.Icon("ui/Icons/civ/cos/locate"), () => Go(CosmosGen.ParentPath(target.Home)), 16f, new Vector2(rw / 2f - 74f, 0f), Txt.T("cos_world_go"), name);
            }
            RectTransform tools = UiKit.Box(c, "WorldTools", 24f);
            RectTransform tools2 = UiKit.Box(c, "WorldTools2", 24f);
            UiKit.Btn(tools, Txt.T("cos_prune"), UiKit.Icon("ui/Icons/civ/cos/prune"), () =>
            {
                int n = Universe.Prune(500);
                Notify.Show(Txt.F("cos_pruned", n));
                Render();
            }, new Vector2(100f, 20f), new Vector2(-52f, 0f), Txt.T("cos_prune"), Txt.T("cos_prune_d"));
            UiKit.Btn(tools, Txt.T("cos_export"), UiKit.Icon("ui/Icons/civ/cos/export"), () =>
            {
                string path = Universe.Export();
                if (path != null) Notify.Show(Txt.F("cos_exported", path));
            }, new Vector2(100f, 20f), new Vector2(52f, 0f), Txt.T("cos_export"), Txt.T("cos_export_d"));
            UiKit.Btn(tools2, Txt.T("cos_import"), UiKit.Icon("ui/Icons/civ/cos/import"), () =>
            {
                Notify.Show(Txt.T(Universe.Import() ? "cos_imported" : "cos_import_none"));
                if (Universe.U != null) Cosmos.Load();
                Render();
            }, new Vector2(100f, 20f), new Vector2(-52f, 0f), Txt.T("cos_import"), Txt.F("cos_import_d", Universe.Folder));
            SimpleButton reset = UiKit.Btn(tools2, _armReset ? Txt.T("world_confirm") : Txt.T("cos_new_universe"), UiKit.Icon("ui/Icons/civ/cos/new_universe"), () =>
            {
                if (!_armReset)
                {
                    _armReset = true;
                    Render();
                    return;
                }
                _armReset = false;
                Universe.Reset();
                Cosmos.Load();
                Render();
            }, new Vector2(100f, 20f), new Vector2(52f, 0f), Txt.T("cos_new_universe"), Txt.T("cos_new_universe_d"));
            if (_armReset) reset.Background.color = new Color(1f, 0.55f, 0.4f);
        }

        private void Nav(Transform c, CNode n)
        {
            RectTransform row = UiKit.Box(c, "Nav", 22f);
            string up = CosmosGen.ParentPath(n.Path);
            UiKit.Btn(row, Txt.T("cos_up"), UiKit.Icon("ui/Icons/civ/cos/up"), () =>
            {
                if (up != null) Go(up);
            }, new Vector2(66f, 20f), new Vector2(-70f, 0f), Txt.T("cos_up"), up != null ? CosmosGen.Get(up).Name : "");
            UiKit.Btn(row, Txt.T("cos_home"), UiKit.Icon("ui/Icons/civ/cos/home"), () => Go(CosmosGen.ParentPath(Cosmos.St.Home)), new Vector2(66f, 20f), new Vector2(0f, 0f), Txt.T("cos_home"), Txt.T("cos_home_d"));
            UiKit.Btn(row, Txt.T("cos_top"), UiKit.Icon("ui/Icons/civ/cos/universe"), () => Go(""), new Vector2(66f, 20f), new Vector2(70f, 0f), Txt.T("cos_top"), Txt.T("cos_top_d"));
        }

        private void View(Transform c, CNode n)
        {
            Release();
            CosView v = CosmosArt.Render(n);
            _last = v.Sprite;
            RectTransform box = UiKit.Box(c, "View", CosmosArt.H * _zoom + 4f, CosmosArt.W * _zoom + 4f);
            UiKit.Img(box, v.Sprite, new Vector2(CosmosArt.W * _zoom, CosmosArt.H * _zoom), Vector2.zero);
            foreach (var kv in v.Spots)
            {
                CNode child = kv.Key;
                float x = (kv.Value.x - CosmosArt.W / 2f) * _zoom, y = (CosmosArt.H / 2f - kv.Value.y) * _zoom;
                SimpleButton b = UiKit.Btn(box, null, null, () => Go(child.Path), new Vector2(12f * _zoom, 12f * _zoom), new Vector2(x, y), child.Name, Tip(child));
                b.Background.color = new Color(1f, 1f, 1f, 0f);
                if (b.Icon != null) b.Icon.enabled = false;
                if (b.Text != null) b.Text.enabled = false;
            }
        }

        public static string NodeIcon(CNode n)
        {
            if (n.Level == 0) return "ui/Icons/civ/cos/body_" + n.Sub;
            if (n.Level == 1) return "ui/Icons/civ/cos/star_" + n.Sub;
            if (n.Level == 5 && n.Sub.Length > 0) return "ui/Icons/civ/cos/galaxy_" + n.Sub;
            return "ui/Icons/civ/cos/scale_" + Math.Min(9, n.Level);
        }

        private static string Tip(CNode n)
        {
            string s = CosmosGen.Describe(n);
            Polity p = Cosmos.OwnerExact(n.Path) ?? (n.Level > 0 ? null : Cosmos.Owner(n.Path));
            if (p != null) s += "\n" + Txt.F("cos_owner", p.Name);
            if (n.Level == 0) s += "\n" + Txt.F("cos_hab", Mathf.RoundToInt(n.Hab * 100f));
            return s;
        }

        private void Info(Transform c, CNode n)
        {
            Polity p = Cosmos.OwnerExact(n.Path);
            if (n.Level == 0)
            {
                UiKit.Stat(c, "ui/Icons/civ/cos/body_" + n.Sub, Txt.T("cos_type"), CosmosGen.Describe(n));
                UiKit.Stat(c, null, Txt.T("cos_habitability"), Mathf.RoundToInt(n.Hab * 100f) + "%", n.Hab > 0.5f ? UiKit.Good : UiKit.Soft);
                if (n.Res.Length > 0) UiKit.Stat(c, null, Txt.T("cos_resources"), string.Join(", ", n.Res.Select(r => Txt.T("cos_res_" + r)).ToArray()), UiKit.Cyan);
                if (n.Home) UiKit.Label(c, Txt.T("cos_homeworld"), 14f, 9, TextAnchor.MiddleCenter, UiKit.Gold);
                if (CosmosPlanet.CanOpen(n)) UiKit.Btn(UiKit.Box(c, "Open", 24f), Txt.T("cos_open_planet"), UiKit.Icon("ui/Icons/civ/cos/land"), () => CosmosPlanet.Open(n.Path), new Vector2(UiKit.W - 20f, 20f), Vector2.zero, Txt.T("cos_open_planet"), Txt.T("cos_open_planet_d"));
            }
            else
            {
                UiKit.Stat(c, NodeIcon(n), Txt.T("cos_type"), CosmosGen.Describe(n));
                if (CosmosScale.CanOpen(n)) UiKit.Btn(UiKit.Box(c, "OpenScale", 24f), Txt.T("cos_open_scale"), UiKit.Icon("ui/Icons/civ/cos/open_map"), () => CosmosPlanet.Open(n.Path), new Vector2(UiKit.W - 20f, 20f), Vector2.zero, Txt.T("cos_open_scale"), Txt.T("cos_open_scale_d"));
            }
            if (p != null)
            {
                UiKit.Header(c, p.Name, UiKit.Hex(p.Color));
                Kingdom k = p.King != 0 ? Cosmos.KingOf(p.King) : null;
                UiKit.Stat(c, null, Txt.T("cos_kind"), Txt.T(p.Kind == "alien" && CosmosLife.KindOf(p.Species) != null && CosmosLife.KindOf(p.Species).Folk ? "cos_kind_folk" : "cos_kind_" + p.Kind));
                if (G.Ok(k)) UiKit.Stat(c, null, Txt.T("cos_parent"), G.Name(k), UiKit.Gold);
                if (p.Species.Length > 0) UiKit.Stat(c, null, Txt.T("cos_species"), Txt.Has("cos_species_" + p.Species) ? Txt.T("cos_species_" + p.Species) : p.Species);
                UiKit.Stat(c, null, Txt.T("cos_population"), Pop(p.Pop));
                UiKit.Stat(c, EraLock.EraSprite(p.Era) != null ? "ui/Icons/civ/era/" + Db.Eras[Math.Min(Db.LastEra, p.Era)].Id : null, Txt.T("cos_era"), Db.EraName(p.Era));
                if (p.Kind == "colony" || p.Kind == "outpost") UiKit.Bar(UiKit.Box(c, "Loyalty", 14f), p.Loyalty, p.Loyalty > 0.5f ? new Color(0.5f, 0.9f, 0.5f) : new Color(1f, 0.5f, 0.4f), new Vector2(UiKit.W - 20f, 10f), Vector2.zero, Txt.F("cos_loyalty", Mathf.RoundToInt(p.Loyalty * 100f)));
            }
        }

        private static Kingdom RealmKingdom()
        {
            Kingdom k = SelectedMetas.selected_kingdom;
            if (!G.Ok(k) || !k.isCiv())
            {
                k = G.Kingdoms().Where(x => x.isCiv()).OrderByDescending(x => Research.Era(x)).FirstOrDefault();
            }
            return G.Ok(k) && Research.Era(k) >= 13 ? k : null;
        }

        private void Realm(Transform parent)
        {
            Kingdom k = RealmKingdom();
            if (k == null) return;
            Transform[] cols = Cols(parent, "Realm");
            Transform c = cols[0];
            UiKit.Header(c, G.Name(k), UiKit.Hex(G.Hex(k)));
            int scale = CosmosSim.CachedScale(k);
            UiKit.Stat(c, null, Txt.T("cos_scale"), Txt.T("cos_scale_" + scale), UiKit.Gold);
            UiKit.Stat(c, null, Txt.T("cos_kardashev"), Txt.T("cos_kardashev_" + Kardashev.Type(k)));
            UiKit.Stat(c, null, Txt.T("cos_drive"), Txt.T("cos_drive_" + CosmosSim.Drive(k)));
            var mine = Cosmos.Of(k).ToList();
            UiKit.Stat(c, null, Txt.T("cos_colonies"), Txt.F("cos_power_line", mine.Count, Pop(mine.Sum(p => p.Pop))));
            int travel = Cosmos.St.Voyages.Count(v => v.King == Cosmos.Key(k));
            if (travel > 0) UiKit.Stat(c, null, Txt.T("cos_voyages"), travel.ToString(), UiKit.Cyan);
            UiKit.Stat(c, null, Txt.T("cos_fleet"), Mathf.RoundToInt(CosmosWar.Strength(k)).ToString());
            Dictionary<string, float> stock = CosmosSim.Stock(k);
            if (stock.Count > 0) UiKit.Paragraph(c, Txt.T("cos_resources") + ": " + string.Join(", ", stock.Select(kv => Txt.T("cos_res_" + kv.Key) + " " + kv.Value.ToString("0")).ToArray()), 7, UiKit.Cyan);
            foreach (Threat t in Cosmos.St.Threats.Where(x => x.King == Cosmos.Key(k) && x.Warned)) UiKit.Label(c, Txt.F("cos_threat_line", Txt.T("cos_inv_" + t.Kind), CosmosGen.Get(t.Target).Name, t.Main - G.Year), 14f, 8, TextAnchor.MiddleCenter, UiKit.Bad);
            c = cols[1];
            UiKit.Header(c, Txt.T("cos_mega_admin"), UiKit.Gold);
            UiKit.Label(c, Megastructures.Used(k) + " / " + Megastructures.Admin(k) + "   " + Txt.F("cos_mega_logistics", Megastructures.Logistics(k)), 14f, 8, TextAnchor.MiddleCenter, UiKit.Soft);
            foreach (Mega m in Megastructures.Of(k))
            {
                MegaDef md = Megastructures.Def(m.Id);
                string stage = md != null ? "  " + Txt.F("cos_mega_stage", Megastructures.Stage(m, md), md.Stages) : "";
                RectTransform mb = UiKit.Box(c, "Mega", 16f);
                UiKit.Img(mb, UiKit.Icon("ui/Icons/civ/mega/" + m.Id, "ui/Icons/civ/world/cosmos"), new Vector2(14f, 14f), new Vector2(-UiKit.W / 2f + 14f, 0f));
                UiKit.Bar(mb, m.Progress, m.Done ? new Color(1f, 0.8f, 0.4f) : new Color(0.5f, 0.75f, 1f), new Vector2(UiKit.W - 38f, 10f), new Vector2(9f, 0f), Txt.T("cos_mega_" + m.Id) + (m.Done ? "" : stage + "  " + Mathf.RoundToInt(m.Progress * 100f) + "%"));
            }
            int ruins = Cosmos.St.Megas.Count(x => x.Ruin);
            if (ruins > 0) UiKit.Stat(c, null, Txt.T("cos_mega_ruins"), ruins.ToString(), UiKit.Dim);
            var battles = Cosmos.St.Battles.Where(b => Cosmos.Now - b.Year < 30).Reverse().Take(4).ToList();
            if (battles.Count > 0)
            {
                UiKit.Header(c, Txt.T("cos_battles"), UiKit.Bad);
                foreach (Battle b in battles) UiKit.Paragraph(c, Txt.F("cos_battle_line", b.Year, b.A, b.B, CosmosGen.Get(b.Node).Name, b.Win), 7, UiKit.Soft);
            }
            if (Cosmos.St.Log.Count > 0)
            {
                UiKit.Header(c, Txt.T("cos_recent"), UiKit.Gold);
                foreach (string line in Enumerable.Reverse(Cosmos.St.Log).Take(6))
                {
                    int i = line.IndexOf('|');
                    UiKit.Paragraph(c, i > 0 ? "<color=#FFD25C>" + line.Substring(0, i) + "</color>  " + line.Substring(i + 1) : line, 7, UiKit.Soft);
                }
            }
            Fit(cols);
        }

        public static string Pop(float millions)
        {
            if (millions <= 0f) return "0";
            if (millions < 0.001f) return Mathf.Max(1, Mathf.RoundToInt(millions * 1e6f)).ToString();
            if (millions < 1f) return Mathf.RoundToInt(millions * 1000f) + " k";
            if (millions < 1000f) return millions.ToString("0.#") + " M";
            if (millions < 1e6f) return (millions / 1000f).ToString("0.#") + " B";
            return (millions / 1e6f).ToString("0.#") + " T";
        }

        private void Children(Transform c, CNode n)
        {
            List<CNode> kids = CosmosGen.Children(n);
            if (kids.Count == 0) return;
            int cols = Wide ? 2 : 1;
            int per = 6 * cols;
            int pages = Math.Max(1, (kids.Count + per - 1) / per);
            _page = Math.Min(_page, pages - 1);
            float rw = Wide ? _cw : UiKit.W;
            float bw = rw / cols - 6f;
            RectTransform box = null;
            int i = 0;
            foreach (CNode k in kids.Skip(_page * per).Take(per))
            {
                CNode kk = k;
                if (i % cols == 0) box = UiKit.Box(c, "Kid", 18f, rw);
                float x = cols == 1 ? 0f : (i % 2 == 0 ? -rw / 4f : rw / 4f);
                Polity p = Cosmos.OwnerExact(k.Path);
                string label = (CosmosGen.Contains(k.Path, Cosmos.St.Home) ? "★ " : "") + k.Name;
                SimpleButton b = UiKit.Btn(box, label, null, () => Go(kk.Path), new Vector2(bw, 16f), new Vector2(x, 0f), k.Name, Tip(k));
                if (p != null) b.Background.color = Color.Lerp(Color.white, UiKit.Hex(p.Color), 0.5f);
                i++;
            }
            UiKit.Pager(c, _page, pages, x =>
            {
                _page = x;
                Render();
            });
        }

        private void Powers(Transform c, CNode n)
        {
            var list = PowerList(n);
            if (list.Count == 0) return;
            Transform[] cols = Cols(c, "Powers");
            int i = 0;
            foreach (var g in list)
            {
                Transform col = cols[i++ % 2];
                Polity first = g.First();
                Kingdom k = first.King != 0 ? Cosmos.KingOf(first.King) : null;
                string name = G.Ok(k) ? G.Name(k) : first.Name;
                float pop = g.Sum(p => p.Pop);
                int worlds = g.Sum(p => p.Nodes.Count(x => CosmosGen.Contains(n.Path, x)));
                UiKit.Stat(col, null, name, Txt.F("cos_power_line", worlds, Pop(pop)), UiKit.Hex(first.Color));
            }
            Fit(cols);
        }

        private static List<IGrouping<string, Polity>> PowerList(CNode n)
        {
            return Cosmos.St.Polities.Where(p => p.Nodes.Any(x => CosmosGen.Contains(n.Path, x))).GroupBy(p => p.King != 0 ? "k" + p.King : "p" + p.Id).Take(12).ToList();
        }
    }
}
