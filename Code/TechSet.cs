using System;
using System.Collections.Generic;

namespace Civilizations
{
    internal class TechSet
    {
        private ulong[] _bits;
        public int Count { get; private set; }

        public TechSet()
        {
            _bits = new ulong[(Db.Techs.Count + 63) / 64 + 1];
        }

        public TechSet(TechSet other) : this()
        {
            if (other == null) return;
            Array.Copy(other._bits, _bits, Math.Min(_bits.Length, other._bits.Length));
            Count = other.Count;
        }

        public bool Has(int i)
        {
            if (i < 0) return false;
            int w = i >> 6;
            return w < _bits.Length && (_bits[w] & (1UL << (i & 63))) != 0;
        }

        public bool Has(TechDef t)
        {
            return t != null && Has(t.Index);
        }

        public bool Add(int i)
        {
            if (i < 0 || Has(i)) return false;
            _bits[i >> 6] |= 1UL << (i & 63);
            Count++;
            return true;
        }

        public bool Remove(int i)
        {
            if (!Has(i)) return false;
            _bits[i >> 6] &= ~(1UL << (i & 63));
            Count--;
            return true;
        }

        public IEnumerable<TechDef> All()
        {
            for (int w = 0; w < _bits.Length && (w << 6) < Db.Techs.Count; w++)
            {
                int next = 0;
                while (next < 64)
                {
                    ulong bits = _bits[w] & (ulong.MaxValue << next);
                    if (bits == 0) break;
                    int bit = FirstBit(bits);
                    int i = (w << 6) + bit;
                    if (i >= Db.Techs.Count) yield break;
                    yield return Db.Techs[i];
                    next = bit + 1;
                }
            }
        }

        private static int FirstBit(ulong bits)
        {
            int n = 0;
            if ((bits & 0xFFFFFFFFUL) == 0) { n += 32; bits >>= 32; }
            if ((bits & 0xFFFFUL) == 0) { n += 16; bits >>= 16; }
            if ((bits & 0xFFUL) == 0) { n += 8; bits >>= 8; }
            if ((bits & 0xFUL) == 0) { n += 4; bits >>= 4; }
            if ((bits & 0x3UL) == 0) { n += 2; bits >>= 2; }
            if ((bits & 0x1UL) == 0) n++;
            return n;
        }

        public string Save()
        {
            int last = _bits.Length - 1;
            while (last >= 0 && _bits[last] == 0) last--;
            if (last < 0) return "";
            int used = 8;
            while ((_bits[last] >> ((used - 1) * 8)) == 0) used--;
            var bytes = new byte[last * 8 + used];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(_bits[i >> 3] >> ((i & 7) * 8));
            }
            return Convert.ToBase64String(bytes);
        }

        public static TechSet Load(string s)
        {
            var set = new TechSet();
            if (string.IsNullOrEmpty(s)) return set;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(s);
            }
            catch (Exception)
            {
                return set;
            }
            int count = Db.Techs.Count;
            int length = Math.Min(bytes.Length, (count + 7) / 8);
            for (int i = 0; i < length; i++)
            {
                set._bits[i >> 3] |= (ulong)bytes[i] << ((i & 7) * 8);
            }
            int tail = count & 63;
            if (tail != 0) set._bits[count >> 6] &= (1UL << tail) - 1;
            int n = 0;
            foreach (ulong word in set._bits)
            {
                ulong bits = word;
                while (bits != 0)
                {
                    bits &= bits - 1;
                    n++;
                }
            }
            set.Count = n;
            return set;
        }
    }
}
