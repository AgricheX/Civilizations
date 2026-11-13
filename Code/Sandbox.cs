using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using NeoModLoader.General;
using UnityEngine;

namespace Civilizations
{
    internal class SbTool
    {
        public string Id = "";
        public string Cat = "";
        public string Name = "";
        public string Desc = "";
        public string Icon = "";
        public int Era = -1;
        public bool Heavy;
        public Func<WorldTile, bool> Act;
    }

    internal static class Sandbox
    {
        public const string PowerId = "civ_sandbox_tool";
        public static readonly string[] Cats = { "units", "build", "brush", "kingdom", "city", "events", "cosmos" };
        public static readonly List<SbTool> Tools = new List<SbTool>();
        public static readonly Dictionary<string, SbTool> ById = new Dictionary<string, SbTool>();
        public static SbTool Current;
        public static PowerButton Button;
        public static PowersTab Tab;
        private static readonly List<string> _favs = new List<string>();
        private static readonly Dictionary<string, PowerButton> _favButtons = new Dictionary<string, PowerButton>();
        private static readonly Dictionary<long, int> _noticed = new Dictionary<long, int>();
        private static string _armed;

        public static bool On
        {
            get { return CivMod.C == null || CivMod.C.Sandbox; }
        }

        public static Func<bool> Blocked = () => Player.Active;

        public static void Disarm()
        {
            Current = null;
            _armed = null;
        }

        public static void InitAssets()
        {
            if (AssetManager.powers.get(PowerId) != null) return;
            var p = new GodPower
            {
                id = PowerId,
                name = PowerId,
                path_icon = "ui/Icons/civ/btn/sandbox",
                force_brush = "circ_0",
                unselect_when_window = true,
                show_tool_sizes = false,
                hold_action = false,
                click_action = Click
            };
            AssetManager.powers.add(p);
        }

        private static SbTool Add(string cat, string id, string name, string desc, string icon, int era, Func<WorldTile, bool> act, bool heavy = false)
        {
            var t = new SbTool { Id = cat + ":" + id, Cat = cat, Name = name, Desc = desc, Icon = icon, Era = era, Act = act, Heavy = heavy };
            Tools.Add(t);
            ById[t.Id] = t;
            return t;
        }

        public static void Build()
        {
            Tools.Clear();
            ById.Clear();
            foreach (UnitDef u in Db.Units)
            {
                UnitDef d = u;
                string art = d.Id.StartsWith("civ_") ? d.Id.Substring(4) : d.Id;
                Add("units", d.Id, Txt.T(d.Id), Txt.T("sb_cls_" + d.Cls), "ui/Icons/civ/unit/" + art, d.Era, t => SpawnUnit(t, d.Id));
            }
            foreach (BuildDef b in Buildings.Defs.Values)
            {
                BuildDef d = b;
                TechDef tech = Db.Tech(d.Tech);
                Add("build", d.Id, Txt.T(d.Id), Txt.T("sb_build_d"), tech != null ? tech.Icon : "ui/Icons/civ/btn/state", tech != null ? tech.Era : -1, t => PlaceBuilding(t, d.Id));
            }
            for (int i = 0; i < Walls.Tiers; i++)
            {
                int tier = i;
                Add("build", "wall_" + i, Txt.T("civ_wall_" + i), Txt.T("sb_wall_d"), "ui/Icons/civ/sb/wall_" + Math.Min(3, i), -1, t => Walls.SandboxPlace(t, tier, Owner(t)));
            }
            Add("build", "wall_border", Txt.T("civ_border_wall"), Txt.T("sb_wall_d"), "ui/Icons/civ/sb/wall_border", -1, t => Walls.SandboxPlace(t, -1, Owner(t)));
            Add("build", "wall_shield", Txt.T("sb_wall_shield"), Txt.T("sb_wall_d"), "ui/Icons/civ/sb/wall_shield", -1, t => Walls.SandboxPlace(t, 99, Owner(t)));
            Add("brush", "eraser", Txt.T("sb_eraser"), Txt.T("sb_eraser_d"), "ui/Icons/civ/sb/eraser", -1, Erase);
            Add("brush", "pollute", Txt.T("sb_pollute"), Txt.T("sb_pollute_d"), "ui/Icons/civ/sb/pollute", -1, t => CityAct(t, c => Pollution.Emit(c, 25f)));
            Add("brush", "clean", Txt.T("sb_clean"), Txt.T("sb_clean_d"), "ui/Icons/civ/sb/clean", -1, t => CityAct(t, c => Pollution.Emit(c, -25f)));
            Add("brush", "reveal", Txt.T("sb_reveal"), Txt.T("sb_reveal_d"), "ui/Icons/civ/sb/reveal", -1, t => KingdomAct(t, k => Fog.Explore(k, 12)));
            Add("kingdom", "era_up", Txt.T("sb_era_up"), Txt.T("sb_era_up_d"), "ui/Icons/civ/sb/era_up", -1, t => KingdomAct(t, k => Research.SetupEra(k, Math.Min(Db.LastEra, Research.Era(k) + 1))));
            Add("kingdom", "era_down", Txt.T("sb_era_down"), Txt.T("sb_era_down_d"), "ui/Icons/civ/sb/era_down", -1, t => KingdomAct(t, k => Research.SetupEra(k, Math.Max(0, Research.Era(k) - 1))));
            for (int e = 0; e < Db.Eras.Count; e++)
            {
                int era = e;
                Add("kingdom", "era_" + e, Txt.F("sb_era_to", Db.EraName(e)), Txt.T("sb_era_to_d"), "ui/Icons/civ/era/" + Db.Eras[e].Id, e, t => KingdomAct(t, k => Research.SetupEra(k, era)), true);
            }
            foreach (PolicyDef p in Db.Policies)
            {
                PolicyDef d = p;
                Add("kingdom", "policy_" + d.Id, Txt.T("civ_p_" + d.Id), Txt.T("sb_policy_" + d.Group), "ui/Icons/civ/policy/" + d.Id, -1, t => KingdomAct(t, k => Policies.Set(k, d.Group, d.Id, "sandbox")));
            }
            Add("kingdom", "legit_up", Txt.T("sb_legit_up"), Txt.T("sb_legit_d"), "ui/Icons/civ/sb/legit_up", -1, t => KingdomAct(t, k => Store.Put(k.data, Legitimacy.KLegit, G.Clamp(Legitimacy.Get(k) + 0.25f, 0f, 1f))));
            Add("kingdom", "dip_peace", Txt.T("sb_dip_peace"), Txt.T("sb_dip_peace_d"), "ui/Icons/civ/sb/dip_peace", -1, t => KingdomAct(t, Statecraft.ForcePeace));
            Add("kingdom", "dip_crisis", Txt.T("sb_dip_crisis"), Txt.T("sb_dip_crisis_d"), "ui/Icons/civ/sb/dip_crisis", -1, t => KingdomAct(t, Statecraft.ForceCrisis));
            Add("kingdom", "dip_coalition", Txt.T("sb_dip_coalition"), Txt.T("sb_dip_coalition_d"), "ui/Icons/civ/sb/dip_coalition", -1, t => KingdomAct(t, k => Statecraft.Form(k)));
            Add("kingdom", "dip_calm", Txt.T("sb_dip_calm"), Txt.T("sb_dip_calm_d"), "ui/Icons/civ/sb/dip_calm", -1, t => KingdomAct(t, k => Statecraft.SetAe(k, 0f)));
            Add("kingdom", "dip_exhaust", Txt.T("sb_dip_exhaust"), Txt.T("sb_dip_exhaust_d"), "ui/Icons/civ/sb/dip_exhaust", -1, t => KingdomAct(t, k => Statecraft.SetExh(k, 100f)));
            Add("kingdom", "dip_rest", Txt.T("sb_dip_rest"), Txt.T("sb_dip_rest_d"), "ui/Icons/civ/sb/dip_rest", -1, t => KingdomAct(t, k => Statecraft.SetExh(k, 0f)));
            Add("kingdom", "dip_dynasty", Txt.T("sb_dip_dynasty"), Txt.T("sb_dip_dynasty_d"), "ui/Icons/civ/sb/dip_dynasty", -1, t => KingdomAct(t, Statecraft.Advance));
            Add("kingdom", "dip_deadlock", Txt.T("sb_dip_deadlock"), Txt.T("sb_dip_deadlock_d"), "ui/Icons/civ/sb/dip_deadlock", -1, t => KingdomAct(t, Statecraft.Deadlock));
            Add("kingdom", "legit_down", Txt.T("sb_legit_down"), Txt.T("sb_legit_d"), "ui/Icons/civ/sb/legit_down", -1, t => KingdomAct(t, k => Store.Put(k.data, Legitimacy.KLegit, G.Clamp(Legitimacy.Get(k) - 0.25f, 0f, 1f))));
            foreach (string r in Db.Resources)
            {
                string res = r;
                string art = Db.ResourceArt.ContainsKey(res) ? Db.ResourceArt[res] : res.Replace("civ_", "");
                Add("city", "give_" + res, Txt.F("sb_give", Txt.T("civ_res_" + res.Replace("civ_", ""))), Txt.T("sb_give_d"), "ui/Icons/civ/res/" + art, -1, t => CityAct(t, c => Economy.Give(c, res, 50)));
                Add("city", "take_" + res, Txt.F("sb_take", Txt.T("civ_res_" + res.Replace("civ_", ""))), Txt.T("sb_take_d"), "ui/Icons/civ/res/" + art, -1, t => CityAct(t, c => Economy.TakeUpTo(c, res, 50)));
            }
            foreach (EventDef ev in Events.Defs)
            {
                EventDef d = ev;
                Add("events", "ev_" + d.Id, Txt.T("civ_ev_" + d.Id), Txt.T("sb_event_d"), "ui/Icons/civ/log/" + EvIcon(d.Cat), d.Min, t => KingdomAct(t, k => Events.Force(k, d.Id)));
            }
            foreach (SagaDef sg in Sagas.Defs)
            {
                SagaDef d = sg;
                Add("events", "sg_" + d.Id, Sagas.Plain(d), Txt.T("sb_saga_d"), "ui/Icons/civ/log/" + EvIcon(d.Cat), -1, t => KingdomAct(t, k => Sagas.Force(k, d.Id)));
            }
            foreach (FestivalDef f in Festivals.Defs)
            {
                FestivalDef d = f;
                Add("events", "fest_" + d.Id, Txt.T("civ_fest_" + d.Id), Txt.T("civ_fest_" + d.Id + "_d"), "ui/Icons/civ/fest/" + d.Id, -1, t => CityAct(t, c => Festivals.Force(c.kingdom, c, d.Id)));
            }
            foreach (AlienKind a in CosmosLife.Kinds)
            {
                AlienKind kind = a;
                Add("cosmos", "alien_" + kind.Id, Txt.T("cos_alien_" + kind.Id), Txt.T("sb_alien_d"), "ui/Icons/civ/cos/body_" + kind.Homes[0], -1, t => SpawnAlien(kind));
            }
            foreach (MegaDef m in Megastructures.Defs)
            {
                MegaDef d = m;
                Add("cosmos", "mega_" + d.Id, Txt.T("cos_mega_" + d.Id), Txt.T("sb_mega_d"), "ui/Icons/civ/mega/" + d.Id, -1, t => KingdomAct(t, k => StartMega(k, d)));
            }
            Add("cosmos", "invasion", Txt.T("sb_invasion"), Txt.T("sb_invasion_d"), "ui/Icons/civ/sb/invasion", -1, t => KingdomAct(t, k => Invasions.Plan(k, CosmosSim.CachedScale(k))));
            Add("cosmos", "years_10", Txt.F("sb_years", 10), Txt.T("sb_years_d"), "ui/Icons/civ/sb/years_10", -1, t => Advance(10));
            Add("cosmos", "years_100", Txt.F("sb_years", 100), Txt.T("sb_years_d"), "ui/Icons/civ/sb/years_100", -1, t => Advance(100), true);
            foreach (string end in new[] { "transcendence", "heat_death", "big_bang" })
            {
                string kind = end;
                Add("cosmos", "end_" + kind, Txt.T("cos_end_" + kind), Txt.T("sb_end_d"), "ui/Icons/civ/sb/end_" + kind, -1, t => KingdomAct(t, k => Ending.Run(k, kind)), true);
            }
            LoadFavs();
        }

        private static string EvIcon(string cat)
        {
            switch (cat)
            {
                case "econ": return "i_event_econ";
                case "mil": return "i_event_mil";
                case "sci": return "i_event_sci";
                case "nature": return "i_event_world";
                default: return "i_event";
            }
        }

        private static Kingdom Owner(WorldTile t)
        {
            City c = t != null && t.zone != null ? t.zone.city : null;
            return c != null && G.Ok(c.kingdom) ? c.kingdom : null;
        }

        private static City CityAt(WorldTile t)
        {
            return t != null && t.zone != null ? t.zone.city : null;
        }

        private static bool CityAct(WorldTile t, Action<City> a)
        {
            City c = CityAt(t);
            if (c == null || !G.Ok(c.kingdom)) return false;
            a(c);
            return true;
        }

        private static bool KingdomAct(WorldTile t, Action<Kingdom> a)
        {
            Kingdom k = Owner(t);
            if (!G.Ok(k)) return false;
            a(k);
            Profile.Invalidate(k);
            Bonus.Dirty(k);
            return true;
        }

        private static bool SpawnUnit(WorldTile t, string id)
        {
            if (AssetManager.actor_library.get(id) == null) return false;
            Actor v = World.world.units.createNewUnit(id, t, false, 0f, null, null, false, true);
            if (v == null) return false;
            City c = CityAt(t);
            if (c != null && G.Ok(c.kingdom))
            {
                v.setKingdom(c.kingdom);
                v.setCity(c);
            }
            if (Vehicles.Def(v) != null) VehicleNames.Apply(v);
            return true;
        }

        private static bool PlaceBuilding(WorldTile t, string id)
        {
            BuildingAsset a = AssetManager.buildings.get(id);
            if (a == null || t == null || t.Type == null || t.Type.liquid || t.Type.lava) return false;
            Building b = World.world.buildings.addBuilding(a, t, false, true);
            if (b == null) return false;
            Kingdom k = Owner(t);
            if (G.Ok(k)) b.setKingdom(k);
            return true;
        }

        private static bool Erase(WorldTile t)
        {
            if (Walls.SandboxErase(t)) return true;
            Building b = t != null ? t.building : null;
            if (b == null || b.asset == null || !b.asset.id.StartsWith("civ_")) return false;
            b.startDestroyBuilding();
            return true;
        }

        private static bool SpawnAlien(AlienKind kind)
        {
            if (!Cosmos.Ready) return false;
            var r = new System.Random(Environment.TickCount);
            for (int i = 0; i < 40; i++)
            {
                string region = CosmosGen.Ancestor(Cosmos.St.Home, 3);
                string body = CosmosGen.RandomBody(region, r);
                if (body == null || body == Cosmos.St.Home || Cosmos.OwnerExact(body) != null) continue;
                Polity p = CosmosLife.Spawn(kind, CosmosGen.Get(body), r);
                if (p == null) continue;
                Cosmos.Log(Txt.F("sb_log_alien", p.Name, CosmosGen.Get(body).Name));
                return true;
            }
            return false;
        }

        private static void StartMega(Kingdom k, MegaDef d)
        {
            if (!Cosmos.Ready) return;
            Cosmos.St.Megas.Add(new Mega { Id = d.Id, King = Cosmos.Key(k), Node = Megastructures.Site(k, d), Started = G.Year, Progress = 1f, Done = true });
            Bonus.Dirty(k);
        }

        private static bool Advance(int years)
        {
            if (!Cosmos.Ready) return false;
            for (int y = 0; y < years; y += 10)
            {
                int step = Math.Min(10, years - y);
                if (Cosmos.Shared) Cosmos.St.Clock += step;
                CosmosSim.Year(step);
            }
            return true;
        }

        public static void Pick(SbTool t)
        {
            if (t == null) return;
            if (Blocked())
            {
                Notify.Show(Txt.T("sb_blocked"));
                return;
            }
            if (t.Heavy && _armed != t.Id)
            {
                _armed = t.Id;
                Notify.Show(Txt.F("sb_confirm", t.Name));
                return;
            }
            _armed = null;
            Current = t;
            if (Button != null && World.world != null && World.world.selected_buttons != null) World.world.selected_buttons.setPower(Button);
            Notify.Show(Txt.F("sb_armed", t.Name));
        }

        private static bool Click(WorldTile tile, string power)
        {
            if (Current == null || tile == null || !On) return false;
            if (Blocked())
            {
                Notify.Show(Txt.T("sb_blocked"));
                return false;
            }
            bool ok = false;
            try
            {
                ok = Current.Act(tile);
            }
            catch (Exception e)
            {
                CivMod.Report("sandbox-" + Current.Id, e);
            }
            if (ok) Notice(tile, Current.Cat, Current.Name);
            return ok;
        }

        public static void Notice(WorldTile tile, string kind, string what)
        {
            if (CivMod.C != null && !CivMod.C.SandboxNotice) return;
            City c = CityAt(tile);
            Kingdom k = c != null ? c.kingdom : null;
            if (!G.Ok(k) || !k.isCiv()) return;
            int last;
            if (_noticed.TryGetValue(k.data.id, out last) && G.Year - last < 1) return;
            _noticed[k.data.id] = G.Year;
            bool harm = kind == "harm";
            bool help = kind == "help" || kind == "city" || kind == "units" || kind == "build";
            if (harm)
            {
                Moods.Apply(c, "civ_divine_wrath", 0.6f);
                Legitimacy.Shock(k, -0.03f);
            }
            else if (help) Moods.Apply(c, "civ_divine_gift", 0.4f);
            string key = harm ? "sb_news_harm" : help ? "sb_news_help" : "sb_news_odd";
            Internet.Official(k, "world", Txt.F(key, G.Name(k), G.Name(c), what), false);
            if (Research.Era(k) <= 9 && G.Chance(0.3f)) Chronicle.Log("sb_omen", k, G.Name(k), what, G.Name(c), null, null, c, null, false);
        }

        private static readonly string[] HarmWords = { "bomb", "fire", "lightning", "meteor", "acid", "curse", "plague", "tornado", "earthquake", "napalm", "nuke", "czar", "grenade", "lava", "infection", "tumor", "madness", "cloud_evil", "rain_blood", "rain_acid", "inspiration_revert", "zombie", "demon", "skeleton" };
        private static readonly string[] HelpWords = { "blessing", "rain", "fertilizer", "seeds", "spawn", "heal", "shield", "inspiration", "coin", "drop_gold", "friendship", "fruit", "life" };

        [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.clickedFinal))]
        private static class Vanilla
        {
            private static void Postfix(Vector2Int pPos, GodPower pPower)
            {
                if (pPower == null || pPower.id == PowerId || World.world == null) return;
                try
                {
                    WorldTile t = World.world.GetTile(pPos.x, pPos.y);
                    if (t == null) return;
                    string id = pPower.id;
                    string kind = HarmWords.Any(w => id.Contains(w)) ? "harm" : HelpWords.Any(w => id.Contains(w)) ? "help" : null;
                    if (kind == null) return;
                    string name = Txt.Has(id) ? Txt.T(id) : id;
                    Notice(t, kind, name);
                }
                catch (Exception e)
                {
                    CivMod.Report("sandbox-notice", e);
                }
            }
        }

        private static string FavFile
        {
            get { return Path.Combine(Universe.Folder, "sandbox_favorites.txt"); }
        }

        private static void LoadFavs()
        {
            _favs.Clear();
            try
            {
                if (File.Exists(FavFile)) _favs.AddRange(File.ReadAllLines(FavFile).Where(l => l.Length > 0).Take(20));
            }
            catch (Exception)
            {
            }
        }

        public static bool IsFav(SbTool t)
        {
            return t != null && _favs.Contains(t.Id);
        }

        public static void ToggleFav(SbTool t)
        {
            if (t == null) return;
            if (_favs.Remove(t.Id))
            {
                PowerButton b;
                if (_favButtons.TryGetValue(t.Id, out b) && b != null) global::Civilizations.Tab.Remove(b);
                _favButtons.Remove(t.Id);
            }
            else
            {
                if (_favs.Count >= 20) return;
                _favs.Add(t.Id);
                AddFavButton(t);
            }
            try
            {
                Directory.CreateDirectory(Universe.Folder);
                File.WriteAllLines(FavFile, _favs.ToArray());
            }
            catch (Exception e)
            {
                CivMod.Report("sandbox-favs", e);
            }
        }

        public static void FavButtons()
        {
            foreach (string id in _favs)
            {
                SbTool t;
                if (ById.TryGetValue(id, out t)) AddFavButton(t);
            }
        }

        private static void AddFavButton(SbTool t)
        {
            if (Tab == null || _favButtons.ContainsKey(t.Id)) return;
            SbTool tool = t;
            PowerButton b = PowerButtonCreator.CreateSimpleButton("civ_fav_" + t.Id.Replace(":", "_"), () => Pick(tool), UiKit.Icon(t.Icon));
            global::Civilizations.Tab.Put(global::Civilizations.Tab.GFavs, b);
            _favButtons[t.Id] = b;
            if (global::Civilizations.Tab.Ready) global::Civilizations.Tab.Relayout();
        }

        public static IEnumerable<string> Favs()
        {
            return _favs;
        }
    }
}
