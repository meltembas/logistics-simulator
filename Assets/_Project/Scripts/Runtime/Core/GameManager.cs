using UnityEngine;
using UnityEngine.InputSystem;

namespace Logistics
{
    public enum GameState { Menu, Playing, Paused, Upgrade, GameOver }

    /// <summary>Owns the <see cref="World"/>, its views, and the menu → play → upgrade → game-over flow.</summary>
    public class GameManager : MonoBehaviour
    {
        public GameConfig config;
        public MaterialLibrary materials;
        public Camera mainCamera;
        public CityCameraController cameraRig;
        public ReflectionProbe reflectionProbe;
        public UIController ui;
        public AudioManager audioManager;
        public ToolController tools;
        [Tooltip("0 = random seed each run")]
        public int fixedSeed;

        public World World { get; private set; }
        public GameState State { get; private set; } = GameState.Menu;
        public int Speed { get; private set; } = 1;
        public static GameManager Instance { get; private set; }

        TerrainView terrain;
        RoadView roads;
        DecorationView deco;
        CityView city;
        UpgradeCard[] offer;
        bool begun;
        int lastWarningFrame;
        const string BestKey = "best.score";

        void Awake()
        {
            Instance = this;
            if (config == null) config = GameConfig.CreateDefault();
            if (mainCamera == null) mainCamera = Camera.main;
            terrain = NewView<TerrainView>("Terrain");
            roads = NewView<RoadView>("Roads");
            deco = NewView<DecorationView>("Decoration");
            city = NewView<CityView>("City");
            if (tools == null) tools = NewView<ToolController>("Tools");
            tools.game = this;
            tools.Init(materials);
        }

        T NewView<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }

        void Start()
        {
            if (ui != null)
            {
                ui.PlayClicked += StartGame;
                ui.QuitClicked += Quit;
                ui.ResumeClicked += () => SetPaused(false);
                ui.RestartClicked += Restart;
                ui.MenuClicked += () => { NewWorld(); EnterMenu(); };
                ui.SpeedClicked += s => { if (s == 0) SetPaused(true, showMenu: false); else { SetSpeed(s); SetPaused(false); } };
                ui.ToolClicked += t => tools.SetTool(t);
                ui.UpgradeChosen += ChooseUpgrade;
                ui.ButtonSound += () => audioManager?.Play(Sfx.Click, 0.7f, 0.02f);
                if (audioManager != null)
                {
                    ui.MusicVolumeChanged += audioManager.SetMusicVolume;
                    ui.SfxVolumeChanged += audioManager.SetSfxVolume;
                    ui.SetVolumes(audioManager.MusicVolume, audioManager.SfxVolume);
                }
            }
            tools.ToolChanged += t => ui?.SetTool(t);
            tools.Message += (m, danger) => { ui?.Toast(m, danger); if (danger) audioManager?.Play(Sfx.Deny, 0.6f); };
            tools.Built += placed => audioManager?.Play(placed ? Sfx.Place : Sfx.Erase, 0.55f, 0.1f);

            NewWorld();
            EnterMenu();
        }

        int NextSeed() => fixedSeed != 0 ? fixedSeed : Random.Range(1, int.MaxValue);

        public void NewWorld(int seed = 0)
        {
            if (seed == 0) seed = NextSeed();
            if (World != null) Unsubscribe(World);
            World = new World(config, seed);
            begun = false;
            terrain.Build(World, materials);
            roads.Build(World, materials);
            deco.Build(World, materials);
            city.Bind(World, materials, mainCamera);
            Subscribe(World);
            UpdateCameraLimits();
            var center = World.Grid.ActiveWorldBounds.center;
            cameraRig?.JumpTo(center, 170f);
            if (reflectionProbe != null)
            {
                var size = World.Grid.WorldSize + new Vector3(TerrainView.Margin * 2f, 60f, TerrainView.Margin * 2f);
                reflectionProbe.size = size;
                reflectionProbe.transform.position = World.Grid.WorldCenter + Vector3.up * 25f;
                reflectionProbe.RenderProbe();
            }
            DynamicGI.UpdateEnvironment();
        }

        void Subscribe(World w)
        {
            w.WeekEnded += OnWeekEnded;
            w.GameOver += OnGameOver;
            w.Delivered += OnDelivered;
            w.OverflowWarning += OnOverflowWarning;
            w.StoreAdded += OnBuildingAdded;
            w.DepotAdded += OnDepotAdded;
        }

        void Unsubscribe(World w)
        {
            w.WeekEnded -= OnWeekEnded;
            w.GameOver -= OnGameOver;
            w.Delivered -= OnDelivered;
            w.OverflowWarning -= OnOverflowWarning;
            w.StoreAdded -= OnBuildingAdded;
            w.DepotAdded -= OnDepotAdded;
        }

        void UpdateCameraLimits()
        {
            if (cameraRig == null || World == null) return;
            var b = World.Grid.ActiveWorldBounds;
            b.Expand(new Vector3(40f, 0f, 40f));
            cameraRig.SetLimits(b);
            cameraRig.maxDistance = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 1.05f, 160f, 360f);
        }

        void EnterMenu()
        {
            State = GameState.Menu;
            if (cameraRig != null) { cameraRig.orbit = true; cameraRig.inputEnabled = false; }
            ui?.ShowMenu(PlayerPrefs.GetInt(BestKey, 0));
        }

        public void StartGame()
        {
            if (begun) NewWorld();
            begun = true;
            World.Begin();
            State = GameState.Playing;
            SetSpeed(1);
            if (cameraRig != null)
            {
                cameraRig.orbit = false;
                cameraRig.inputEnabled = true;
                var focus = World.Depots.Count > 0 ? World.Depots[0].WorldCenter(World.Grid) : World.Grid.ActiveWorldBounds.center;
                cameraRig.Focus(focus, 120f);
            }
            tools.SetTool(Tool.Road);
            ui?.ShowHud();
            ui?.Toast("Depodan mağazalara yol çiz — kamyonlar yolu kendileri bulur");
            audioManager?.Play(Sfx.Sting, 0.6f, 0f);
        }

        public void Restart()
        {
            NewWorld();
            begun = false;
            StartGame();
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void SetSpeed(int s)
        {
            Speed = Mathf.Clamp(s, 1, 3);
            ui?.SetSpeed(Speed);
        }

        public void SetPaused(bool paused, bool showMenu = true)
        {
            if (paused && State == GameState.Playing)
            {
                State = GameState.Paused;
                ui?.SetSpeed(0);
                if (showMenu) ui?.ShowPause(true);
            }
            else if (!paused && State == GameState.Paused)
            {
                State = GameState.Playing;
                ui?.SetSpeed(Speed);
                ui?.ShowPause(false);
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    if (State == GameState.Playing) SetPaused(true, showMenu: false);
                    else if (State == GameState.Paused) SetPaused(false);
                }
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (State == GameState.Playing) SetPaused(true);
                    else if (State == GameState.Paused) SetPaused(false);
                }
                if (State == GameState.Playing || State == GameState.Paused)
                {
                    if (kb.digit1Key.wasPressedThisFrame) { SetSpeed(1); SetPaused(false); }
                    if (kb.digit2Key.wasPressedThisFrame) { SetSpeed(2); SetPaused(false); }
                }
            }

            if (State == GameState.Playing && World != null)
                World.Tick(Mathf.Min(Time.deltaTime, 0.1f) * Speed);

            if (World != null && State != GameState.Menu)
                ui?.UpdateHud(World, World.Spawner.ActiveCargo);
        }

        void OnWeekEnded(int week)
        {
            UpdateCameraLimits();
            offer = UpgradeSystem.Offer(World);
            State = GameState.Upgrade;
            ui?.ShowUpgrade(week, offer, config);
            audioManager?.Play(Sfx.Sting, 0.8f, 0f);
        }

        void ChooseUpgrade(int i)
        {
            if (State != GameState.Upgrade || offer == null) return;
            UpgradeSystem.Apply(World, offer[i]);
            offer = null;
            ui?.HideUpgrade();
            State = GameState.Playing;
        }

        void OnGameOver(Store s)
        {
            State = GameState.GameOver;
            int best = Mathf.Max(PlayerPrefs.GetInt(BestKey, 0), World.Score);
            PlayerPrefs.SetInt(BestKey, best);
            PlayerPrefs.Save();
            cameraRig?.Focus(World.Grid.TileCenter(s.origin), 70f);
            cameraRig?.Shake(0.6f);
            audioManager?.Play(Sfx.GameOver, 0.9f, 0f);
            ui?.ShowGameOver(World, best);
        }

        void OnDelivered(Store s, TruckAgent t) => audioManager?.Play(Sfx.Deliver, 0.5f, 0.08f);

        void OnOverflowWarning(Store s)
        {
            if (Time.frameCount - lastWarningFrame < 30) return;
            lastWarningFrame = Time.frameCount;
            audioManager?.Play(Sfx.Warning, 0.7f, 0f);
            ui?.Toast($"{CargoInfo.NameOf(s.cargo)} mağazası bekliyor!", true);
        }

        void OnBuildingAdded(Store s)
        {
            if (State == GameState.Playing) audioManager?.Play(Sfx.Spawn, 0.45f, 0.05f);
        }

        void OnDepotAdded(Depot d)
        {
            if (State != GameState.Playing) return;
            audioManager?.Play(Sfx.Spawn, 0.7f, 0f);
            if (World.Depots.Count > 1) ui?.Toast($"Yeni depo: {CargoInfo.NameOf(d.cargo)}");
        }
    }
}
