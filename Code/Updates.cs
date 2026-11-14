using System;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Civilizations
{
    internal static class Updates
    {
        public const string NexusUrl = "https://agrichex.github.io/Nexus/mods/civilizations/";
        public const string NexusHome = "https://agrichex.github.io/Nexus/";
        public const string DiscordUrl = "https://discord.gg/s3EbgNSsJW";
        private const string RemoteUrl = "https://raw.githubusercontent.com/AgricheX/Civilizations/main/mod.json";
        private static bool _started;
        private static bool _shown;
        private static volatile string _remote;
        private static float _wait = 4f;

        public static string Local
        {
            get
            {
                try
                {
                    return CivMod.Instance.GetDeclaration().Version ?? "0";
                }
                catch (Exception)
                {
                    return "0";
                }
            }
        }

        public static string Remote
        {
            get { return _remote; }
        }

        private static string IgnoreFile
        {
            get { return Path.Combine(Universe.Folder, "update_ignore.txt"); }
        }

        public static void Tick()
        {
            if (CivMod.C != null && !CivMod.C.CheckUpdates) return;
            if (!_started)
            {
                _started = true;
                Thread th = new Thread(Fetch);
                th.IsBackground = true;
                th.Start();
                return;
            }
            if (_shown || _remote == null || !Tab.Ready) return;
            _wait -= Time.unscaledDeltaTime;
            if (_wait > 0f) return;
            _shown = true;
            if (Compare(_remote, Local) <= 0) return;
            if (Ignored() == _remote) return;
            WinUpdate.Show();
        }

        private static void Fetch()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Civilizations-Mod");
                    string json = wc.DownloadString(RemoteUrl + "?t=" + DateTime.UtcNow.Ticks);
                    Match m = Regex.Match(json, "\"version\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase);
                    if (m.Success) _remote = m.Groups[1].Value.Trim();
                }
            }
            catch (Exception)
            {
            }
        }

        public static int Compare(string a, string b)
        {
            string[] pa = (a ?? "").Split('.');
            string[] pb = (b ?? "").Split('.');
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length ? Num(pa[i]) : 0;
                int y = i < pb.Length ? Num(pb[i]) : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }

        private static int Num(string s)
        {
            Match m = Regex.Match(s ?? "", "\\d+");
            int v;
            return m.Success && int.TryParse(m.Value, out v) ? v : 0;
        }

        private static string Ignored()
        {
            try
            {
                return File.Exists(IgnoreFile) ? File.ReadAllText(IgnoreFile).Trim() : "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        public static void Ignore()
        {
            try
            {
                Directory.CreateDirectory(Universe.Folder);
                File.WriteAllText(IgnoreFile, _remote ?? "");
            }
            catch (Exception)
            {
            }
        }
    }
}
