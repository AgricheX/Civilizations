using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal static class Paths
    {
        private static readonly int[] Dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] Dy = { 0, 0, 1, -1, 1, -1, 1, -1 };

        private struct Entry
        {
            public float Priority;
            public int Key;
        }

        private class OpenHeap
        {
            private readonly List<Entry> _items = new List<Entry>(128);
            public int Count { get { return _items.Count; } }

            private static int Compare(Entry a, Entry b)
            {
                int order = a.Priority.CompareTo(b.Priority);
                return order != 0 ? order : a.Key.CompareTo(b.Key);
            }

            public void Add(float priority, int key)
            {
                var entry = new Entry { Priority = priority, Key = key };
                int i = _items.Count;
                _items.Add(entry);
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (Compare(_items[parent], entry) <= 0) break;
                    _items[i] = _items[parent];
                    i = parent;
                }
                _items[i] = entry;
            }

            public int RemoveMin()
            {
                int key = _items[0].Key;
                int last = _items.Count - 1;
                Entry entry = _items[last];
                _items.RemoveAt(last);
                if (last == 0) return key;
                int i = 0;
                while (true)
                {
                    int child = i * 2 + 1;
                    if (child >= last) break;
                    if (child + 1 < last && Compare(_items[child + 1], _items[child]) < 0) child++;
                    if (Compare(entry, _items[child]) <= 0) break;
                    _items[i] = _items[child];
                    i = child;
                }
                _items[i] = entry;
                return key;
            }
        }

        private class Node
        {
            public int X;
            public int Y;
            public float G;
            public float F;
            public Node Parent;
        }

        public static List<WorldTile> Find(WorldTile from, WorldTile to, Func<WorldTile, float> cost, int maxNodes = 12000)
        {
            var result = new List<WorldTile>();
            if (from == null || to == null || World.world == null) return result;
            int w = MapBox.width;
            var open = new OpenHeap();
            var nodes = new Dictionary<int, Node>();
            var closed = new HashSet<int>();
            int startKey = from.y * w + from.x;
            var start = new Node { X = from.x, Y = from.y, G = 0f, F = Heur(from.x, from.y, to.x, to.y) };
            nodes[startKey] = start;
            open.Add(start.F, startKey);
            int expanded = 0;
            while (open.Count > 0 && expanded < maxNodes)
            {
                int key = open.RemoveMin();
                if (!closed.Add(key)) continue;
                Node n = nodes[key];
                expanded++;
                if (n.X == to.x && n.Y == to.y)
                {
                    var stack = new List<WorldTile>();
                    for (Node p = n; p != null; p = p.Parent) stack.Add(Globe.Tile(p.X, p.Y));
                    stack.Reverse();
                    return stack;
                }
                for (int i = 0; i < 8; i++)
                {
                    WorldTile t = Globe.Tile(n.X + Dx[i], n.Y + Dy[i]);
                    if (t == null) continue;
                    int nk = t.y * w + t.x;
                    if (closed.Contains(nk)) continue;
                    float c = cost(t);
                    if (c < 0f) continue;
                    float step = (i < 4 ? 1f : 1.41f) * c;
                    float g = n.G + step;
                    Node m;
                    if (nodes.TryGetValue(nk, out m) && m.G <= g) continue;
                    if (m == null)
                    {
                        m = new Node { X = t.x, Y = t.y };
                        nodes[nk] = m;
                    }
                    m.G = g;
                    m.Parent = n;
                    m.F = g + Heur(t.x, t.y, to.x, to.y);
                    open.Add(m.F + nk * 1e-7f, nk);
                }
            }
            return result;
        }

        private static float Heur(int ax, int ay, int bx, int by)
        {
            return (float)Math.Sqrt(Globe.SqDist(ax, ay, bx, by));
        }
    }
}
