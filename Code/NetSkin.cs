using UnityEngine;

namespace Civilizations
{
    internal class NetSkin
    {
        public string Med = "social";
        public Color Bezel = new Color(0.07f, 0.07f, 0.09f);
        public Color Page = new Color(0.9f, 0.91f, 0.95f);
        public Color Card = new Color(0.97f, 0.97f, 0.99f);
        public Color Head = new Color(0.97f, 0.97f, 0.99f);
        public Color Ink = new Color(0.13f, 0.15f, 0.2f);
        public Color InkSoft = new Color(0.35f, 0.38f, 0.46f);
        public Color Accent = new Color(0.2f, 0.45f, 0.85f);
        public Color Pill = new Color(0.8f, 0.82f, 0.88f);
        public Color Line = new Color(0.85f, 0.87f, 0.92f);
        public Color Official = new Color(0.65f, 0.45f, 0.05f);
        public Color Hot = new Color(1f, 0.95f, 0.82f);
        public bool Round = true;
        public bool Social = true;

        public string Like { get { return Txt.T("net_skin_like_" + Med); } }
        public string Talk { get { return Txt.T("net_skin_talk_" + Med); } }
        public string Device { get { return Txt.T("net_skin_device_" + Med); } }

        public static NetSkin For(string med)
        {
            var s = new NetSkin { Med = med };
            switch (med)
            {
                case "crier":
                    s.Social = false;
                    s.Round = false;
                    s.Bezel = new Color(0.4f, 0.36f, 0.31f);
                    s.Page = new Color(0.58f, 0.51f, 0.42f);
                    s.Card = new Color(0.76f, 0.69f, 0.57f);
                    s.Head = new Color(0.66f, 0.59f, 0.48f);
                    s.Ink = new Color(0.22f, 0.15f, 0.1f);
                    s.InkSoft = new Color(0.38f, 0.3f, 0.22f);
                    s.Accent = new Color(0.55f, 0.24f, 0.1f);
                    s.Pill = new Color(0.64f, 0.56f, 0.45f);
                    s.Line = new Color(0.5f, 0.43f, 0.35f);
                    s.Official = new Color(0.5f, 0.2f, 0.08f);
                    s.Hot = new Color(0.82f, 0.72f, 0.5f);
                    break;
                case "scroll":
                    s.Social = false;
                    s.Round = false;
                    s.Bezel = new Color(0.42f, 0.26f, 0.13f);
                    s.Page = new Color(0.82f, 0.72f, 0.5f);
                    s.Card = new Color(0.94f, 0.87f, 0.68f);
                    s.Head = new Color(0.88f, 0.79f, 0.58f);
                    s.Ink = new Color(0.28f, 0.17f, 0.1f);
                    s.InkSoft = new Color(0.5f, 0.36f, 0.22f);
                    s.Accent = new Color(0.62f, 0.12f, 0.1f);
                    s.Pill = new Color(0.84f, 0.74f, 0.54f);
                    s.Line = new Color(0.7f, 0.58f, 0.38f);
                    s.Official = new Color(0.62f, 0.12f, 0.1f);
                    s.Hot = new Color(0.98f, 0.9f, 0.62f);
                    break;
                case "gazette":
                    s.Social = false;
                    s.Round = false;
                    s.Bezel = new Color(0.3f, 0.2f, 0.12f);
                    s.Page = new Color(0.86f, 0.81f, 0.68f);
                    s.Card = new Color(0.96f, 0.92f, 0.81f);
                    s.Head = new Color(0.93f, 0.88f, 0.75f);
                    s.Ink = new Color(0.2f, 0.14f, 0.1f);
                    s.InkSoft = new Color(0.45f, 0.36f, 0.28f);
                    s.Accent = new Color(0.52f, 0.18f, 0.12f);
                    s.Pill = new Color(0.86f, 0.8f, 0.66f);
                    s.Line = new Color(0.4f, 0.32f, 0.24f);
                    s.Official = new Color(0.52f, 0.18f, 0.12f);
                    s.Hot = new Color(1f, 0.93f, 0.74f);
                    break;
                case "paper":
                    s.Social = false;
                    s.Round = false;
                    s.Bezel = new Color(0.22f, 0.2f, 0.19f);
                    s.Page = new Color(0.8f, 0.79f, 0.76f);
                    s.Card = new Color(0.94f, 0.93f, 0.9f);
                    s.Head = new Color(0.9f, 0.89f, 0.86f);
                    s.Ink = new Color(0.08f, 0.08f, 0.08f);
                    s.InkSoft = new Color(0.36f, 0.36f, 0.35f);
                    s.Accent = new Color(0.45f, 0.08f, 0.08f);
                    s.Pill = new Color(0.82f, 0.81f, 0.78f);
                    s.Line = new Color(0.2f, 0.2f, 0.2f);
                    s.Official = new Color(0.1f, 0.1f, 0.1f);
                    s.Hot = new Color(0.98f, 0.95f, 0.78f);
                    break;
                case "broadcast":
                    s.Social = false;
                    s.Round = true;
                    s.Bezel = new Color(0.36f, 0.23f, 0.14f);
                    s.Page = new Color(0.1f, 0.12f, 0.17f);
                    s.Card = new Color(0.17f, 0.2f, 0.28f);
                    s.Head = new Color(0.12f, 0.14f, 0.2f);
                    s.Ink = new Color(0.94f, 0.95f, 0.97f);
                    s.InkSoft = new Color(0.66f, 0.71f, 0.8f);
                    s.Accent = new Color(1f, 0.78f, 0.3f);
                    s.Pill = new Color(0.27f, 0.31f, 0.42f);
                    s.Line = new Color(0.25f, 0.29f, 0.38f);
                    s.Official = new Color(1f, 0.78f, 0.3f);
                    s.Hot = new Color(0.32f, 0.27f, 0.2f);
                    break;
            }
            return s;
        }
    }
}
