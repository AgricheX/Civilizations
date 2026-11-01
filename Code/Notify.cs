using System;
using UnityEngine;

namespace Civilizations
{
    internal static class Notify
    {
        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                WorldTip.showNow(text, false, "top", 5f);
            }
            catch (Exception)
            {
                Debug.Log("[Civilizations] " + text);
            }
        }
    }
}
