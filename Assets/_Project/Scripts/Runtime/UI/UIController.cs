using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Logistics
{
    /// <summary>Binds Game.uxml: HUD, tool bar, main menu, weekly upgrade cards, pause and game-over screens.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class UIController : MonoBehaviour
    {
        public event Action PlayClicked, QuitClicked, ResumeClicked, RestartClicked, MenuClicked;
        public event Action<int> SpeedClicked;            // 0 = pause, 1, 2
        public event Action<Tool> ToolClicked;
        public event Action<int> UpgradeChosen;
        public event Action<float> MusicVolumeChanged, SfxVolumeChanged;
        public event Action ButtonSound;

        UIDocument doc;
        VisualElement root, hud, menu, upgrade, pause, gameOver, cards, weekFill, legend, dayLabels;
        Label weekNumber, scoreLabel, toast, bestLabel, upgradeTitle, upgradeSub, finalScore, finalWeek, finalBest, gameOverSub;
        Button pauseButton, speed1Button, speed2Button, toolRoad, toolBridge, toolRoundabout, toolHighway;
        Label countRoad, countBridge, countRoundabout, countHighway;
        Slider musicSlider, sfxSlider;
        float toastTimer;
        int shownScore = -1, shownWeek = -1, shownDay = -1, legendCount = -1;
        readonly List<Label> dayLabelList = new List<Label>();
        static readonly string[] Days = { "Pzt", "Sal", "Çar", "Per", "Cum", "Cmt", "Paz" };

        void OnEnable()
        {
            doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            hud = root.Q("hud");
            menu = root.Q("menu");
            upgrade = root.Q("upgrade");
            pause = root.Q("pause");
            gameOver = root.Q("gameOver");
            cards = root.Q("cards");
            weekFill = root.Q("weekFill");
            legend = root.Q("cargoLegend");
            dayLabels = root.Q("dayLabels");
            weekNumber = root.Q<Label>("weekNumber");
            scoreLabel = root.Q<Label>("scoreLabel");
            toast = root.Q<Label>("toast");
            bestLabel = root.Q<Label>("bestLabel");
            upgradeTitle = root.Q<Label>("upgradeTitle");
            upgradeSub = root.Q<Label>("upgradeSub");
            finalScore = root.Q<Label>("finalScore");
            finalWeek = root.Q<Label>("finalWeek");
            finalBest = root.Q<Label>("finalBest");
            gameOverSub = root.Q<Label>("gameOverSub");
            countRoad = root.Q<Label>("countRoad");
            countBridge = root.Q<Label>("countBridge");
            countRoundabout = root.Q<Label>("countRoundabout");
            countHighway = root.Q<Label>("countHighway");
            musicSlider = root.Q<Slider>("musicSlider");
            sfxSlider = root.Q<Slider>("sfxSlider");

            Bind("playButton", () => PlayClicked?.Invoke());
            Bind("quitButton", () => QuitClicked?.Invoke());
            Bind("resumeButton", () => ResumeClicked?.Invoke());
            Bind("restartButton", () => RestartClicked?.Invoke());
            Bind("menuButton", () => MenuClicked?.Invoke());
            Bind("againButton", () => RestartClicked?.Invoke());
            Bind("overMenuButton", () => MenuClicked?.Invoke());
            pauseButton = Bind("pauseButton", () => SpeedClicked?.Invoke(0));
            speed1Button = Bind("speed1Button", () => SpeedClicked?.Invoke(1));
            speed2Button = Bind("speed2Button", () => SpeedClicked?.Invoke(2));
            toolRoad = Bind("toolRoad", () => ToolClicked?.Invoke(Tool.Road));
            toolBridge = Bind("toolBridge", () => ToolClicked?.Invoke(Tool.Road));
            toolRoundabout = Bind("toolRoundabout", () => ToolClicked?.Invoke(Tool.Roundabout));
            toolHighway = Bind("toolHighway", () => ToolClicked?.Invoke(Tool.Highway));
            musicSlider?.RegisterValueChangedCallback(e => MusicVolumeChanged?.Invoke(e.newValue));
            sfxSlider?.RegisterValueChangedCallback(e => SfxVolumeChanged?.Invoke(e.newValue));

            dayLabels.Clear();
            dayLabelList.Clear();
            foreach (var d in Days)
            {
                var l = new Label(d);
                l.AddToClassList("day-label");
                dayLabels.Add(l);
                dayLabelList.Add(l);
            }
        }

        Button Bind(string name, Action action)
        {
            var b = root.Q<Button>(name);
            if (b != null) b.clicked += () => { ButtonSound?.Invoke(); action(); };
            return b;
        }

        static void Show(VisualElement e, bool on)
        {
            if (e == null) return;
            if (on) e.RemoveFromClassList("hidden"); else e.AddToClassList("hidden");
        }

        public void SetVolumes(float music, float sfx)
        {
            musicSlider?.SetValueWithoutNotify(music);
            sfxSlider?.SetValueWithoutNotify(sfx);
        }

        public void ShowMenu(int best)
        {
            Show(menu, true); Show(hud, false); Show(upgrade, false); Show(pause, false); Show(gameOver, false);
            bestLabel.text = best > 0 ? $"Rekor: {best} teslimat" : "Depolardan mağazalara yol çiz, şehri besle.";
        }

        public void ShowHud()
        {
            Show(menu, false); Show(hud, true); Show(upgrade, false); Show(pause, false); Show(gameOver, false);
            shownScore = shownWeek = shownDay = legendCount = -1;
        }

        public void ShowPause(bool on) => Show(pause, on);

        public void ShowUpgrade(int week, UpgradeCard[] offer, GameConfig cfg)
        {
            upgradeTitle.text = $"{week}. Hafta başlıyor";
            upgradeSub.text = $"+{cfg.weeklyRoadTiles} yol karosu eklendi. Bir yükseltme seç:";
            cards.Clear();
            for (int i = 0; i < offer.Length; i++)
            {
                int index = i;
                var card = new Button(() => { ButtonSound?.Invoke(); UpgradeChosen?.Invoke(index); });
                card.AddToClassList("card");
                var icon = new Glyph(IconFor(offer[i]));
                icon.AddToClassList("card-icon");
                var title = new Label(UpgradeSystem.Title(offer[i]));
                title.AddToClassList("card-title");
                var desc = new Label(UpgradeSystem.Description(offer[i], cfg));
                desc.AddToClassList("card-desc");
                card.Add(icon); card.Add(title); card.Add(desc);
                cards.Add(card);
            }
            Show(upgrade, true);
        }

        static string IconFor(UpgradeCard c)
        {
            switch (c)
            {
                case UpgradeCard.Bridge: return "bridge";
                case UpgradeCard.Roundabout: return "roundabout";
                case UpgradeCard.Highway: return "highway";
                case UpgradeCard.ExtraTruck: return "truck";
                default: return "road";
            }
        }

        public void HideUpgrade() => Show(upgrade, false);

        public void ShowGameOver(World w, int best)
        {
            Show(gameOver, true);
            finalScore.text = w.Score.ToString();
            finalWeek.text = w.Week.ToString();
            finalBest.text = best.ToString();
            var s = w.FailedStore;
            gameOverSub.text = s != null
                ? $"{CargoInfo.NameOf(s.cargo)} mağazası siparişlere yetişemedi."
                : "Şehrin lojistiği çöktü.";
        }

        public void SetSpeed(int speed)
        {
            pauseButton?.EnableInClassList("selected", speed == 0);
            speed1Button?.EnableInClassList("selected", speed == 1);
            speed2Button?.EnableInClassList("selected", speed == 2);
        }

        public void SetTool(Tool t)
        {
            toolRoad?.EnableInClassList("selected", t == Tool.Road);
            toolRoundabout?.EnableInClassList("selected", t == Tool.Roundabout);
            toolHighway?.EnableInClassList("selected", t == Tool.Highway);
        }

        public void Toast(string message, bool danger = false)
        {
            if (toast == null || string.IsNullOrEmpty(message)) return;
            toast.text = message;
            toast.EnableInClassList("danger", danger);
            toast.AddToClassList("shown");
            toastTimer = 2.4f;
        }

        public void UpdateHud(World w, IReadOnlyList<CargoType> activeCargo)
        {
            if (w == null) return;
            if (w.Score != shownScore) { shownScore = w.Score; scoreLabel.text = w.Score.ToString(); }
            if (w.Week != shownWeek) { shownWeek = w.Week; weekNumber.text = w.Week.ToString(); }
            // Progress fill is data-driven, so it is the one inline style here.
            weekFill.style.width = Length.Percent(w.WeekProgress * 100f);
            int day = Mathf.Min(6, Mathf.FloorToInt(w.WeekProgress * 7f));
            if (day != shownDay)
            {
                shownDay = day;
                for (int i = 0; i < dayLabelList.Count; i++) dayLabelList[i].EnableInClassList("today", i == day);
            }
            countRoad.text = w.Inventory.Roads.ToString();
            countBridge.text = w.Inventory.Bridges.ToString();
            countRoundabout.text = w.Inventory.Roundabouts.ToString();
            countHighway.text = w.Inventory.Highways.ToString();
            toolRoad.EnableInClassList("empty", w.Inventory.Roads == 0);
            toolBridge.EnableInClassList("empty", w.Inventory.Bridges == 0);
            toolRoundabout.EnableInClassList("empty", w.Inventory.Roundabouts == 0);
            toolHighway.EnableInClassList("empty", w.Inventory.Highways == 0);

            if (activeCargo != null && activeCargo.Count != legendCount)
            {
                legendCount = activeCargo.Count;
                legend.Clear();
                foreach (var c in activeCargo)
                {
                    var chip = new VisualElement();
                    chip.AddToClassList("cargo-chip");
                    var sw = new VisualElement();
                    sw.AddToClassList("cargo-swatch");
                    sw.style.backgroundColor = CargoInfo.ColorOf(c); // data-driven cargo color
                    var name = new Label(CargoInfo.NameOf(c));
                    name.AddToClassList("cargo-name");
                    chip.Add(sw); chip.Add(name);
                    legend.Add(chip);
                }
            }
        }

        void Update()
        {
            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f) toast?.RemoveFromClassList("shown");
            }
        }

        /// <summary>True when the pointer is over a pickable UI element (so world clicks should be ignored).</summary>
        public bool IsPointerOverUI(Vector2 screenPos)
        {
            if (root == null || root.panel == null) return false;
            var panelPos = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(screenPos.x, Screen.height - screenPos.y));
            var picked = root.panel.Pick(panelPos);
            if (picked == null || picked == root) return false;
            for (var e = picked; e != null; e = e.parent) if (e == root) return true;
            return false;
        }
    }
}
