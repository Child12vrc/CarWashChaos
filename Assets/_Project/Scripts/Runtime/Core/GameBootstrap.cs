using CarWash.Data;
using CarWash.Net;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CarWash.Core
{
    /// <summary>
    /// Lives in Bootstrap.unity. Registers persistent services, then loads MainMenu (TECH §2).
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private DataRegistry _data;
        [SerializeField] private NetSession _net;
        [SerializeField] private AudioService _audio;
        [SerializeField] private UI.UiSkin _skin;

        public static GameBootstrap Instance { get; private set; }
        public GameStateMachine Flow { get; private set; }

        void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            // dev: -fps N caps the frame rate (P1 measurements at a realistic 60 fps; P2 settings own this for players)
            var argv = System.Environment.GetCommandLineArgs();
            int fi = System.Array.IndexOf(argv, "-fps");
            if (fi >= 0 && fi + 1 < argv.Length && int.TryParse(argv[fi + 1], out int fps)) { QualitySettings.vSyncCount = 0; Application.targetFrameRate = fps; }

            Services.Clear();
            Flow = new GameStateMachine();
            Flow.Changed += (_, to) => GameEvents.RaiseStateChanged(to);
            Services.Register(Flow);
            Services.Register(_data);
            var settings = new Settings();
            Services.Register(settings);
            settings.Apply();                       // P2: brightness volume, master volume, quality, resolution, vsync
            settings.Changed += settings.Apply;
            var save = new SaveSystem();
            save.Load();
            Services.Register(save);
            if (_audio) Services.Register(_audio);
            if (_skin) Services.Register(_skin);
            if (_net) Services.Register(_net);
            if (DevAutopilot.Requested) gameObject.AddComponent<DevAutopilot>();
        }

        void Start()
        {
            Flow.TryChange(GameState.Menu);
            SceneManager.LoadScene(SceneNames.MainMenu);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
