using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NinetyNine
{
    public sealed class ProceduralWorld : MonoBehaviour
    {
        private readonly List<Light> _corridorLights = new List<Light>();
        private readonly Dictionary<string, PassengerVisual> _onboardPassengers = new Dictionary<string, PassengerVisual>();
        private readonly Dictionary<SurvivalControl, Renderer> _survivalControlLights = new Dictionary<SurvivalControl, Renderer>();
        private NinetyNineGame _game;
        private NinetyNineSurvivalGame _survivalGame;
        private Transform _root;
        private Transform _floorRoot;
        private Transform _passengerRoot;
        private Transform _survivalWatcher;
        private Transform _survivalCrawler;
        private Transform _mirrorDouble;
        private GameObject _survivalBarrier;
        private Transform _leftDoor;
        private Transform _rightDoor;
        private TextMesh _floorDisplay;
        private TextMesh _survivalTimeDisplay;
        private TextMesh _survivalPowerDisplay;
        private TextMesh _survivalAlertDisplay;
        private Light _cabinLight;
        private Light _warningLight;
        private Light _uvLight;
        private Light _fuseSparkLight;
        private Material _brass;
        private Material _darkMetal;
        private Material _wall;
        private Material _trim;
        private Material _floor;
        private Material _door;
        private Material _black;
        private Material _redGlow;
        private Material _cyanGlow;
        private Material _warmGlow;
        private Material _glass;
        private Material _memoryPoster;
        private float _doorAmount;
        private float _doorTarget;
        private float _flickerClock;
        private int _flickerSeed;
        private float _crawlerDanger;
        private float _mirrorDanger;
        private float _fuseDanger;

        private sealed class PassengerVisual
        {
            public Transform root;
            public Transform head;
            public bool anomaly;
            public float phase;
        }

        public FirstPersonController Player { get; private set; }

        public void Initialize(NinetyNineGame game)
        {
            _game = game;
            InitializeWorld();
        }

        public void InitializeSurvival(NinetyNineSurvivalGame game)
        {
            _survivalGame = game;
            InitializeWorld();
        }

        private void InitializeWorld()
        {
            ConfigureAtmosphere();
            CreateMaterials();

            _root = new GameObject("ProceduralWorld").transform;
            _root.SetParent(transform, false);
            BuildCabin();
            BuildPlayer();
            _floorRoot = new GameObject("CurrentFloor").transform;
            _floorRoot.SetParent(_root, false);
            BuildNeutralHall();
            SetFloorNumber(0);
            _doorAmount = 1f;
            _doorTarget = 1f;
            ApplyDoorPositions();
        }

        public void ResetToTitle()
        {
            ClearOnboardPassengers();
            BuildNeutralHall();
            SetFloorNumber(0);
            SetDoorsOpen(true);
        }

        public void BeginSurvival()
        {
            ClearOnboardPassengers();
            ClearFloor();
            ApplyMood(BuildingMood.Cold);
            BuildHallShell(24f, BuildingMood.Cold);
            PassengerVisual watcher = CreatePassengerModel(PassengerKind.Mourner,
                new Vector3(0f, 0f, 24f), _floorRoot, true, AnomalyTrait.NoBreath, 99);
            watcher.root.name = "TheWatcher";
            _survivalWatcher = watcher.root;

            if (_survivalBarrier != null)
            {
                Destroy(_survivalBarrier);
            }
            _survivalBarrier = new GameObject("SurvivalBarrier");
            _survivalBarrier.transform.SetParent(_root, false);
            _survivalBarrier.transform.localPosition = new Vector3(0f, 1.5f, 2.42f);
            BoxCollider barrier = _survivalBarrier.AddComponent<BoxCollider>();
            barrier.size = new Vector3(3.15f, 3f, 0.12f);
        }

        public void SetWatcherStage(int stage)
        {
            if (_survivalWatcher == null)
            {
                return;
            }
            float[] distances = { 24f, 17f, 11f, 5.3f, 2.82f };
            int index = Mathf.Clamp(stage, 0, distances.Length - 1);
            Vector3 target = new Vector3(0f, 0f, distances[index]);
            _survivalWatcher.localPosition = target;
            float scale = Mathf.Lerp(0.88f, 1.12f, index / 4f);
            _survivalWatcher.localScale = Vector3.one * scale;
        }

        public void SetCabinLightEnabled(bool enabled)
        {
            if (_cabinLight != null)
            {
                _cabinLight.enabled = enabled;
            }
            if (_warningLight != null)
            {
                _warningLight.enabled = enabled;
            }
        }

        public void ResetSurvivalDevices()
        {
            if (_survivalCrawler != null)
            {
                _survivalCrawler.gameObject.SetActive(false);
            }
            if (_mirrorDouble != null)
            {
                _mirrorDouble.gameObject.SetActive(false);
            }
            if (_uvLight != null)
            {
                _uvLight.enabled = false;
            }
            if (_fuseSparkLight != null)
            {
                _fuseSparkLight.enabled = false;
            }
            _crawlerDanger = 0f;
            _mirrorDanger = 0f;
            _fuseDanger = 0f;
            foreach (Renderer indicator in _survivalControlLights.Values)
            {
                SetIndicator(indicator, new Color(0.04f, 0.14f, 0.13f));
            }
            SetSurvivalDisplays(99, 99f, "STANDBY");
        }

        public void SetSurvivalDisplays(int seconds, float power, string alert)
        {
            if (_survivalTimeDisplay != null)
            {
                _survivalTimeDisplay.text = Mathf.Clamp(seconds, 0, 99).ToString("00") + " SEC";
            }
            if (_survivalPowerDisplay != null)
            {
                _survivalPowerDisplay.text = "PWR " + Mathf.Clamp(Mathf.CeilToInt(power), 0, 99).ToString("00");
                _survivalPowerDisplay.color = power > 40f ? new Color(0.16f, 1f, 0.76f) :
                    power > 18f ? new Color(1f, 0.55f, 0.08f) : new Color(1f, 0.08f, 0.025f);
            }
            if (_survivalAlertDisplay != null)
            {
                _survivalAlertDisplay.text = alert;
            }
        }

        public void SetSurvivalControlState(SurvivalControl control, bool active, bool fault)
        {
            Renderer indicator;
            if (!_survivalControlLights.TryGetValue(control, out indicator))
            {
                return;
            }
            Color color = fault ? new Color(0.9f, 0.025f, 0.01f) :
                active ? new Color(0.08f, 1f, 0.68f) : new Color(0.04f, 0.14f, 0.13f);
            SetIndicator(indicator, color);
        }

        public void SetCrawlerState(bool active, float danger, bool ultraviolet)
        {
            _crawlerDanger = Mathf.Clamp01(danger);
            if (_survivalCrawler != null)
            {
                _survivalCrawler.gameObject.SetActive(active);
            }
            if (_uvLight != null)
            {
                _uvLight.enabled = ultraviolet;
            }
        }

        public void SetMirrorState(bool active, float danger)
        {
            _mirrorDanger = Mathf.Clamp01(danger);
            if (_mirrorDouble != null)
            {
                _mirrorDouble.gameObject.SetActive(active);
            }
        }

        public void SetFuseFault(bool active, float danger)
        {
            _fuseDanger = Mathf.Clamp01(danger);
            if (_fuseSparkLight != null)
            {
                _fuseSparkLight.enabled = active;
            }
        }

        public void BuildPassengerEncounter(PassengerEncounter encounter, BuildingMood mood, int stopIndex)
        {
            BuildFloor(encounter.floorKind, mood, stopIndex);
            Vector3 waitingPosition = encounter.trait == AnomalyTrait.Inverted
                ? new Vector3(0f, 2.75f, 6.35f)
                : new Vector3(0f, 0f, 6.35f);
            PassengerVisual waiting = CreatePassengerModel(encounter.kind, waitingPosition, _floorRoot,
                encounter.anomaly, encounter.trait, encounter.destinationFloor);
            waiting.root.name = "WaitingPassenger_" + encounter.id;
            if (encounter.trait == AnomalyTrait.Inverted)
            {
                waiting.root.localRotation = Quaternion.Euler(0f, 180f, 180f);
            }
            if (encounter.trait == AnomalyTrait.Duplicate)
            {
                PassengerVisual duplicate = CreatePassengerModel(encounter.kind,
                    new Vector3(0.95f, 0f, 14.5f), _floorRoot, true, encounter.trait,
                    encounter.destinationFloor);
                duplicate.root.localScale = Vector3.one * 0.94f;
                duplicate.root.name = "DuplicatePassenger";
            }
        }

        public void AddOnboardPassenger(PassengerEncounter encounter)
        {
            if (_onboardPassengers.ContainsKey(encounter.id))
            {
                return;
            }
            Vector3[] slots =
            {
                new Vector3(-1.35f, 0f, -1.45f),
                new Vector3(1.35f, 0f, -1.45f),
                new Vector3(-1.62f, 0f, -0.1f),
                new Vector3(1.62f, 0f, -0.1f),
                new Vector3(-1.42f, 0f, 1.1f),
                new Vector3(1.42f, 0f, 1.1f)
            };
            Vector3 slot = slots[Mathf.Min(_onboardPassengers.Count, slots.Length - 1)];
            PassengerVisual visual = CreatePassengerModel(encounter.kind, slot, _passengerRoot,
                encounter.anomaly, encounter.trait, encounter.destinationFloor);
            visual.root.name = "Onboard_" + encounter.id;
            visual.root.localScale = Vector3.one * 0.86f;
            _onboardPassengers.Add(encounter.id, visual);
        }

        public void RemoveOnboardPassenger(string id)
        {
            PassengerVisual visual;
            if (!_onboardPassengers.TryGetValue(id, out visual))
            {
                return;
            }
            if (visual.root != null)
            {
                Destroy(visual.root.gameObject);
            }
            _onboardPassengers.Remove(id);
        }

        public void ClearOnboardPassengers()
        {
            foreach (PassengerVisual visual in _onboardPassengers.Values)
            {
                if (visual.root != null)
                {
                    Destroy(visual.root.gameObject);
                }
            }
            _onboardPassengers.Clear();
        }

        public void SetDoorsOpen(bool open)
        {
            _doorTarget = open ? 1f : 0f;
        }

        public void SetFloorNumber(int floorNumber)
        {
            if (_floorDisplay != null)
            {
                _floorDisplay.text = Mathf.Clamp(floorNumber, 0, 99).ToString("00");
            }
        }

        public void BuildFloor(FloorKind kind, BuildingMood mood, int stopIndex)
        {
            ClearFloor();
            ApplyMood(mood);
            _flickerSeed = stopIndex * 31 + (int)kind * 17;
            float length = kind == FloorKind.EndlessCorridor ? 38f : 19f;
            BuildHallShell(length, mood);

            switch (kind)
            {
                case FloorKind.Ordinary:
                    BuildOrdinary();
                    break;
                case FloorKind.Maintenance:
                    BuildMaintenance();
                    break;
                case FloorKind.MovingBoxes:
                    BuildMovingBoxes();
                    break;
                case FloorKind.Laundry:
                    BuildLaundry();
                    break;
                case FloorKind.TooManyDoors:
                    BuildTooManyDoors(length);
                    break;
                case FloorKind.FacelessResidents:
                    BuildFacelessResidents();
                    break;
                case FloorKind.CeilingRoom:
                    BuildCeilingRoom();
                    break;
                case FloorKind.RedPhone:
                    BuildRedPhone();
                    break;
                case FloorKind.EndlessCorridor:
                    BuildEndlessCorridor(length);
                    break;
                case FloorKind.DuplicateElevator:
                    BuildDuplicateElevator(length);
                    break;
                case FloorKind.BlackSun:
                    BuildBlackSun(length);
                    break;
                case FloorKind.FloatingRoom:
                    BuildFloatingRoom();
                    break;
                case FloorKind.WatchingEyes:
                    BuildWatchingEyes(length);
                    break;
                case FloorKind.StairIntoWall:
                    BuildStairIntoWall(length);
                    break;
                case FloorKind.FloodedHall:
                    BuildFloodedHall(length);
                    break;
                case FloorKind.ChildShadow:
                    BuildChildShadow();
                    break;
            }
        }

        public void BuildFinalFloor(int correct, BuildingMood mood)
        {
            ClearFloor();
            ApplyMood(mood);
            BuildHallShell(22f, mood);

            if (correct >= 8)
            {
                Box("WhiteThreshold", new Vector3(0f, 1.6f, 21.2f), new Vector3(3.1f, 3.1f, 0.08f),
                    _cyanGlow, _floorRoot, false);
                Light exitLight = CreateLight("ExitLight", new Vector3(0f, 2.1f, 17.2f),
                    new Color(0.55f, 0.95f, 1f), 7f, 14f, _floorRoot);
                exitLight.shadows = LightShadows.Soft;
            }
            else if (correct >= 6)
            {
                for (int i = 0; i < 11; i++)
                {
                    float side = i % 2 == 0 ? -1f : 1f;
                    CreateResident(new Vector3(side * (0.7f + (i % 3) * 0.26f), 0f,
                        5.2f + i * 1.25f), 180f, i % 3 == 0);
                }
            }
            else
            {
                BuildDuplicateElevator(22f);
                for (int i = 0; i < 5; i++)
                {
                    Box("LoopFrame", new Vector3(0f, 1.55f, 6f + i * 3.2f),
                        new Vector3(3.7f - i * 0.33f, 2.8f - i * 0.2f, 0.08f), _black, _floorRoot, false);
                }
            }
        }

        private void Update()
        {
            _doorAmount = Mathf.MoveTowards(_doorAmount, _doorTarget, Time.deltaTime * 1.15f);
            ApplyDoorPositions();
            UpdateLights();
            UpdateSurvivalThreats();
            UpdateOnboardPassengers();
        }

        private void ConfigureAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.075f, 0.095f, 0.1f);
            RenderSettings.ambientEquatorColor = new Color(0.045f, 0.06f, 0.062f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.012f, 0.014f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;
            RenderSettings.fogColor = new Color(0.015f, 0.035f, 0.04f);
        }

        private void CreateMaterials()
        {
            Texture2D brassTexture = Resources.Load<Texture2D>("Art/brass_tile");
            Texture2D wallTexture = Resources.Load<Texture2D>("Art/wall_tile");
            Texture2D titleTexture = Resources.Load<Texture2D>("Art/title_hall");
            if (brassTexture != null)
            {
                brassTexture.wrapMode = TextureWrapMode.Repeat;
            }
            if (wallTexture != null)
            {
                wallTexture.wrapMode = TextureWrapMode.Repeat;
            }

            _brass = MakeMaterial("Aged Brass", new Color(0.32f, 0.24f, 0.09f), 0.72f, 0.36f);
            _brass.mainTexture = brassTexture;
            _brass.mainTextureScale = new Vector2(1.5f, 1.5f);
            _darkMetal = MakeMaterial("Dark Metal", new Color(0.035f, 0.045f, 0.045f), 0.82f, 0.28f);
            _wall = MakeMaterial("Aged Wall", new Color(0.58f, 0.66f, 0.64f), 0.02f, 0.12f);
            _wall.mainTexture = wallTexture;
            _wall.mainTextureScale = new Vector2(1.8f, 1.8f);
            _trim = MakeMaterial("Wall Trim", new Color(0.045f, 0.075f, 0.075f), 0.25f, 0.18f);
            _floor = MakeMaterial("Linoleum", new Color(0.075f, 0.105f, 0.105f), 0.08f, 0.34f);
            _door = MakeMaterial("Apartment Door", new Color(0.055f, 0.105f, 0.105f), 0.15f, 0.22f);
            _black = MakeMaterial("Lightless", new Color(0.001f, 0.001f, 0.001f), 0f, 0f);
            _redGlow = MakeEmissive("Red Glow", new Color(0.7f, 0.025f, 0.012f), 4.8f);
            _cyanGlow = MakeEmissive("Cyan Glow", new Color(0.2f, 0.9f, 0.88f), 2.7f);
            _warmGlow = MakeEmissive("Warm Glow", new Color(1f, 0.48f, 0.12f), 2.2f);
            _glass = MakeTransparent("Water Glass", new Color(0.08f, 0.35f, 0.4f, 0.34f));
            _memoryPoster = MakeMaterial("Memory Poster", Color.white, 0f, 0.1f);
            _memoryPoster.mainTexture = titleTexture;
            _memoryPoster.EnableKeyword("_EMISSION");
            _memoryPoster.SetColor("_EmissionColor", new Color(0.08f, 0.12f, 0.12f));
        }

        private void BuildCabin()
        {
            Transform cabin = new GameObject("ElevatorCabin").transform;
            cabin.SetParent(_root, false);
            _passengerRoot = new GameObject("OnboardPassengers").transform;
            _passengerRoot.SetParent(cabin, false);
            Box("CabinFloor", new Vector3(0f, -0.08f, 0f), new Vector3(4.7f, 0.16f, 4.7f), _floor, cabin);
            Box("CabinCeiling", new Vector3(0f, 3.2f, 0f), new Vector3(4.7f, 0.12f, 4.7f), _darkMetal, cabin);
            Box("BackWall", new Vector3(0f, 1.55f, -2.3f), new Vector3(4.7f, 3.2f, 0.12f), _brass, cabin);
            Box("LeftWall", new Vector3(-2.3f, 1.55f, 0f), new Vector3(0.12f, 3.2f, 4.7f), _brass, cabin);
            Box("RightWall", new Vector3(2.3f, 1.55f, 0f), new Vector3(0.12f, 3.2f, 4.7f), _brass, cabin);
            Box("FrontLeft", new Vector3(-1.86f, 1.55f, 2.28f), new Vector3(0.86f, 3.2f, 0.15f), _darkMetal, cabin);
            Box("FrontRight", new Vector3(1.86f, 1.55f, 2.28f), new Vector3(0.86f, 3.2f, 0.15f), _darkMetal, cabin);
            Box("DoorHeader", new Vector3(0f, 2.97f, 2.28f), new Vector3(2.9f, 0.38f, 0.15f), _darkMetal, cabin);

            _leftDoor = Box("LeftDoor", new Vector3(-0.74f, 1.48f, 2.24f), new Vector3(1.46f, 2.78f, 0.11f), _brass, cabin);
            _rightDoor = Box("RightDoor", new Vector3(0.74f, 1.48f, 2.24f), new Vector3(1.46f, 2.78f, 0.11f), _brass, cabin);

            if (_survivalGame == null)
            {
                Box("ControlPanel", new Vector3(2.225f, 1.42f, 0.68f), new Vector3(0.055f, 1.48f, 0.7f), _darkMetal, cabin);
                for (int i = 0; i < 9; i++)
                {
                    float y = 1.88f - (i / 3) * 0.31f;
                    float z = 0.45f + (i % 3) * 0.22f;
                    Sphere("Button", new Vector3(2.18f, y, z), new Vector3(0.075f, 0.075f, 0.075f),
                        i == 8 ? _redGlow : _warmGlow, cabin, false);
                }
            }

            Transform display = Box("FloorDisplay", new Vector3(0f, 2.73f, 2.185f),
                new Vector3(0.74f, 0.32f, 0.04f), _black, cabin, false);
            GameObject textObject = new GameObject("FloorDigits");
            textObject.transform.SetParent(display, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.55f);
            textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            _floorDisplay = textObject.AddComponent<TextMesh>();
            _floorDisplay.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _floorDisplay.fontSize = 96;
            _floorDisplay.characterSize = 0.11f;
            _floorDisplay.anchor = TextAnchor.MiddleCenter;
            _floorDisplay.alignment = TextAlignment.Center;
            _floorDisplay.color = new Color(1f, 0.12f, 0.045f);
            MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
            textRenderer.material = _floorDisplay.font.material;

            _cabinLight = CreateLight("CabinLight", new Vector3(0f, 2.88f, -0.2f),
                new Color(0.95f, 0.66f, 0.32f), 2.7f, 7f, cabin);
            _cabinLight.type = LightType.Point;
            _cabinLight.shadows = LightShadows.Soft;
            _warningLight = CreateLight("WarningLight", new Vector3(-1.95f, 2.45f, 1.95f),
                new Color(1f, 0.04f, 0.015f), 1.4f, 3.5f, cabin);
            Sphere("WarningLens", new Vector3(-2.02f, 2.46f, 1.93f), new Vector3(0.11f, 0.11f, 0.11f),
                _redGlow, cabin, false);

            if (_survivalGame != null)
            {
                BuildSurvivalStations(cabin);
            }
        }

        private void BuildSurvivalStations(Transform cabin)
        {
            CreateStation("CORRIDOR MONITOR", "按住监控", SurvivalControl.Monitor,
                new Vector3(-2.19f, 1.58f, 0.68f), Quaternion.Euler(0f, -90f, 0f),
                new Vector3(0.62f, 0.82f, 0.09f), cabin, 0f);
            CreateStation("DOOR OVERRIDE", "按住门控", SurvivalControl.Door,
                new Vector3(2.19f, 1.2f, 0.52f), Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0.54f, 0.58f, 0.09f), cabin, 0f);
            CreateStation("CABIN LIGHT", "切换照明", SurvivalControl.Light,
                new Vector3(2.19f, 1.02f, -1.22f), Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0.55f, 0.42f, 0.09f), cabin, 0f);
            CreateStation("CEILING UV", "按住紫外灯", SurvivalControl.Ultraviolet,
                new Vector3(2.19f, 2.32f, -0.3f), Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0.62f, 0.5f, 0.09f), cabin, 0f);
            CreateStation("INTERCOM", "使用对讲机", SurvivalControl.Intercom,
                new Vector3(2.19f, 1.82f, 1.45f), Quaternion.Euler(0f, 90f, 0f),
                new Vector3(0.52f, 0.56f, 0.09f), cabin, 0f);
            CreateStation("FUSE BOX", "按住复位", SurvivalControl.FuseBox,
                new Vector3(-1.36f, 1.32f, -2.19f), Quaternion.Euler(0f, 180f, 0f),
                new Vector3(0.78f, 0.86f, 0.09f), cabin, 1.25f);
            CreateStation("EMERGENCY BRAKE", "按住制动", SurvivalControl.Brake,
                new Vector3(1.36f, 1.24f, -2.19f), Quaternion.Euler(0f, 180f, 0f),
                new Vector3(0.78f, 0.7f, 0.09f), cabin, 1.1f);

            Transform timePanel = Box("SurvivalTimePanel", new Vector3(-1.03f, 2.77f, 2.18f),
                new Vector3(0.92f, 0.3f, 0.045f), _black, cabin, false);
            _survivalTimeDisplay = CreateWorldText("TimeDigits", timePanel, "99 SEC",
                new Vector3(0f, 0f, -0.57f), Vector3.zero, 0.018f,
                new Color(1f, 0.12f, 0.045f), TextAnchor.MiddleCenter);
            Transform powerPanel = Box("SurvivalPowerPanel", new Vector3(1.03f, 2.77f, 2.18f),
                new Vector3(0.92f, 0.3f, 0.045f), _black, cabin, false);
            _survivalPowerDisplay = CreateWorldText("PowerDigits", powerPanel, "PWR 99",
                new Vector3(0f, 0f, -0.57f), Vector3.zero, 0.018f,
                new Color(0.16f, 1f, 0.76f), TextAnchor.MiddleCenter);
            Transform alertPanel = Box("SurvivalAlertPanel", new Vector3(0f, 2.38f, 2.18f),
                new Vector3(1.08f, 0.2f, 0.045f), _black, cabin, false);
            _survivalAlertDisplay = CreateWorldText("AlertDigits", alertPanel, "STANDBY",
                new Vector3(0f, 0f, -0.57f), Vector3.zero, 0.014f,
                new Color(1f, 0.48f, 0.08f), TextAnchor.MiddleCenter);

            Material mirrorMaterial = MakeTransparent("Cabin Mirror", new Color(0.13f, 0.22f, 0.22f, 0.48f));
            Box("Mirror", new Vector3(0f, 1.62f, -2.225f), new Vector3(0.82f, 1.48f, 0.025f),
                mirrorMaterial, cabin, false);
            _mirrorDouble = new GameObject("MirrorDouble").transform;
            _mirrorDouble.SetParent(cabin, false);
            _mirrorDouble.localPosition = new Vector3(0f, 0f, -2.16f);
            Capsule("MirrorBody", new Vector3(0f, 0.88f, 0f), new Vector3(0.38f, 0.78f, 0.24f),
                _black, _mirrorDouble, false);
            Sphere("MirrorHead", new Vector3(0f, 1.78f, 0f), new Vector3(0.3f, 0.38f, 0.2f),
                _black, _mirrorDouble, false);
            Sphere("MirrorLeftEye", new Vector3(-0.1f, 1.82f, 0.19f), new Vector3(0.025f, 0.018f, 0.012f),
                _redGlow, _mirrorDouble, false);
            Sphere("MirrorRightEye", new Vector3(0.1f, 1.82f, 0.19f), new Vector3(0.025f, 0.018f, 0.012f),
                _redGlow, _mirrorDouble, false);

            Transform vent = Box("CeilingVent", new Vector3(0f, 3.12f, -0.2f),
                new Vector3(1.25f, 0.055f, 1.05f), _black, cabin, false);
            for (int i = 0; i < 5; i++)
            {
                Box("VentSlat", new Vector3(-0.42f + i * 0.21f, -0.62f, 0f),
                    new Vector3(0.055f, 0.06f, 0.82f), _darkMetal, vent, false);
            }
            _survivalCrawler = new GameObject("CeilingCrawler").transform;
            _survivalCrawler.SetParent(cabin, false);
            _survivalCrawler.localPosition = new Vector3(0f, 2.98f, -1.15f);
            Transform crawlerBody = Capsule("CrawlerBody", Vector3.zero,
                new Vector3(0.22f, 0.68f, 0.2f), _black, _survivalCrawler, false);
            crawlerBody.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Sphere("CrawlerHead", new Vector3(0.65f, -0.08f, 0f), new Vector3(0.3f, 0.22f, 0.3f),
                _black, _survivalCrawler, false);
            for (int i = 0; i < 6; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                Transform limb = Cylinder("CrawlerLimb", new Vector3(-0.42f + (i / 2) * 0.38f,
                    -0.12f, side * 0.34f), new Vector3(0.035f, 0.42f, 0.035f), _black,
                    _survivalCrawler, false);
                limb.localRotation = Quaternion.Euler(62f, 0f, side * 42f);
            }
            _uvLight = CreateLight("UltravioletFlood", new Vector3(0f, 2.72f, -0.15f),
                new Color(0.3f, 0.12f, 1f), 5.2f, 4.5f, cabin);
            _uvLight.enabled = false;
            _fuseSparkLight = CreateLight("FuseSparks", new Vector3(-1.35f, 1.55f, -1.85f),
                new Color(1f, 0.12f, 0.015f), 5f, 3.2f, cabin);
            _fuseSparkLight.enabled = false;
            ResetSurvivalDevices();
        }

        private void CreateStation(string objectName, string label, SurvivalControl control,
            Vector3 position, Quaternion rotation, Vector3 scale, Transform cabin, float holdDuration)
        {
            Transform root = new GameObject(objectName).transform;
            root.SetParent(cabin, false);
            root.localPosition = position;
            root.localRotation = rotation;
            Transform panel = Box("InteractionSurface", Vector3.zero, scale, _darkMetal, root);
            SurvivalInteractable interactable = panel.gameObject.AddComponent<SurvivalInteractable>();
            interactable.Configure(control, label, holdDuration);

            Material indicatorMaterial = MakeEmissive(objectName + " Indicator",
                new Color(0.04f, 0.14f, 0.13f), 2.4f);
            Transform indicator = Box("Indicator", new Vector3(0f, scale.y * 0.28f,
                -scale.z * 0.55f - 0.012f), new Vector3(scale.x * 0.42f, 0.075f, 0.025f),
                indicatorMaterial, root, false);
            _survivalControlLights[control] = indicator.GetComponent<Renderer>();
            CreateWorldText("Label", root, objectName,
                new Vector3(0f, -scale.y * 0.12f, -scale.z * 0.55f - 0.016f),
                Vector3.zero, 0.0065f, new Color(0.68f, 0.82f, 0.78f),
                TextAnchor.MiddleCenter);
        }

        private static TextMesh CreateWorldText(string name, Transform parent, string value,
            Vector3 position, Vector3 rotation, float characterSize, Color color, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            textObject.transform.localRotation = Quaternion.Euler(rotation);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 80;
            text.characterSize = characterSize;
            text.anchor = anchor;
            text.alignment = TextAlignment.Center;
            text.color = color;
            textObject.GetComponent<MeshRenderer>().material = text.font.material;
            return text;
        }

        private void BuildPlayer()
        {
            GameObject playerObject = new GameObject("Player");
            playerObject.transform.SetParent(_root, false);
            playerObject.transform.position = new Vector3(0f, 0.08f, -0.72f);
            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.72f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.88f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 50f;

            GameObject cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 68f;
            camera.nearClipPlane = 0.035f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.004f, 0.008f, 0.01f);
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            AnalogPostEffect post = cameraObject.AddComponent<AnalogPostEffect>();
            post.Game = _game;
            post.SurvivalGame = _survivalGame;

            Player = playerObject.AddComponent<FirstPersonController>();
            Player.Initialize(camera);
        }

        private void BuildNeutralHall()
        {
            ClearFloor();
            ApplyMood(BuildingMood.Cold);
            BuildHallShell(19f, BuildingMood.Cold);
            BuildOrdinary();
        }

        private void BuildHallShell(float length, BuildingMood mood)
        {
            float centerZ = 2.35f + length * 0.5f;
            Box("HallFloor", new Vector3(0f, -0.08f, centerZ), new Vector3(4.4f, 0.14f, length), _floor, _floorRoot);
            Box("HallCeiling", new Vector3(0f, 3.15f, centerZ), new Vector3(4.4f, 0.12f, length), _wall, _floorRoot);
            Box("HallLeft", new Vector3(-2.15f, 1.53f, centerZ), new Vector3(0.12f, 3.12f, length), _wall, _floorRoot);
            Box("HallRight", new Vector3(2.15f, 1.53f, centerZ), new Vector3(0.12f, 3.12f, length), _wall, _floorRoot);
            Box("HallEnd", new Vector3(0f, 1.53f, 2.35f + length), new Vector3(4.4f, 3.12f, 0.12f), _wall, _floorRoot);
            Box("LeftTrim", new Vector3(-2.075f, 0.65f, centerZ), new Vector3(0.08f, 0.15f, length), _trim, _floorRoot, false);
            Box("RightTrim", new Vector3(2.075f, 0.65f, centerZ), new Vector3(0.08f, 0.15f, length), _trim, _floorRoot, false);

            int doorCount = Mathf.FloorToInt(length / 4f);
            for (int i = 0; i < doorCount; i++)
            {
                float z = 5f + i * 4f;
                AddApartmentDoor(-1, z);
                AddApartmentDoor(1, z);
            }

            Color lightColor = mood == BuildingMood.Amber
                ? new Color(1f, 0.56f, 0.26f)
                : mood == BuildingMood.Crimson
                    ? new Color(1f, 0.14f, 0.08f)
                    : new Color(0.3f, 0.86f, 0.88f);
            int lightCount = Mathf.CeilToInt(length / 4.5f);
            for (int i = 0; i < lightCount; i++)
            {
                float z = 4.2f + i * 4.4f;
                Box("Fluorescent", new Vector3(0f, 3.05f, z), new Vector3(0.72f, 0.05f, 0.18f),
                    mood == BuildingMood.Amber ? _warmGlow : mood == BuildingMood.Crimson ? _redGlow : _cyanGlow,
                    _floorRoot, false);
                Light light = CreateLight("HallLight", new Vector3(0f, 2.72f, z), lightColor,
                    mood == BuildingMood.Crimson ? 2f : 2.7f, 6.8f, _floorRoot);
                light.shadows = i < 3 ? LightShadows.Soft : LightShadows.None;
                _corridorLights.Add(light);
            }
        }

        private void AddApartmentDoor(int side, float z)
        {
            float x = side * 2.075f;
            Transform door = Box("ApartmentDoor", new Vector3(x, 1.25f, z),
                new Vector3(0.08f, 2.35f, 1.15f), _door, _floorRoot, false);
            door.localRotation = Quaternion.Euler(0f, 0f, 0f);
            Sphere("Handle", new Vector3(x - side * 0.08f, 1.2f, z + 0.35f),
                new Vector3(0.055f, 0.055f, 0.055f), _warmGlow, _floorRoot, false);
        }

        private void BuildOrdinary()
        {
            Cylinder("PlantPot", new Vector3(-1.45f, 0.28f, 7.1f), new Vector3(0.35f, 0.28f, 0.35f), _brass, _floorRoot);
            Sphere("Plant", new Vector3(-1.45f, 0.92f, 7.1f), new Vector3(0.55f, 0.75f, 0.55f), _trim, _floorRoot, false);
            Box("Doormat", new Vector3(1.8f, 0.02f, 5f), new Vector3(0.55f, 0.025f, 1.05f), _darkMetal, _floorRoot, false);
            Box("MemoryFrame", new Vector3(0f, 1.65f, 21.19f), new Vector3(2.2f, 1.35f, 0.045f), _darkMetal, _floorRoot, false);
            Box("MemoryImage", new Vector3(0f, 1.65f, 21.13f), new Vector3(1.98f, 1.12f, 0.025f), _memoryPoster, _floorRoot, false);
        }

        private void BuildMaintenance()
        {
            for (int i = 0; i < 3; i++)
            {
                Cylinder("SafetyCone", new Vector3(-0.7f + i * 0.7f, 0.28f, 7.3f + i * 0.22f),
                    new Vector3(0.23f, 0.38f, 0.23f), _warmGlow, _floorRoot);
            }
            Box("ToolCase", new Vector3(1.25f, 0.22f, 8.3f), new Vector3(0.85f, 0.42f, 0.4f), _darkMetal, _floorRoot);
            Box("OpenCeilingPanel", new Vector3(0.8f, 3.02f, 9f), new Vector3(1f, 0.04f, 1.3f), _black, _floorRoot, false);
        }

        private void BuildMovingBoxes()
        {
            for (int i = 0; i < 7; i++)
            {
                float x = 1.05f + (i % 2) * 0.46f;
                float y = 0.27f + (i / 2) * 0.5f;
                float z = 6.2f + (i % 3) * 0.55f;
                Box("MovingBox", new Vector3(x, y, z), new Vector3(0.72f, 0.52f, 0.72f), _brass, _floorRoot);
            }
        }

        private void BuildLaundry()
        {
            for (int i = 0; i < 3; i++)
            {
                Box("Washer", new Vector3(-1.3f + i * 1.3f, 0.65f, 8.6f),
                    new Vector3(1.05f, 1.25f, 0.9f), _wall, _floorRoot);
                Cylinder("WasherWindow", new Vector3(-1.3f + i * 1.3f, 0.67f, 8.12f),
                    new Vector3(0.32f, 0.04f, 0.32f), _black, _floorRoot, false,
                    new Vector3(90f, 0f, 0f));
            }
        }

        private void BuildTooManyDoors(float length)
        {
            for (int i = 0; i < 7; i++)
            {
                float x = -1.75f + i * 0.58f;
                Box("ImpossibleDoor", new Vector3(x, 1.22f, 2.28f + length - 0.1f),
                    new Vector3(0.52f, 2.32f, 0.08f), i % 2 == 0 ? _door : _darkMetal, _floorRoot, false);
            }
        }

        private void BuildFacelessResidents()
        {
            CreateResident(new Vector3(-0.85f, 0f, 7.2f), 180f, true);
            CreateResident(new Vector3(0.55f, 0f, 9.8f), 180f, true);
            CreateResident(new Vector3(1.15f, 0f, 13f), 180f, true);
        }

        private void BuildCeilingRoom()
        {
            for (int i = 0; i < 6; i++)
            {
                Box("CeilingFurniture", new Vector3(-1.2f + (i % 3) * 1.2f, 2.72f, 6.3f + (i / 3) * 3.5f),
                    new Vector3(0.9f, 0.48f, 1.35f), i % 2 == 0 ? _brass : _darkMetal, _floorRoot, false);
            }
            for (int i = 0; i < 4; i++)
            {
                Cylinder("HangingLeg", new Vector3(-1f + i * 0.65f, 2.15f, 9.8f),
                    new Vector3(0.07f, 0.55f, 0.07f), _darkMetal, _floorRoot, false);
            }
        }

        private void BuildRedPhone()
        {
            Box("PhoneTable", new Vector3(-1.45f, 0.55f, 9f), new Vector3(0.9f, 0.1f, 0.65f), _darkMetal, _floorRoot);
            Box("RedPhone", new Vector3(-1.45f, 0.72f, 9f), new Vector3(0.52f, 0.2f, 0.34f), _redGlow, _floorRoot, false);
            CreateLight("PhoneGlow", new Vector3(-1.45f, 1.1f, 9f), new Color(1f, 0.025f, 0.01f), 3.4f, 4f, _floorRoot);
            Box("CutCable", new Vector3(-1.05f, 0.66f, 9f), new Vector3(0.6f, 0.025f, 0.025f), _black, _floorRoot, false);
        }

        private void BuildEndlessCorridor(float length)
        {
            RenderSettings.fogDensity = 0.031f;
            for (int i = 0; i < 10; i++)
            {
                Box("DepthFrame", new Vector3(0f, 1.55f, 8f + i * 2.75f),
                    new Vector3(3.8f - i * 0.06f, 2.85f - i * 0.035f, 0.035f), _trim, _floorRoot, false);
            }
        }

        private void BuildDuplicateElevator(float length)
        {
            float z = 2.23f + length - 0.15f;
            Box("DuplicateFrame", new Vector3(0f, 1.55f, z), new Vector3(3.2f, 3.05f, 0.22f), _darkMetal, _floorRoot, false);
            Box("DuplicateDoorLeft", new Vector3(-0.72f, 1.46f, z - 0.13f), new Vector3(1.38f, 2.72f, 0.08f), _brass, _floorRoot, false);
            Box("DuplicateDoorRight", new Vector3(0.72f, 1.46f, z - 0.13f), new Vector3(1.38f, 2.72f, 0.08f), _brass, _floorRoot, false);
            Box("DuplicateDisplay", new Vector3(0f, 2.86f, z - 0.2f), new Vector3(0.65f, 0.24f, 0.06f), _redGlow, _floorRoot, false);
        }

        private void BuildBlackSun(float length)
        {
            Sphere("BlackSun", new Vector3(0f, 1.65f, 2.5f + length - 0.8f), new Vector3(2.3f, 2.3f, 2.3f), _black, _floorRoot, false);
            Light rim = CreateLight("ImpossibleLight", new Vector3(0f, 1.6f, 2.5f + length - 2.3f),
                new Color(1f, 0.25f, 0.08f), 7f, 12f, _floorRoot);
            rim.shadows = LightShadows.Hard;
        }

        private void BuildFloatingRoom()
        {
            for (int i = 0; i < 12; i++)
            {
                float x = -1.35f + (i % 4) * 0.9f;
                float y = 0.7f + (i % 3) * 0.68f;
                float z = 6f + (i / 4) * 3.3f;
                Box("FloatingObject", new Vector3(x, y, z),
                    new Vector3(0.42f + (i % 2) * 0.35f, 0.2f + (i % 3) * 0.18f, 0.55f),
                    i % 2 == 0 ? _brass : _darkMetal, _floorRoot, false).localRotation =
                    Quaternion.Euler(i * 13f, i * 29f, i * 7f);
            }
        }

        private void BuildWatchingEyes(float length)
        {
            foreach (Light light in _corridorLights)
            {
                light.intensity *= 0.18f;
            }
            float z = 2.25f + length - 0.5f;
            Sphere("LeftEye", new Vector3(-0.16f, 1.65f, z), new Vector3(0.055f, 0.035f, 0.025f), _cyanGlow, _floorRoot, false);
            Sphere("RightEye", new Vector3(0.16f, 1.65f, z), new Vector3(0.055f, 0.035f, 0.025f), _cyanGlow, _floorRoot, false);
        }

        private void BuildStairIntoWall(float length)
        {
            for (int i = 0; i < 9; i++)
            {
                float y = 0.12f + i * 0.21f;
                float z = 2.5f + length - 5.6f + i * 0.42f;
                Box("ImpossibleStep", new Vector3(0f, y, z), new Vector3(2.5f, 0.22f, 0.75f), _darkMetal, _floorRoot);
            }
        }

        private void BuildFloodedHall(float length)
        {
            Box("StillWater", new Vector3(0f, 0.13f, 2.3f + length * 0.5f),
                new Vector3(4.1f, 0.035f, length - 0.5f), _glass, _floorRoot, false);
            foreach (Light light in _corridorLights)
            {
                light.color = new Color(0.12f, 0.72f, 0.88f);
            }
        }

        private void BuildChildShadow()
        {
            Box("ChildShadowBody", new Vector3(0.8f, 0.025f, 8.6f), new Vector3(0.45f, 0.02f, 1.25f), _black, _floorRoot, false).localRotation = Quaternion.Euler(0f, 18f, 0f);
            Sphere("ChildShadowHead", new Vector3(0.65f, 0.035f, 7.9f), new Vector3(0.34f, 0.02f, 0.34f), _black, _floorRoot, false);
        }

        private PassengerVisual CreatePassengerModel(PassengerKind kind, Vector3 position, Transform parent,
            bool anomaly, AnomalyTrait trait, int destinationFloor)
        {
            Transform passenger = new GameObject(kind.ToString()).transform;
            passenger.SetParent(parent, false);
            passenger.localPosition = position;
            passenger.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Color bodyColor;
            switch (kind)
            {
                case PassengerKind.Nurse: bodyColor = new Color(0.56f, 0.78f, 0.76f); break;
                case PassengerKind.Courier: bodyColor = new Color(0.82f, 0.34f, 0.08f); break;
                case PassengerKind.OldWoman: bodyColor = new Color(0.28f, 0.18f, 0.31f); break;
                case PassengerKind.MaintenanceWorker: bodyColor = new Color(0.72f, 0.52f, 0.08f); break;
                case PassengerKind.Student: bodyColor = new Color(0.12f, 0.26f, 0.42f); break;
                case PassengerKind.Child: bodyColor = new Color(0.62f, 0.035f, 0.025f); break;
                case PassengerKind.OfficeWorker: bodyColor = new Color(0.16f, 0.18f, 0.2f); break;
                default: bodyColor = new Color(0.045f, 0.045f, 0.055f); break;
            }
            Material clothes = MakeMaterial(kind + " Clothes", bodyColor, 0.03f, 0.22f);
            Material skin = anomaly && trait == AnomalyTrait.NoBreath
                ? _black
                : MakeMaterial("Skin", new Color(0.54f, 0.39f, 0.29f), 0f, 0.18f);

            float heightScale = kind == PassengerKind.Child ? 0.68f :
                kind == PassengerKind.OldWoman ? 0.9f : 1f;
            Transform body = Capsule("Body", new Vector3(0f, 0.9f * heightScale, 0f),
                new Vector3(0.5f, 0.82f * heightScale, 0.38f), clothes, passenger, false);
            Transform head = Sphere("Head", new Vector3(0f, 1.82f * heightScale, 0f),
                new Vector3(0.36f, 0.42f, 0.34f), skin, passenger, false);

            if (anomaly)
            {
                Sphere("LeftEye", new Vector3(-0.12f, 1.87f * heightScale, -0.315f),
                    new Vector3(0.025f, 0.018f, 0.012f), _redGlow, passenger, false);
                Sphere("RightEye", new Vector3(0.12f, 1.87f * heightScale, -0.315f),
                    new Vector3(0.025f, 0.018f, 0.012f), _redGlow, passenger, false);
            }

            AddPassengerProps(kind, passenger, clothes, heightScale);
            AddDestinationTag(passenger, destinationFloor, heightScale);
            return new PassengerVisual
            {
                root = passenger,
                head = head,
                anomaly = anomaly,
                phase = Random.value * 10f
            };
        }

        private void AddPassengerProps(PassengerKind kind, Transform passenger, Material clothes, float heightScale)
        {
            switch (kind)
            {
                case PassengerKind.Nurse:
                    Box("NurseCap", new Vector3(0f, 2.17f * heightScale, 0f),
                        new Vector3(0.42f, 0.08f, 0.38f), _wall, passenger, false);
                    Box("MedicalBag", new Vector3(0.46f, 0.68f, 0f),
                        new Vector3(0.35f, 0.45f, 0.22f), _darkMetal, passenger, false);
                    break;
                case PassengerKind.Courier:
                    Box("Parcel", new Vector3(0f, 0.9f, -0.48f),
                        new Vector3(0.82f, 0.62f, 0.42f), _brass, passenger, false);
                    break;
                case PassengerKind.OldWoman:
                    Cylinder("Cane", new Vector3(0.48f, 0.58f, 0f),
                        new Vector3(0.035f, 0.58f, 0.035f), _brass, passenger, false);
                    break;
                case PassengerKind.MaintenanceWorker:
                    Box("ToolBox", new Vector3(0.52f, 0.48f, 0f),
                        new Vector3(0.52f, 0.35f, 0.28f), _warmGlow, passenger, false);
                    Box("Helmet", new Vector3(0f, 2.14f, 0f),
                        new Vector3(0.47f, 0.12f, 0.42f), _warmGlow, passenger, false);
                    break;
                case PassengerKind.Student:
                    Box("Backpack", new Vector3(0f, 1.03f, 0.36f),
                        new Vector3(0.72f, 0.82f, 0.34f), _trim, passenger, false);
                    break;
                case PassengerKind.Child:
                    Sphere("Ball", new Vector3(0.48f, 0.38f, -0.15f),
                        new Vector3(0.3f, 0.3f, 0.3f), _warmGlow, passenger, false);
                    break;
                case PassengerKind.OfficeWorker:
                    Box("Briefcase", new Vector3(0.52f, 0.45f, 0f),
                        new Vector3(0.58f, 0.42f, 0.18f), _darkMetal, passenger, false);
                    Box("Tie", new Vector3(0f, 1.28f, -0.36f),
                        new Vector3(0.11f, 0.58f, 0.03f), _redGlow, passenger, false);
                    break;
                default:
                    Cylinder("Umbrella", new Vector3(0.52f, 0.68f, 0f),
                        new Vector3(0.035f, 0.72f, 0.035f), _darkMetal, passenger, false);
                    break;
            }
        }

        private void AddDestinationTag(Transform passenger, int destinationFloor, float heightScale)
        {
            Transform tag = Box("DestinationTag", new Vector3(-0.38f, 1.24f * heightScale, -0.36f),
                new Vector3(0.24f, 0.18f, 0.025f), _black, passenger, false);
            GameObject textObject = new GameObject("DestinationDigits");
            textObject.transform.SetParent(tag, false);
            textObject.transform.localPosition = new Vector3(0f, 0f, -0.56f);
            textObject.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 64;
            text.characterSize = 0.12f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(1f, 0.22f, 0.1f);
            text.text = destinationFloor.ToString("00");
            textObject.GetComponent<MeshRenderer>().material = text.font.material;
        }

        private void UpdateOnboardPassengers()
        {
            if (Player == null)
            {
                return;
            }
            foreach (PassengerVisual visual in _onboardPassengers.Values)
            {
                if (visual.root == null || visual.head == null)
                {
                    continue;
                }
                float breathe = Mathf.Sin(Time.time * 0.85f + visual.phase) * 0.006f;
                visual.root.localPosition += new Vector3(0f, breathe * Time.deltaTime, 0f);
                if (visual.anomaly)
                {
                    float yaw = Mathf.Sin(Time.time * 0.42f + visual.phase) * 18f;
                    visual.head.localRotation = Quaternion.Euler(0f, yaw, 0f);
                }
            }
        }

        private void CreateResident(Vector3 position, float yaw, bool faceless)
        {
            Transform resident = new GameObject("Resident").transform;
            resident.SetParent(_floorRoot, false);
            resident.localPosition = position;
            resident.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Capsule("Body", new Vector3(0f, 0.9f, 0f), new Vector3(0.5f, 0.85f, 0.38f), _darkMetal, resident, false);
            Sphere("Head", new Vector3(0f, 1.83f, 0f), new Vector3(0.36f, 0.43f, 0.34f), faceless ? _wall : _brass, resident, false);
        }

        private void ClearFloor()
        {
            _corridorLights.Clear();
            RenderSettings.fogDensity = 0.018f;
            if (_floorRoot == null)
            {
                return;
            }
            for (int i = _floorRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(_floorRoot.GetChild(i).gameObject);
            }
        }

        private void ApplyMood(BuildingMood mood)
        {
            switch (mood)
            {
                case BuildingMood.Amber:
                    RenderSettings.fogColor = new Color(0.055f, 0.033f, 0.012f);
                    _wall.color = new Color(0.62f, 0.48f, 0.32f);
                    break;
                case BuildingMood.Crimson:
                    RenderSettings.fogColor = new Color(0.055f, 0.005f, 0.006f);
                    _wall.color = new Color(0.52f, 0.27f, 0.27f);
                    break;
                default:
                    RenderSettings.fogColor = new Color(0.015f, 0.035f, 0.04f);
                    _wall.color = new Color(0.58f, 0.66f, 0.64f);
                    break;
            }
        }

        private void ApplyDoorPositions()
        {
            if (_leftDoor == null || _rightDoor == null)
            {
                return;
            }
            float offset = Mathf.SmoothStep(0f, 1f, _doorAmount) * 1.43f;
            _leftDoor.localPosition = new Vector3(-0.74f - offset, 1.48f, 2.24f);
            _rightDoor.localPosition = new Vector3(0.74f + offset, 1.48f, 2.24f);
        }

        private void UpdateLights()
        {
            _flickerClock += Time.deltaTime;
            float tension = _survivalGame != null ? _survivalGame.Tension :
                _game != null ? _game.Tension : 0f;
            float noise = Mathf.PerlinNoise(_flickerSeed * 0.071f, _flickerClock * (3.5f + tension * 7f));
            float pulse = noise < 0.11f + tension * 0.08f ? 0.12f : 1f;
            if (_cabinLight != null)
            {
                _cabinLight.intensity = Mathf.Lerp(2.25f, 2.9f, noise) * pulse;
            }
            if (_warningLight != null)
            {
                _warningLight.intensity = 0.6f + Mathf.PingPong(Time.time * 0.75f, 1.6f);
            }
            for (int i = 0; i < _corridorLights.Count; i++)
            {
                Light light = _corridorLights[i];
                if (light == null)
                {
                    continue;
                }
                float localNoise = Mathf.PerlinNoise(i * 0.42f + _flickerSeed, _flickerClock * 2.4f);
                light.enabled = localNoise > 0.035f + tension * 0.025f;
            }
        }

        private void UpdateSurvivalThreats()
        {
            if (_survivalCrawler != null && _survivalCrawler.gameObject.activeSelf)
            {
                _survivalCrawler.localPosition = Vector3.Lerp(new Vector3(0f, 3.02f, -1.18f),
                    new Vector3(0f, 2.78f, 0.28f), _crawlerDanger);
                _survivalCrawler.localRotation = Quaternion.Euler(0f,
                    Mathf.Sin(Time.time * 5.3f) * (4f + _crawlerDanger * 13f),
                    Mathf.Sin(Time.time * 8.1f) * _crawlerDanger * 5f);
                float pulse = 0.9f + Mathf.Sin(Time.time * 9f) * 0.08f * _crawlerDanger;
                _survivalCrawler.localScale = Vector3.one * pulse;
            }
            if (_mirrorDouble != null && _mirrorDouble.gameObject.activeSelf)
            {
                _mirrorDouble.localPosition = new Vector3(
                    Mathf.Sin(Time.time * 1.7f) * 0.025f * _mirrorDanger,
                    Mathf.Sin(Time.time * 0.83f) * 0.015f,
                    -2.16f + _mirrorDanger * 0.12f);
                _mirrorDouble.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.08f, _mirrorDanger);
            }
            if (_fuseSparkLight != null && _fuseSparkLight.enabled)
            {
                float noise = Mathf.PerlinNoise(Time.time * 14f, _fuseDanger * 8f);
                _fuseSparkLight.intensity = noise > 0.4f ? 1.5f + _fuseDanger * 6f : 0f;
            }
        }

        private static void SetIndicator(Renderer indicator, Color color)
        {
            if (indicator == null)
            {
                return;
            }
            Material material = indicator.material;
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 3f);
        }

        private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
        {
            Material material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            return material;
        }

        private static Material MakeEmissive(string name, Color color, float intensity)
        {
            Material material = MakeMaterial(name, color, 0.1f, 0.25f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * intensity);
            return material;
        }

        private static Material MakeTransparent(string name, Color color)
        {
            Material material = MakeMaterial(name, color, 0.05f, 0.75f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }

        private static Transform Box(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider = true)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                Destroy(result.GetComponent<Collider>());
            }
            return result.transform;
        }

        private static Transform Sphere(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                Destroy(result.GetComponent<Collider>());
            }
            return result.transform;
        }

        private static Transform Cylinder(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider = true, Vector3 rotation = default(Vector3))
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.transform.localRotation = Quaternion.Euler(rotation);
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                Destroy(result.GetComponent<Collider>());
            }
            return result.transform;
        }

        private static Transform Capsule(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider)
        {
            GameObject result = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = position;
            result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                Destroy(result.GetComponent<Collider>());
            }
            return result.transform;
        }

        private static Light CreateLight(string name, Vector3 position, Color color, float intensity,
            float range, Transform parent)
        {
            GameObject lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = position;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            return light;
        }
    }
}
