using NeoModLoader.api;
using UnityEngine;

namespace Civilizations
{
    internal class WinSaga : AbstractWindow<WinSaga>
    {
        public const string Id = "civ_saga";
        private static SagaCase _cur;
        private static bool _answered;

        public static void Create()
        {
            CreateAndInit(Id);
        }

        public static void Show()
        {
            if (_cur != null) return;
            Next();
        }

        private static void Next()
        {
            _cur = null;
            while (Sagas.Pending.Count > 0)
            {
                SagaCase c = Sagas.Pending.Dequeue();
                if (G.Ok(c.K))
                {
                    _cur = c;
                    break;
                }
            }
            if (_cur == null) return;
            _answered = false;
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

        public override void OnNormalDisable()
        {
            if (_cur != null && !_answered) Answer(_cur.Options[0]);
        }

        private static void Answer(int option)
        {
            SagaCase c = _cur;
            if (c == null) return;
            _answered = true;
            _cur = null;
            CivMod.Safe("saga-answer", () => Sagas.Resolve(c, option, true));
            if (c.Paused) Config.paused = false;
        }

        private static SagaCase Take()
        {
            while (Sagas.Pending.Count > 0)
            {
                SagaCase c = Sagas.Pending.Dequeue();
                if (G.Ok(c.K)) return c;
            }
            return null;
        }

        private void Pick(int option)
        {
            Answer(option);
            _cur = Take();
            if (_cur != null)
            {
                _answered = false;
                Render();
                return;
            }
            ScrollWindow sw = GetComponent<ScrollWindow>();
            if (sw != null) sw.clickHide();
        }

        private void Render()
        {
            Transform t = ContentTransform;
            UiKit.Clear(t);
            SagaCase c = _cur;
            if (c == null) return;
            UiKit.Header(t, Sagas.Title(c), c.Def.Good ? UiKit.Good : UiKit.Gold, "ui/Icons/civ/sb/saga");
            RectTransform head = UiKit.Box(t, "Who", 20f);
            UiKit.Banner(head, c.K, new Vector2(-UiKit.W / 2f + 12f, 0f), 16f);
            UiKit.Text(head, G.Name(c.K) + " · " + Db.EraName(Research.Era(c.K)), new Vector2(10f, 0f), new Vector2(UiKit.W - 40f, 16f), 8, TextAnchor.MiddleLeft, UiKit.Soft);
            UiKit.Paragraph(t, Sagas.Body(c), 9, Color.white);
            UiKit.Gap(t, 6f);
            foreach (int o in c.Options)
            {
                int opt = o;
                RectTransform row = UiKit.Box(t, "Opt", 24f);
                UiKit.Btn(row, Sagas.Option(c, o), null, () => Pick(opt), new Vector2(UiKit.W - 16f, 22f), Vector2.zero, null, null, new Color(1f, 0.85f, 0.45f));
            }
        }
    }
}
