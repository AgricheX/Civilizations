using System;
using System.Collections.Generic;
using System.Linq;

namespace Civilizations
{
    internal static partial class Internet
    {
        public const string KBrag = "civ_brag_heat";
        private const string KFounded = "civ_city_founded";

        private static void Geotag(NetPost p, WorldTile t, Kingdom k)
        {
            var parts = new List<string>();
            if (t != null && t.Type != null && t.Type.biome_asset != null)
            {
                string key = t.Type.biome_asset.getLocaleID();
                if (LocalizedTextManager.stringExists(key)) parts.Add(LocalizedTextManager.getText(key));
            }
            if (k != null)
            {
                foreach (Kingdom o in Groups.Enemies(k))
                {
                    parts.Add(Txt.F("net_geo_war", G.Name(o)));
                    break;
                }
            }
            p.Geo = string.Join(" · ", parts.ToArray());
        }

        private static void Reception(Actor a, NetPost p)
        {
            if (!G.Ok(a)) return;
            bool psycho = a.hasTrait("psychopath");
            if (p.Likes > 20 && p.Likes > p.Dislikes * 4)
            {
                if (G.Chance(0.5f)) Moods.Apply(a, "civ_liked");
                Store.Put(a.data, KFollow, Followers(a) + Math.Max(1, p.Likes / 20));
            }
            else if (p.Dislikes > p.Likes && !psycho)
            {
                if (G.Chance(0.6f)) Moods.Apply(a, "civ_disliked");
            }
            if (p.Kind == "brag" || p.Kind == "rich")
            {
                Kingdom k = a.kingdom;
                if (k != null) Store.Put(k.data, KBrag, Store.Flt(k.data, KBrag, 0f) + 1f + Followers(a) / 400f);
            }
        }

        private static bool RelatedComment(NetPost p)
        {
            if (p.Who != "person" || p.Med != "social" || !G.Chance(0.35f)) return false;
            Actor author = G.ActorById(p.AuthorId);
            if (!G.Ok(author)) return false;
            var options = new List<KeyValuePair<Actor, string>>();
            if (author.hasLover() && G.Ok(author.lover)) options.Add(new KeyValuePair<Actor, string>(author.lover, "net_c_lover"));
            foreach (Actor par in author.getParents()) if (G.Ok(par)) options.Add(new KeyValuePair<Actor, string>(par, "net_c_parent"));
            foreach (Actor ch in author.getChildren()) if (G.Ok(ch) && ch.isAdult()) options.Add(new KeyValuePair<Actor, string>(ch, "net_c_child"));
            foreach (Actor par in author.getParents())
            {
                if (!G.Ok(par)) continue;
                foreach (Actor sib in par.getChildren()) if (G.Ok(sib) && sib != author && sib.isAdult()) options.Add(new KeyValuePair<Actor, string>(sib, "net_c_sibling"));
                break;
            }
            Actor friend = author.getBestFriend();
            if (G.Ok(friend)) options.Add(new KeyValuePair<Actor, string>(friend, "net_c_friend"));
            if (options.Count == 0) return false;
            var pick = options[UnityEngine.Random.Range(0, options.Count)];
            Actor c = pick.Key;
            p.Comments.Add(new NetComment { Author = c.getName(), Handle = Names.Handle(c), AuthorId = G.Id(c), Text = Line(pick.Value, p.Author), Likes = UnityEngine.Random.Range(0, Math.Max(2, p.Likes / 8)) });
            if (p.Comments.Count > 12) p.Comments.RemoveAt(0);
            return true;
        }

        public static void Candle(NetPost p)
        {
            if (p == null) return;
            p.Candles++;
            Version++;
        }

        private static void Life(List<Kingdom> online)
        {
            foreach (Kingdom k in online)
            {
                Birthdays(k);
                Anniversaries(k);
                if (G.Chance(0.3f)) Throwback(k);
                if (G.Chance(0.35f)) Preach(k);
                ReputationEffects(k, online);
                Store.Put(k.data, KBrag, Store.Flt(k.data, KBrag, 0f) * 0.6f);
            }
        }

        private static void Birthdays(Kingdom k)
        {
            int done = 0;
            foreach (City c in G.Cities(k))
            {
                foreach (Actor a in c.units)
                {
                    if (done >= 2) return;
                    if (!G.Ok(a) || !a.isAdult() || !a.isSapient()) continue;
                    int age = a.getAge();
                    if (age <= 0 || age % 10 != 0) continue;
                    if (!(a.isKing() || a.isCityLeader() || Followers(a) > 100) && !G.Chance(0.02f)) continue;
                    NetPost p = Person(a, "birthday", Line("net_birthday", age), 1.5f);
                    if (p == null) continue;
                    p.Icon = "ui/Icons/civ/app/calendar";
                    foreach (Actor par in a.getParents()) if (G.Ok(par)) p.Comments.Add(new NetComment { Author = par.getName(), Handle = Names.Handle(par), AuthorId = G.Id(par), Text = Line("net_c_bday_family", a.getName()), Likes = 1 });
                    if (a.hasLover() && G.Ok(a.lover)) p.Comments.Add(new NetComment { Author = a.lover.getName(), Handle = Names.Handle(a.lover), AuthorId = G.Id(a.lover), Text = Line("net_c_bday_lover", a.getName()), Likes = 2 });
                    done++;
                }
            }
        }

        private static void Anniversaries(Kingdom k)
        {
            foreach (City c in G.Cities(k))
            {
                int founded = Store.Int(c.data, KFounded, int.MinValue);
                if (founded == int.MinValue)
                {
                    Store.Put(c.data, KFounded, G.Year);
                    continue;
                }
                int age = G.Year - founded;
                if (age <= 0 || age % 25 != 0) continue;
                NetPost p = Official(k, "city_anniv", Line("net_city_anniv", G.Name(c), age), true);
                if (p == null) continue;
                p.Icon = "ui/Icons/civ/app/calendar";
                WorldTile t = G.Tile(c);
                if (t != null) p.Snap = t.x + "," + t.y;
            }
        }

        private static void Throwback(Kingdom k)
        {
            string kid = G.Id(k);
            NetPost old = _feed.Where(x => x.Who == "person" && x.Kingdom == kid && x.Year <= G.Year - 10 && x.Snap.Length > 0).OrderBy(x => UnityEngine.Random.value).FirstOrDefault();
            if (old == null) return;
            Actor a = G.ActorById(old.AuthorId);
            if (!G.Ok(a)) return;
            NetPost p = Person(a, "throwback", Line("net_throwback", old.Year), 1.2f);
            if (p == null) return;
            p.Snap = old.Snap;
            p.Icon = "ui/Icons/civ/app/calendar";
        }

        private static void Preach(Kingdom k)
        {
            Religion r = k.getReligion();
            if (r == null || r.data == null) return;
            NetPost p = Make("religion", r.data.id.ToString(), r.data.name, "@" + Root(r.data.name).ToLowerInvariant(), k, "preach", Line("net_preach", r.data.name));
            p.Icon = "ui/Icons/civ/net/memorial";
            _feed.Add(p);
            React(p, k, 1.5f);
            Version++;
        }

        private static void ReputationEffects(Kingdom k, List<Kingdom> online)
        {
            float rep = Reputation(k);
            City cap = k.capital;
            if (cap != null && cap.isAlive())
            {
                if (rep > 0.3f) Economy.Give(cap, "gold", (int)(rep * 20f));
                else if (rep < -0.3f) Economy.TakeUpTo(cap, "gold", (int)(-rep * 15f));
            }
            if (rep > 0.35f && G.Chance(0.5f)) Migrate(k, online);
        }

        private static void Migrate(Kingdom k, List<Kingdom> online)
        {
            Kingdom from = online.Where(o => o != k && Reputation(o) < 0f).OrderBy(o => Reputation(o)).FirstOrDefault();
            if (from == null) return;
            List<City> targets = G.Cities(k);
            if (targets.Count == 0) return;
            int moved = 0;
            foreach (City c in G.Cities(from))
            {
                foreach (Actor a in c.units.ToList())
                {
                    if (moved >= 2) break;
                    if (!G.Ok(a) || !a.isAdult() || a.isKing() || a.isCityLeader() || Vehicles.IsVehicle(a) || a.current_tile == null) continue;
                    City dest = targets.FirstOrDefault(t => G.Tile(t) != null && G.Tile(t).isSameIsland(a.current_tile));
                    if (dest == null) continue;
                    a.joinCity(dest);
                    a.goTo(G.Tile(dest));
                    moved++;
                }
                if (moved >= 2) break;
            }
            if (moved > 0) Official(k, "migrants", Line("net_migrants", moved, G.Name(from)), false);
        }
    }
}
