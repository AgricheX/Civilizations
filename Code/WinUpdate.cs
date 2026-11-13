using NeoModLoader.api;
using UnityEngine;
using UnityEngine.UI;

namespace Civilizations
{
    internal class WinUpdate : AbstractWindow<WinUpdate>
    {
        public const string Id = "civ_update";

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Show()
        {
            UiKit.Open(Id);
        }

        protected override void Init()
        {
            UiKit.Setup(ContentTransform);
        }

        public override void OnNormalEnable()
        {
            Render();
        }

        private void Close()
        {
            ScrollWindow sw = GetComponent<ScrollWindow>();
            if (sw != null) sw.clickHide();
        }

        private void Render()
        {
            Transform t = ContentTransform;
            UiKit.Clear(t);
            UiKit.Header(t, Txt.T("civ_update_title"), UiKit.Gold, "ui/Icons/civ/btn/nexus");
            UiKit.Gap(t, 4f);
            RectTransform logo = UiKit.Box(t, "Logo", 52f);
            UiKit.Img(logo, UiKit.Icon("ui/Icons/civ/tab"), new Vector2(48f, 48f), Vector2.zero);
            UiKit.Img(logo, UiKit.Icon("ui/Icons/civ/btn/nexus"), new Vector2(22f, 22f), new Vector2(-70f, 0f), new Color(1f, 1f, 1f, 0.8f));
            UiKit.Img(logo, UiKit.Icon("ui/Icons/civ/btn/discord"), new Vector2(22f, 22f), new Vector2(70f, 0f), new Color(1f, 1f, 1f, 0.8f));
            UiKit.Paragraph(t, Txt.T("civ_update_body"), 9, Color.white);
            UiKit.Gap(t, 4f);
            RectTransform ver = UiKit.Box(t, "Versions", 34f);
            UiKit.Panel(ver, 0.6f);
            UiKit.Text(ver, Txt.T("civ_update_installed"), new Vector2(-UiKit.W / 4f, 7f), new Vector2(UiKit.W / 2f - 10f, 12f), 8, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Text(ver, "v" + Updates.Local, new Vector2(-UiKit.W / 4f, -6f), new Vector2(UiKit.W / 2f - 10f, 14f), 11, TextAnchor.MiddleCenter, UiKit.Soft);
            UiKit.Text(ver, Txt.T("civ_update_new"), new Vector2(UiKit.W / 4f, 7f), new Vector2(UiKit.W / 2f - 10f, 12f), 8, TextAnchor.MiddleCenter, UiKit.Dim);
            UiKit.Text(ver, "v" + (Updates.Remote ?? "?"), new Vector2(UiKit.W / 4f, -6f), new Vector2(UiKit.W / 2f - 10f, 14f), 11, TextAnchor.MiddleCenter, UiKit.Good);
            UiKit.Text(ver, "→", Vector2.zero, new Vector2(20f, 20f), 12, TextAnchor.MiddleCenter, UiKit.Gold);
            UiKit.Gap(t, 6f);
            RectTransform a = UiKit.Box(t, "Get", 26f);
            UiKit.Btn(a, Txt.T("civ_update_get"), UiKit.Icon("ui/Icons/civ/btn/nexus"), () => { Application.OpenURL(Updates.NexusUrl); Close(); }, new Vector2(UiKit.W - 16f, 24f), Vector2.zero, null, null, new Color(0.55f, 0.95f, 0.5f));
            RectTransform b = UiKit.Box(t, "Discord", 24f);
            UiKit.Btn(b, Txt.T("civ_update_discord"), UiKit.Icon("ui/Icons/civ/btn/discord"), () => Application.OpenURL(Updates.DiscordUrl), new Vector2(UiKit.W - 16f, 22f), Vector2.zero, null, null, new Color(0.62f, 0.66f, 1f));
            RectTransform c = UiKit.Box(t, "Later", 24f);
            float hw = (UiKit.W - 20f) / 2f;
            UiKit.Btn(c, Txt.T("civ_update_later"), null, Close, new Vector2(hw, 22f), new Vector2(-hw / 2f - 2f, 0f));
            UiKit.Btn(c, Txt.T("civ_update_ignore"), null, () => { Updates.Ignore(); Close(); }, new Vector2(hw, 22f), new Vector2(hw / 2f + 2f, 0f), null, null, new Color(0.7f, 0.7f, 0.75f));
        }
    }
}
