using System;
using System.Collections.Generic;
using System.Linq;
using NeoModLoader.General;
using UnityEngine;

namespace Civilizations
{
    internal static class SandboxTabs
    {
        public const string GCatalog = "sb_catalog";
        public static readonly string[] Groups = { "army", "realm", "dynasty", "diplo", "alliance", "cities", "economy", "construct", "disaster", "cosmos" };
        private static readonly Color[] Tints =
        {
            new Color(1f, 0.55f, 0.5f), new Color(1f, 0.8f, 0.45f), new Color(0.95f, 0.7f, 1f), new Color(0.6f, 0.85f, 1f), new Color(0.55f, 1f, 0.85f),
            new Color(0.75f, 1f, 0.6f), new Color(1f, 0.95f, 0.5f), new Color(0.8f, 0.75f, 0.65f), new Color(1f, 0.6f, 0.75f), new Color(0.65f, 0.7f, 1f)
        };
        private static readonly Dictionary<string, List<string>> _layout = new Dictionary<string, List<string>>();

        public static void Build(PowersTab tab)
        {
            if (tab == null) return;
            Register();
            Tab.Group(tab, GCatalog, new Color(0.85f, 0.85f, 0.9f));
            for (int i = 0; i < Groups.Length; i++) Tab.Group(tab, "sb_" + Groups[i], Tints[i]);
            Tab.Group(tab, Tab.GFavs, new Color(1f, 0.85f, 0.3f));
            Sandbox.Button = PowerButtonCreator.CreateGodPowerButton(Sandbox.PowerId, UiKit.Icon("ui/Icons/civ/btn/sandbox"));
            Tab.Put(GCatalog, Sandbox.Button);
            for (int i = 0; i < Sandbox.Cats.Length; i++)
            {
                int cat = i;
                Tab.Add(GCatalog, "civ_btn_sb_" + Sandbox.Cats[i], "ui/Icons/civ/sb/" + Sandbox.Cats[i], () => WinSandbox.Open(cat));
            }
            foreach (string g in Groups)
            {
                List<string> ids;
                if (!_layout.TryGetValue(g, out ids)) continue;
                foreach (string id in ids)
                {
                    SbTool t;
                    if (!Sandbox.ById.TryGetValue(id, out t)) continue;
                    SbTool tool = t;
                    string key = "civ_sbq_" + t.Id.Replace(":", "_");
                    Tab.Label(key, t.Name, t.Desc);
                    Tab.Put("sb_" + g, PowerButtonCreator.CreateSimpleButton(key, () => Sandbox.Pick(tool), UiKit.Icon(t.Icon)));
                }
            }
            Sandbox.FavButtons();
        }

        public static int Count
        {
            get { return _layout.Values.Sum(l => l.Count(id => Sandbox.ById.ContainsKey(id))); }
        }

        private static void Place(string group, string id)
        {
            List<string> l;
            if (!_layout.TryGetValue(group, out l)) _layout[group] = l = new List<string>();
            if (!l.Contains(id)) l.Add(id);
        }

        private static void Q(string group, string cat, string id, string icon, Func<WorldTile, bool> act, bool heavy = false)
        {
            Sandbox.Extra(cat, "q_" + id, Txt.T("sbq_" + id), Txt.T("sbq_" + id + "_d"), icon, -1, act, heavy);
            Place(group, cat + ":q_" + id);
        }

        private static void Ref(string group, string id)
        {
            Place(group, id);
        }

        private static void K(string group, string id, string icon, Action<Kingdom> a, bool heavy = false)
        {
            Q(group, "kingdom", id, icon, t => Sandbox.KingdomAct(t, a), heavy);
        }

        private static void KB(string group, string id, string icon, Func<Kingdom, bool> a, bool heavy = false)
        {
            Q(group, "kingdom", id, icon, t =>
            {
                Kingdom k = Sandbox.Owner(t);
                if (!G.Ok(k) || !a(k)) return false;
                Profile.Invalidate(k);
                Bonus.Dirty(k);
                return true;
            }, heavy);
        }

        private static void C(string group, string id, string icon, Func<City, bool> a, bool heavy = false)
        {
            Q(group, "city", id, icon, t =>
            {
                City c = Sandbox.CityAt(t);
                return c != null && G.Ok(c.kingdom) && a(c);
            }, heavy);
        }

        private static void Hooked(string group, string id, string icon, Func<Kingdom, bool> fallback, bool heavy = false)
        {
            KB(group, id, icon, k => Sandbox.Hook(id, k) || (fallback != null && fallback(k)), heavy);
        }

        private static void Register()
        {
            _layout.Clear();

            Army("soldier", "ui/Icons/civ/unit/sol_hoplite", d => d.Cls == "soldier" && d.Role == "melee", 1);
            Q("army", "units", "general", "ui/Icons/civ/great/general", t =>
            {
                Kingdom k = Sandbox.Owner(t);
                return G.Ok(k) && Events.Great(k, "general", Sandbox.CityAt(t)) != null;
            });
            Q("army", "units", "army", "ui/Icons/civ/log/military", t =>
            {
                bool any = false;
                any |= Spawn(t, d => d.Cls == "soldier" && d.Role == "melee", 4);
                any |= Spawn(t, d => d.Cls == "soldier" && d.Role == "ranged", 3);
                any |= Spawn(t, d => d.Cls == "soldier" && d.Role == "heavy", 2);
                any |= Spawn(t, d => d.Cls == "siege", 1);
                return any;
            }, true);
            Army("siege", "ui/Icons/civ/unit/catapult", d => d.Cls == "siege", 1);
            Army("cavalry", "ui/Icons/civ/unit/horse", d => d.Cls == "mount", 3);
            Army("tank", "ui/Icons/civ/unit/tank", d => d.Cls == "tank", 1);
            Army("plane", "ui/Icons/civ/unit/fighter", d => d.Cls == "air" && d.Role == "fighter", 1);
            Army("warship", "ui/Icons/civ/unit/ironclad", d => d.Cls == "ship" && d.Role == "war", 1);
            Army("starship", "ui/Icons/civ/unit/fusion_cruiser", d => d.Cls == "space" && (d.Role == "line" || d.Role == "capital"), 1);
            Q("army", "units", "mercs", "ui/Icons/civ/econ/rich", t =>
            {
                Kingdom best = G.Kingdoms().OrderByDescending(Research.Era).FirstOrDefault();
                if (!G.Ok(best)) return false;
                List<UnitDef> o = Vehicles.Options(best, d => d.Cls == "soldier" && d.Role == "heavy");
                if (o.Count == 0) return false;
                UnitDef top = o.OrderByDescending(d => d.Era).First();
                bool any = false;
                for (int i = 0; i < 4; i++) any |= Sandbox.SpawnUnit(Near(t, i), top.Id);
                return any;
            });

            C("realm", "found_kingdom", "ui/Icons/civ/log/i_independence", c => Independent(c, false) != null);
            C("realm", "found_tribe", "ui/Icons/civ/log/i_stage", c => Independent(c, true) != null);
            KB("realm", "split", "ui/Icons/civ/log/i_civil_war", k =>
            {
                if (G.Cities(k).Count < 2) return false;
                Legitimacy.CivilWar(k, Profile.Get(k));
                return true;
            }, true);
            KB("realm", "unite", "ui/Icons/civ/log/i_merger", k =>
            {
                Kingdom o = Nearest(k, x => true);
                if (!G.Ok(o)) return false;
                foreach (City c in G.Cities(o).ToList()) c.joinAnotherKingdom(k, true);
                return true;
            }, true);
            C("realm", "capital", "ui/Icons/civ/state/overview", c =>
            {
                if (c.kingdom.capital == c) return false;
                c.kingdom.setCapital(c);
                return true;
            });
            KB("realm", "gov", "ui/Icons/civ/log/i_gov", k => Cycle(k, "gov"));
            KB("realm", "ideo", "ui/Icons/civ/log/i_ideo", k => Cycle(k, "ideo"));
            Hooked("realm", "coup", "ui/Icons/civ/log/i_coup", k =>
            {
                Events.Force(k, "military_coup");
                return true;
            });
            Hooked("realm", "revolution", "ui/Icons/civ/log/i_revolution", k =>
            {
                Events.Force(k, "peasant_revolt");
                return true;
            });
            KB("realm", "destroy", "ui/Icons/civ/log/i_sack", k =>
            {
                foreach (City c in G.Cities(k).ToList()) c.destroyCity();
                return true;
            }, true);

            KB("realm", "flag_auto", "ui/Icons/civ/log/i_flag", k =>
            {
                FlagEditor.Lock(k, false);
                Flags.Reform(k, "government");
                return true;
            });

            KB("dynasty", "crown", "ui/Icons/civ/state/legit", k => Crown(k, true));
            KB("dynasty", "kill_king", "ui/Icons/civ/log/i_great_dead", k =>
            {
                if (!k.hasKing() || !G.Ok(k.king)) return false;
                k.king.die();
                return true;
            });
            Hooked("dynasty", "dynasty_new", "ui/Icons/civ/log/i_dynasty_renewal", k =>
            {
                if (k.hasKing() && G.Ok(k.king)) k.king.die();
                return Crown(k, false);
            });
            Ref("dynasty", "kingdom:dip_dynasty");
            Hooked("dynasty", "restoration", "ui/Icons/civ/log/i_dynasty_peak", null);
            Hooked("dynasty", "great_rebellion", "ui/Icons/civ/log/i_dynasty_collapse", k =>
            {
                if (G.Cities(k).Count < 2) return false;
                Legitimacy.CivilWar(k, Profile.Get(k));
                return true;
            }, true);
            Ref("dynasty", "kingdom:legit_up");
            Ref("dynasty", "kingdom:legit_down");
            Hooked("dynasty", "heir", "ui/Icons/civ/log/i_great", null);
            Ref("dynasty", "events:ev_succession_war");

            KB("diplo", "war", "ui/Icons/civ/state/war", k =>
            {
                Kingdom o = Nearest(k, x => !World.world.wars.isInWarWith(k, x));
                if (!G.Ok(o)) return false;
                World.world.diplomacy.startWar(k, o, WarTypeLibrary.normal);
                return true;
            });
            Ref("diplo", "kingdom:dip_peace");
            KB("diplo", "vassal", "ui/Icons/civ/state/dep", k =>
            {
                Kingdom o = Nearest(k, x => Diplomacy.Overlord(x) == null && Diplomacy.Overlord(k) != x);
                if (!G.Ok(o)) return false;
                Diplomacy.MakeDependent(o, k, "sandbox");
                return true;
            });
            KB("diplo", "free_vassal", "ui/Icons/civ/log/i_independence", k =>
            {
                if (Diplomacy.Overlord(k) != null)
                {
                    Diplomacy.Release(k, "sandbox");
                    return true;
                }
                List<Kingdom> deps = Diplomacy.DependentsOf(k);
                if (deps.Count == 0) return false;
                Diplomacy.Release(deps[0], "sandbox");
                return true;
            });
            Ref("diplo", "events:ev_independence_movement");
            KB("diplo", "tribute", "ui/Icons/civ/state/treaty", k => Treaty(k, "tribute", 10));
            KB("diplo", "marriage", "ui/Icons/civ/state/relations", k => Treaty(k, "royal_marriage", 20));
            Ref("diplo", "kingdom:dip_calm");
            Ref("diplo", "kingdom:dip_deadlock");
            Ref("diplo", "kingdom:dip_exhaust");

            Hooked("alliance", "ally_new", "ui/Icons/civ/log/i_treaty", k => Ally(k));
            KB("alliance", "ally_join", "ui/Icons/civ/log/i_integrated", k =>
            {
                if (k.getAlliance() != null) return false;
                Kingdom o = Nearest(k, x => x.getAlliance() != null && !x.getAlliance().hasWarsWith(k));
                return G.Ok(o) && o.getAlliance().join(k, true, true);
            });
            KB("alliance", "ally_kick", "ui/Icons/civ/log/i_treaty_broken", k =>
            {
                Alliance a = k.getAlliance();
                if (a == null) return false;
                a.leave(k, true);
                return true;
            });
            Hooked("alliance", "ally_betray", "ui/Icons/civ/log/i_crisis_war", k =>
            {
                Alliance a = k.getAlliance();
                if (a == null) return false;
                Kingdom victim = a.kingdoms_list.FirstOrDefault(x => x != k && G.Ok(x));
                a.leave(k, true);
                if (G.Ok(victim)) World.world.diplomacy.startWar(k, victim, WarTypeLibrary.normal);
                return true;
            }, true);
            Ref("alliance", "kingdom:dip_coalition");
            Hooked("alliance", "ally_bloc", "ui/Icons/civ/log/i_ideo", k => Ally(k));
            Hooked("alliance", "ally_trade", "ui/Icons/civ/econ/trade", k => Ally(k));
            Hooked("alliance", "ally_nuclear", "ui/Icons/civ/log/i_nuke", k => Ally(k));
            Hooked("alliance", "ally_federation", "ui/Icons/civ/state/orgs", k => Ally(k));
            Hooked("alliance", "ally_dissolve", "ui/Icons/civ/log/i_org_end", k =>
            {
                Alliance a = k.getAlliance();
                if (a == null) return false;
                World.world.alliances.dissolveAlliance(a);
                return true;
            }, true);

            Q("cities", "city", "found_city", "ui/Icons/civ/log/i_first", FoundCity);
            C("cities", "grow", "ui/Icons/civ/stat/subjects", c =>
            {
                Actor src = c.units.FirstOrDefault(G.Ok);
                if (src == null) return false;
                WorldTile t = G.Tile(c);
                for (int i = 0; i < 6; i++)
                {
                    Actor v = World.world.units.createNewUnit(src.asset.id, Near(t, i), false, 0f, null, null, false, true);
                    if (v == null) continue;
                    v.setKingdom(c.kingdom);
                    v.setCity(c);
                }
                return true;
            });
            Hooked("cities", "loyal", "ui/Icons/civ/state/stability", k =>
            {
                Politics.AddStability(k, 0.2f);
                return true;
            });
            C("cities", "revolt", "ui/Icons/civ/log/i_revolt", c =>
            {
                Kingdom old = c.kingdom;
                if (G.Cities(old).Count < 2) return false;
                Kingdom nk = Independent(c, false);
                if (!G.Ok(nk)) return false;
                World.world.diplomacy.startWar(old, nk, WarTypeLibrary.rebellion);
                return true;
            }, true);
            Ref("cities", "events:ev_plague");
            C("cities", "festival", "ui/Icons/civ/log/i_festival", c =>
            {
                if (Festivals.Defs.Count == 0) return false;
                Festivals.Force(c.kingdom, c, G.Pick(Festivals.Defs).Id);
                return true;
            });
            C("cities", "governor", "ui/Icons/civ/role/ambassador", c =>
            {
                Actor a = c.units.Where(x => G.Ok(x) && x.isAdult() && !x.isKing() && x != c.leader).OrderBy(x => G.Range(0, 1000)).FirstOrDefault();
                if (a == null) return false;
                if (c.hasLeader()) c.removeLeader();
                c.setLeader(a, true);
                return true;
            });
            C("cities", "transfer", "ui/Icons/civ/log/i_flag", c =>
            {
                Kingdom o = Nearest(c.kingdom, x => true);
                if (!G.Ok(o)) return false;
                c.joinAnotherKingdom(o, true);
                return true;
            });
            C("cities", "ruin", "ui/Icons/civ/log/i_mega_ruined", c =>
            {
                c.destroyCity();
                return true;
            }, true);
            C("cities", "rebuild", "ui/Icons/civ/log/i_recovered", c =>
            {
                foreach (string r in new[] { "wood", "stone", "civ_bricks", "civ_concrete", "civ_steel", "civ_glass", "civ_tools" }) Economy.Give(c, r, 80);
                return true;
            });

            Hooked("economy", "gold", "ui/Icons/civ/econ/rich", k =>
            {
                if (k.capital == null) return false;
                Economy.Give(k.capital, "gold", 500);
                return true;
            });
            Hooked("economy", "bankrupt", "ui/Icons/civ/log/i_bankrupt", k =>
            {
                Events.Force(k, "bubble_crash");
                return true;
            });
            Ref("economy", "events:ev_great_depression");
            Ref("economy", "events:ev_economic_miracle");
            Ref("economy", "events:ev_tycoon");
            K("economy", "currency", "ui/Icons/civ/econ/currency", k => Currency.Create(k, true));
            KB("economy", "tech", "ui/Icons/civ/sb/era_up", k =>
            {
                List<TechDef> c = Research.Candidates(k);
                if (c.Count == 0) return false;
                Research.Learn(k, G.Pick(c), "sandbox");
                return true;
            });
            Ref("economy", "kingdom:era_up");
            Ref("economy", "kingdom:era_down");
            K("economy", "dark_age", "ui/Icons/civ/log/i_age_dark", k =>
            {
                Research.SetupEra(k, Math.Max(0, Research.Era(k) - 2));
                Legitimacy.Shock(k, -0.15f);
            }, true);

            Ref("construct", "build:wall_2");
            Ref("construct", "build:civ_fortress");
            Ref("construct", "build:civ_castle");
            Ref("construct", "build:civ_station");
            Ref("construct", "build:civ_airport");
            Ref("construct", "build:civ_nuclear_plant");
            Ref("construct", "build:civ_launchpad");
            Ref("construct", "build:civ_university");
            Ref("construct", "build:civ_hospital");
            Ref("construct", "brush:eraser");

            Ref("disaster", "events:ev_plague");
            Ref("disaster", "events:ev_famine");
            Ref("disaster", "events:ev_royal_scandal");
            Ref("disaster", "events:ev_great_flood");
            Hooked("disaster", "civil_war", "ui/Icons/civ/log/i_civil_war", k =>
            {
                if (G.Cities(k).Count < 2) return false;
                Legitimacy.CivilWar(k, Profile.Get(k));
                return true;
            }, true);
            Ref("disaster", "events:ev_great_migration");
            Ref("disaster", "cosmos:invasion");
            Ref("disaster", "events:ev_volcanic_winter");
            Ref("disaster", "brush:pollute");
            K("disaster", "saga", "ui/Icons/civ/sb/saga", k =>
            {
                if (Sagas.Defs.Count > 0) Sagas.Force(k, G.Pick(Sagas.Defs).Id);
            });

            Ref("cosmos", "cosmos:years_10");
            Ref("cosmos", "cosmos:years_100");
            foreach (SbTool t in Sandbox.Tools.Where(x => x.Cat == "cosmos" && x.Id.StartsWith("cosmos:alien_")).Take(2).ToList()) Ref("cosmos", t.Id);
            foreach (SbTool t in Sandbox.Tools.Where(x => x.Cat == "cosmos" && x.Id.StartsWith("cosmos:mega_")).Take(1).ToList()) Ref("cosmos", t.Id);
            foreach (string b in new[] { "crystal", "glow", "terraformed", "radiation", "ruins" })
            {
                string id = b;
                if (!Biomes.Has(id)) continue;
                Q("cosmos", "brush", "biome_" + id, "ui/Icons/civ/biome/" + id, t => Biomes.Paint(id, t, 6, 0.95f) > 0);
            }
        }

        public static void AddCosmos(string id)
        {
            Place("cosmos", id);
        }

        private static void Army(string id, string icon, Func<UnitDef, bool> filter, int n)
        {
            Q("army", "units", id, icon, t => Spawn(t, filter, n));
        }

        private static bool Spawn(WorldTile t, Func<UnitDef, bool> filter, int n)
        {
            if (t == null) return false;
            Kingdom k = Sandbox.Owner(t);
            UnitDef d = null;
            if (G.Ok(k))
            {
                List<UnitDef> o = Vehicles.Options(k, filter);
                if (o.Count > 0) d = o.OrderByDescending(x => x.Era).First();
            }
            if (d == null) d = Db.Units.Where(x => filter(x) && AssetManager.actor_library.get(x.Id) != null).OrderBy(x => x.Era).FirstOrDefault();
            if (d == null) return false;
            bool any = false;
            for (int i = 0; i < n; i++) any |= Sandbox.SpawnUnit(Near(t, i), d.Id);
            return any;
        }

        private static WorldTile Near(WorldTile t, int i)
        {
            if (t == null || i == 0) return t;
            WorldTile n = World.world.GetTile(t.x + (i % 3) - 1, t.y + (i / 3 % 3) - 1);
            return n != null && n.Type != null && !n.Type.liquid ? n : t;
        }

        private static Kingdom Nearest(Kingdom k, Func<Kingdom, bool> ok)
        {
            if (!G.Ok(k)) return null;
            WorldTile home = G.Tile(k.capital);
            return G.Kingdoms().Where(x => x != k && G.Ok(x) && x.capital != null && ok(x))
                .OrderBy(x => home == null ? 0f : G.Dist(home, G.Tile(x.capital))).FirstOrDefault();
        }

        private static bool Cycle(Kingdom k, string group)
        {
            List<PolicyDef> o = Policies.Options(k, group);
            if (o.Count < 2) return false;
            string cur = Policies.Id(k, group);
            int i = o.FindIndex(p => p.Id == cur);
            Policies.Set(k, group, o[(i + 1) % o.Count].Id, "sandbox");
            return true;
        }

        private static bool Treaty(Kingdom k, string type, int years)
        {
            Kingdom o = Nearest(k, x => !Diplomacy.Has(k, x, type) && !World.world.wars.isInWarWith(k, x));
            if (!G.Ok(o)) return false;
            Diplomacy.Sign(k, o, type, years);
            return true;
        }

        private static bool Ally(Kingdom k)
        {
            Alliance mine = k.getAlliance();
            Kingdom o = Nearest(k, x => x.getAlliance() == null && !World.world.wars.isInWarWith(k, x));
            if (!G.Ok(o)) return false;
            if (mine != null) return mine.join(o, true, true);
            return World.world.alliances.newAlliance(k, o) != null;
        }

        private static bool Crown(Kingdom k, bool replace)
        {
            City c = k.capital ?? G.Cities(k).FirstOrDefault();
            if (c == null) return false;
            Actor a = c.units.Where(x => G.Ok(x) && x.isAdult() && !x.isKing() && x != k.king).OrderByDescending(x => x.getAge()).FirstOrDefault();
            if (a == null) return false;
            if (k.hasKing())
            {
                if (!replace) return false;
                k.removeKing();
            }
            if (a.isCityLeader() && a.city != null) a.city.removeLeader();
            k.setKing(a);
            return true;
        }

        private static Kingdom Independent(City c, bool tribe)
        {
            if (c == null || !G.Ok(c.kingdom) || G.Cities(c.kingdom).Count < 2) return null;
            Actor leader = c.hasLeader() && G.Ok(c.leader) ? c.leader : c.units.FirstOrDefault(x => G.Ok(x) && x.isAdult() && !x.isKing());
            if (leader == null) return null;
            if (!c.hasLeader()) c.setLeader(leader, true);
            Kingdom nk = c.makeOwnKingdom(leader, true);
            if (!G.Ok(nk)) return null;
            if (tribe)
            {
                Research.SetupEra(nk, 0);
                Store.Put(nk.data, Politics.KStage, "tribe");
                Policies.Set(nk, "gov", "tribal_band", "sandbox");
            }
            return nk;
        }

        private static bool FoundCity(WorldTile t)
        {
            if (t == null || t.zone == null || t.zone.city != null || t.Type == null || t.Type.liquid) return false;
            City near = G.Cities().Where(x => G.Ok(x.kingdom) && x.units.Any(G.Ok)).OrderBy(x => G.Dist(G.Tile(x), t)).FirstOrDefault();
            if (near == null) return false;
            Actor src = near.units.First(G.Ok);
            Actor founder = World.world.units.createNewUnit(src.asset.id, t, false, 0f, null, null, false, true);
            if (founder == null) return false;
            founder.setKingdom(near.kingdom);
            City nc = World.world.cities.buildNewCity(founder, t.zone);
            if (nc == null) return false;
            for (int i = 1; i < 5; i++)
            {
                Actor v = World.world.units.createNewUnit(src.asset.id, Near(t, i), false, 0f, null, null, false, true);
                if (v == null) continue;
                v.setKingdom(nc.kingdom);
                v.setCity(nc);
            }
            return true;
        }
    }
}
