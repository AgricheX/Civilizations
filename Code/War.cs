using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Civilizations
{
    internal static class War
    {
        public const string KAmmo = "civ_ammo";
        public const string KFuel = "civ_afuel";
        public const string KNukes = "civ_nukes";
        public const string KNuked = "civ_nuked";
        public const string KWorldNukes = "civ_allow_nukes";
        public const string KSites = "civ_rad_sites";

        private class Site
        {
            public int X;
            public int Y;
            public int Year;
            public int Radius;
        }

        private static readonly List<Site> _sites = new List<Site>();
        private static float _supply;

        public static bool NukesAllowed
        {
            get
            {
                SaveCustomData d = Store.World();
                bool def = CivMod.C != null && CivMod.C.NukesDefault;
                return d == null ? def : Store.Bool(d, KWorldNukes, def);
            }
            set
            {
                SaveCustomData d = Store.World();
                if (d != null) Store.Put(d, KWorldNukes, value);
            }
        }

        public static void Load()
        {
            _sites.Clear();
            SaveCustomData d = Store.World();
            if (d == null) return;
            foreach (string p in Store.Str(d, KSites).Split(';'))
            {
                string[] f = p.Split(':');
                if (f.Length != 4) continue;
                int x, y, yr, r;
                if (int.TryParse(f[0], out x) && int.TryParse(f[1], out y) && int.TryParse(f[2], out yr) && int.TryParse(f[3], out r)) _sites.Add(new Site { X = x, Y = y, Year = yr, Radius = r });
            }
        }

        private static void SaveSites()
        {
            SaveCustomData d = Store.World();
            if (d == null) return;
            Store.Put(d, KSites, string.Join(";", _sites.Select(s => s.X + ":" + s.Y + ":" + s.Year + ":" + s.Radius)));
        }

        public static int RadiationSites { get { return _sites.Count; } }

        public static bool HasRadiationNear(City c)
        {
            WorldTile t = G.Tile(c);
            if (t == null) return false;
            foreach (Site s in _sites)
            {
                if (Globe.SqDist(t.x, t.y, s.X, s.Y) < (s.Radius + 20) * (s.Radius + 20)) return true;
            }
            return false;
        }

        public static bool UsedNukes(Kingdom k)
        {
            if (k == null || k.data == null) return false;
            int y = Store.Int(k.data, KNuked, int.MinValue);
            return y != int.MinValue && G.Year - y < 200;
        }

        public static int Nukes(Kingdom k)
        {
            return k == null || k.data == null ? 0 : Store.Int(k.data, KNukes, 0);
        }

        public static bool WantsSilo(City c)
        {
            if (!G.Ok(c) || c.kingdom == null || !NukesAllowed || !Research.HasFeature(c.kingdom, "icbm")) return false;
            int silos = 0;
            foreach (City o in G.Cities(c.kingdom)) silos += o.countBuildingsType("type_civ_silo");
            return silos < 2 && c.countBuildingsType("type_civ_silo") == 0;
        }

        public static void Tick(float dt)
        {
            _supply += dt;
            if (_supply < 3f) return;
            _supply = 0f;
            Supply();
        }

        private static void Supply()
        {
            if (World.world.armies == null) return;
            foreach (Army army in World.world.armies.list.ToList())
            {
                if (army == null || !army.isAlive() || army.data == null) continue;
                Kingdom k = army.getKingdom();
                if (k == null || !k.isCiv() || !Research.Has(k, "arquebus")) continue;
                Actor cap = army.getCaptain();
                if (!G.Ok(cap) || cap.current_tile == null) continue;
                int men = 0, fuelUsers = 0;
                foreach (Actor a in army.units)
                {
                    if (!G.Ok(a)) continue;
                    men++;
                    UnitDef d = Vehicles.Def(a);
                    if (d != null && (d.Fuel == "fuel" || d.Fuel == "coal")) fuelUsers++;
                }
                if (men == 0) continue;
                float ammo = Store.Flt(army.data, KAmmo, men * 4f);
                float fuel = Store.Flt(army.data, KFuel, fuelUsers * 4f);
                City here = cap.current_tile.zone != null ? cap.current_tile.zone.city : null;
                if (here != null && here.kingdom == k)
                {
                    int wantAmmo = (int)Math.Max(0f, men * 6f - ammo);
                    int gotAmmo = Math.Min(wantAmmo, Economy.Amount(here, "civ_ammo"));
                    if (gotAmmo > 0 && Economy.Take(here, "civ_ammo", gotAmmo)) ammo += gotAmmo;
                    if (fuelUsers > 0)
                    {
                        int wantFuel = (int)Math.Max(0f, fuelUsers * 6f - fuel);
                        int gotFuel = Math.Min(wantFuel, Economy.Amount(here, "civ_fuel") + Economy.Amount(here, "civ_coal"));
                        if (gotFuel > 0)
                        {
                            int f1 = Math.Min(gotFuel, Economy.Amount(here, "civ_fuel"));
                            Economy.Take(here, "civ_fuel", f1);
                            Economy.Take(here, "civ_coal", gotFuel - f1);
                            fuel += gotFuel;
                        }
                    }
                }
                else if (World.world.wars.hasWars(k))
                {
                    ammo -= men * 0.06f;
                    fuel -= fuelUsers * 0.08f;
                }
                ammo = Math.Max(0f, ammo);
                fuel = Math.Max(0f, fuel);
                Store.Put(army.data, KAmmo, ammo);
                Store.Put(army.data, KFuel, fuel);
                bool noAmmo = ammo <= 0.01f;
                bool noFuel = fuelUsers > 0 && fuel <= 0.01f;
                if (!noAmmo && !noFuel) continue;
                foreach (Actor a in army.units)
                {
                    if (!G.Ok(a)) continue;
                    UnitDef d = Vehicles.Def(a);
                    bool gun = a.getWeaponAsset() != null && a.getWeaponAsset().equipment_subtype == "civ_gun";
                    if (noFuel && d != null && (d.Fuel == "fuel" || d.Fuel == "coal") || noAmmo && (gun || d != null && Vehicles.Military(d))) { if (!a.hasStatus("civ_unsupplied")) a.addStatusEffect("civ_unsupplied", 6f); }
                }
            }
        }

        public static void Year()
        {
            for (int i = _sites.Count - 1; i >= 0; i--)
            {
                if (G.Year - _sites[i].Year > 300) _sites.RemoveAt(i);
            }
            foreach (Kingdom k in G.Kingdoms())
            {
                CivMod.Safe("missiles", () => Missiles.Year(k));
                if (!NukesAllowed || !Research.HasFeature(k, "nuke")) continue;
                BuildNukes(k);
                if (Nukes(k) > 0 && World.world.wars.hasWars(k)) TryLaunch(k);
            }
        }

        private static void BuildNukes(Kingdom k)
        {
            int cap = Research.HasFeature(k, "icbm") ? 6 : 3;
            if (Nukes(k) >= cap || k.capital == null) return;
            City c = k.capital;
            if (!c.hasBuildingType("type_civ_factory") && !c.hasBuildingType("type_civ_lab")) return;
            if (!Economy.Has(c, "civ_uranium", 10) || !Economy.Has(c, "civ_steel", 15) || Economy.Amount(c, "gold") < 300) return;
            if (!G.Chance(0.05f + Math.Max(0f, Politics.Aggression(k)) * 0.1f)) return;
            Economy.Take(c, "civ_uranium", 10);
            Economy.Take(c, "civ_steel", 15);
            Economy.Take(c, "gold", 300);
            Store.Put(k.data, KNukes, Nukes(k) + 1);
            Internet.OnNukeBuilt(k);
        }

        private static void TryLaunch(Kingdom k)
        {
            float desperation = 0f;
            int lost = Store.Int(k.data, "civ_lost_cities", 0);
            int cities = k.countCities();
            Store.Put(k.data, "civ_lost_cities", cities);
            if (lost > cities) desperation += 0.15f * (lost - cities);
            desperation += Math.Max(0f, Politics.Aggression(k)) * 0.1f;
            if (Politics.Stability(k) < 0.3f) desperation += 0.05f;
            if (Policies.Id(k, "ideo") == "environmentalism" || Policies.Id(k, "ideo") == "humanism") desperation *= 0.3f;
            if (!G.Chance(Math.Min(0.25f, desperation))) return;
            City target = null;
            int best = 0;
            foreach (Kingdom e in World.world.wars.getEnemiesOf(k))
            {
                foreach (City c in G.Cities(e))
                {
                    int p = c.getPopulationPeople();
                    if (p > best)
                    {
                        best = p;
                        target = c;
                    }
                }
            }
            if (target == null) return;
            Launch(k, target);
        }

        public static City SiloCity(Kingdom k, City target)
        {
            City best = null;
            float bd = float.MaxValue;
            foreach (City c in G.Cities(k))
            {
                if (c.countBuildingsType("type_civ_silo") == 0) continue;
                float d = G.Dist(c, target);
                if (d < bd)
                {
                    bd = d;
                    best = c;
                }
            }
            return best;
        }

        public static void Launch(Kingdom k, City target)
        {
            WorldTile t = G.Tile(target);
            if (t == null) return;
            Store.Put(k.data, KNukes, Math.Max(0, Nukes(k) - 1));
            Store.Put(k.data, KNuked, G.Year);
            City silo = Research.HasFeature(k, "icbm") ? SiloCity(k, target) : null;
            if (silo != null)
            {
                WorldTile from = G.Tile(silo);
                Transit.LaunchMissile(from, t, () => Detonate(k, target, t));
            }
            else
            {
                Transit.Flyover(G.Tile(k.capital) ?? t, t, "bomber", () => Detonate(k, target, t));
            }
            Internet.OnNukeLaunched(k, target, silo != null);
        }

        private static void Detonate(Kingdom k, City target, WorldTile t)
        {
            if (target != null && target.isAlive())
            {
                int bunkers = target.countBuildingsType("type_civ_bunker");
                if (bunkers > 0)
                {
                    float share = Math.Min(0.7f, bunkers * 0.25f);
                    foreach (Actor a in target.units)
                    {
                        if (G.Ok(a) && G.Chance(share)) a.addStatusEffect("invincible", 12f);
                    }
                }
            }
            World.world.drop_manager.spawn(t, "atomic_bomb", 15f, -1f, -1L);
            _sites.Add(new Site { X = t.x, Y = t.y, Year = G.Year, Radius = 30 });
            SaveSites();
            Internet.OnNuke(k, target);
            foreach (Kingdom o in G.Kingdoms())
            {
                if (o == k) continue;
                foreach (City c in G.Cities(o)) Moods.Apply(c, "civ_nuke_fear", 0.2f);
                if (!World.world.wars.isInWarWith(o, k) && Politics.Aggression(o) > 0.05f && G.Chance(0.15f))
                {
                    WarTypeAsset wt = AssetManager.war_types_library.get("normal");
                    if (wt != null) World.world.wars.newWar(o, k, wt);
                }
            }
        }

        public static void SlowTick(float dt)
        {
            if (_sites.Count == 0) return;
            foreach (Site s in _sites)
            {
                float age = G.Year - s.Year;
                float strength = Math.Max(0f, 1f - age / 300f);
                int r = (int)(s.Radius * (0.4f + 0.6f * strength));
                if (r <= 0) continue;
                for (int i = 0; i < 12; i++)
                {
                    WorldTile t = Globe.Tile(s.X + UnityEngine.Random.Range(-r, r), s.Y + UnityEngine.Random.Range(-r, r));
                    if (t == null) continue;
                    foreach (Actor a in Finder.getUnitsFromChunk(t, 1, 8f))
                    {
                        if (G.Ok(a) && G.Chance(strength * 0.5f)) a.addStatusEffect("civ_radiation", 60f);
                    }
                }
            }
        }
    }
}
