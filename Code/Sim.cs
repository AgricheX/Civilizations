using System;
using System.Collections.Generic;
using UnityEngine;

namespace Civilizations
{
    internal static class Sim
    {
        private const string WorldKey = "civ_world_id";
        private static string _world = "";
        private static int _year = int.MinValue;
        private static readonly Queue<Kingdom> _kq = new Queue<Kingdom>();
        private static readonly Queue<City> _cq = new Queue<City>();
        private static readonly Queue<Clan> _clq = new Queue<Clan>();
        private static float _fast;
        private static float _slow;

        public static string WorldId
        {
            get { return _world; }
        }

        public static void Update()
        {
            CivMod.Safe("tab", Tab.Try);
            CivMod.Safe("ocean-compat", OceanCompat.Try);
            if (!G.Loaded)
            {
                if (_world.Length > 0)
                {
                    _world = "";
                    DayNight.Clear();
                    Transit.Clear();
                }
                return;
            }
            string id = CurrentId();
            if (id != _world)
            {
                _world = id;
                _year = int.MinValue;
                _kq.Clear();
                _cq.Clear();
                _clq.Clear();
                CivMod.Safe("world", OnWorld);
            }
            float dt = Time.unscaledDeltaTime;
            CivMod.Safe("globe", Globe.Check);
            CivMod.Safe("mirror", Mirror.Ensure);
            CivMod.Safe("eralock", () => EraLock.Tick(dt));
            CivMod.Safe("daynight", DayNight.Tick);
            CivMod.Safe("transit", Transit.Frame);
            if (World.world.isPaused()) return;
            int y = G.Year;
            if (y != _year)
            {
                bool first = _year == int.MinValue;
                _year = y;
                foreach (Kingdom k in G.Kingdoms()) _kq.Enqueue(k);
                foreach (City c in G.Cities()) _cq.Enqueue(c);
                foreach (Clan cl in Clans.All()) _clq.Enqueue(cl);
                if (!first)
                {
                    CivMod.Safe("companies", Companies.Year);
                    CivMod.Safe("rails", Rails.Year);
                    CivMod.Safe("era-skins", EraSkins.Year);
                    CivMod.Safe("war-year", War.Year);
                    CivMod.Safe("internet-year", Internet.Year);
                }
            }
            int kb = 2;
            while (kb-- > 0 && _kq.Count > 0)
            {
                Kingdom k = _kq.Dequeue();
                if (!G.Ok(k) || !k.isCiv()) continue;
                int yrs = Elapsed(k.data);
                if (yrs <= 0) continue;
                CivMod.Safe("research", () => Research.Year(k, yrs));
                CivMod.Safe("politics", () => Politics.Year(k, yrs));
                CivMod.Safe("depot", () => Economy.KingdomYear(k, yrs));
                CivMod.Safe("energy", () => Energy.KingdomYear(k, yrs));
                CivMod.Safe("currency", () => Currency.Year(k, yrs));
                CivMod.Safe("space", () =>
                {
                    Space.Year(k, yrs);
                    Space.Decay(k);
                });
                CivMod.Safe("borders", () => Walls.KingdomYear(k, yrs));
                CivMod.Safe("names", () => RealNames.KingdomYear(k));
                CivMod.Safe("culture-sync", () => CultureSync.Year(k));
                CivMod.Safe("ships", () => Ships.Retire(k, yrs));
            }
            int cb = 6;
            while (cb-- > 0 && _cq.Count > 0)
            {
                City c = _cq.Dequeue();
                if (!G.Ok(c) || c.kingdom == null || !c.kingdom.isCiv()) continue;
                int yrs = Elapsed(c.data);
                if (yrs <= 0) continue;
                CivMod.Safe("economy", () => Economy.Year(c, yrs));
                CivMod.Safe("roles", () => Roles.Year(c, yrs));
                CivMod.Safe("houses", () => Buildings.Year(c, yrs));
                CivMod.Safe("pollution", () => Pollution.Year(c, yrs));
                CivMod.Safe("cold", () => Items.ColdYear(c));
                CivMod.Safe("clothes", () => Clothes.Year(c, yrs));
                CivMod.Safe("xeno", () => Xeno.CityYear(c, yrs));
                CivMod.Safe("catchup", () => Catchup.CityYear(c, yrs));
                CivMod.Safe("walls", () => Walls.CityYear(c, yrs));
                CivMod.Safe("vehicles", () => Vehicles.CityYear(c, yrs));
                CivMod.Safe("carrier-year", () => Carriers.CityYear(c, yrs));
            }
            int lb = 3;
            while (lb-- > 0 && _clq.Count > 0)
            {
                Clan cl = _clq.Dequeue();
                if (cl == null || !cl.isAlive() || cl.data == null) continue;
                int yrs = Elapsed(cl.data);
                if (yrs <= 0) continue;
                CivMod.Safe("clans", () => Clans.Year(cl, yrs));
            }
            _fast += World.world.elapsed;
            if (_fast >= 0.5f)
            {
                float f = _fast;
                _fast = 0f;
                CivMod.Safe("formations", () => Formations.Tick(f));
                CivMod.Safe("waters", () => Waters.Tick(f));
                CivMod.Safe("ship-names", () => ShipNames.Tick(f));
                CivMod.Safe("war-tick", () => War.Tick(f));
                CivMod.Safe("walls-tick", () => Walls.Tick(f));
            }
            Heavy(Time.unscaledDeltaTime);
            _slow += World.world.elapsed;
            if (_slow >= 5f)
            {
                float s = _slow;
                _slow = 0f;
                CivMod.Safe("internet-tick", () => Internet.Tick(s));
                CivMod.Safe("relink", Buildings.Relink);
                CivMod.Safe("radiation", () => War.SlowTick(s));
            }
        }

        private static readonly string[] HeavyNames = { "vehicles-tick", "soldiers-tick", "air-ops", "navy", "carriers", "ammo" };
        private static readonly Action<float>[] HeavyRuns = { Vehicles.Tick, Soldiers.Tick, Aircraft.Tick, Navy.Tick, Carriers.Tick, Ammo.Tick };
        private static readonly float[] HeavyAcc = { 0f, 0.33f, 0.66f, 1f, 1.33f, 1.66f };
        private const float HeavyEvery = 2f;

        private static void Heavy(float dt)
        {
            if (dt > 0.5f) dt = 0.5f;
            int due = -1;
            for (int i = 0; i < HeavyAcc.Length; i++)
            {
                HeavyAcc[i] += dt;
                if (HeavyAcc[i] >= HeavyEvery && (due < 0 || HeavyAcc[i] > HeavyAcc[due])) due = i;
            }
            if (due < 0) return;
            float acc = HeavyAcc[due];
            HeavyAcc[due] = 0f;
            Action<float> run = HeavyRuns[due];
            CivMod.Safe(HeavyNames[due], () => run(acc));
        }

        private static int Elapsed(BaseSystemData d)
        {
            int now = G.Year;
            int last = Store.Int(d, "civ_y", int.MinValue);
            if (last == int.MinValue)
            {
                Store.Put(d, "civ_y", now);
                return 1;
            }
            if (now <= last) return 0;
            Store.Put(d, "civ_y", now);
            return Math.Min(10, now - last);
        }

        private static string CurrentId()
        {
            SaveCustomData d = Store.World();
            if (d == null) return "";
            string id = Store.Str(d, WorldKey);
            if (id.Length == 0)
            {
                id = Guid.NewGuid().ToString("N");
                Store.Put(d, WorldKey, id);
            }
            return id;
        }

        private static void OnWorld()
        {
            Research.ClearCache();
            Bonus.Clear();
            Buildings.Relink();
            Internet.Load();
            Companies.Load();
            Rails.Load();
            War.Load();
            Transit.Clear();
            Mirror.Reset();
            Walls.Clear();
            Catchup.Clear();
            Formations.Clear();
            EraSkins.Clear();
            Waters.Clear();
            Navy.Clear();
            Ammo.Clear();
            Energy.Clear();
            NetPhoto.Clear();
            Store.Reset();
            G.ResetCache();
            Items.FixSprites();
            Globe.Invalidate();
            Rules.Apply();
            Legacy.Migrate();
            EraLock.Force();
        }
    }
}
