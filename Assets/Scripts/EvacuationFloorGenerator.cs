using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NinetyNine
{
    public enum EvacuationTheme
    {
        Hospital,
        Office,
        Apartment,
        Maintenance,
        Flooded,
        RedHall
    }

    public sealed class EvacuationFloorGenerator : MonoBehaviour
    {
        private readonly List<Light> _floorLights = new List<Light>();
        private readonly List<Light> _cabinLights = new List<Light>();
        private readonly Dictionary<EvacuationAction, Renderer> _controlIndicators =
            new Dictionary<EvacuationAction, Renderer>();
        private readonly Dictionary<EvacuationAction, Material> _controlAtlasMaterials =
            new Dictionary<EvacuationAction, Material>();
        private NinetyNineEvacuationGame _game;
        private EvacuationAudio _audio;
        private Transform _root;
        private Transform _cabin;
        private Transform _floorRoot;
        private Transform _passengerRoot;
        private Transform _leftDoor;
        private Transform _rightDoor;
        private GameObject _barrier;
        private TextMesh _floorDisplay;
        private Light _flashlight;
        private GameObject _parasiteVisual;
        private Material _black;
        private Material _metal;
        private Material _brass;
        private Material _cabinMetal;
        private Material _doorMetal;
        private Material _cabinFloor;
        private Material _hospital;
        private Material _office;
        private Material _apartment;
        private Material _maintenance;
        private Material _flooded;
        private Material _redHall;
        private Material _anomalyDecal;
        private Material _exitSign;
        private Material _elevatorSign;
        private Material _powerSign;
        private Material _surveillanceSign;
        private readonly Dictionary<EvacuationItemKind, Material> _itemAtlasMaterials =
            new Dictionary<EvacuationItemKind, Material>();
        private Material _floor;
        private Material _redGlow;
        private Material _cyanGlow;
        private Material _amberGlow;
        private Material _cabinLampGlow;
        private Material _glass;
        private Material _survivorClothes;
        private Material _mimicClothes;
        private Font _worldFont;
        private float _doorSeal;
        private EvacuationMonster _monster;
        private EvacuationPrimitivePool _primitivePool;
        private EvacuationNavigationGraph _navigationGraph;

        public FirstPersonController Player { get; private set; }
        public Transform Cabin => _cabin;
        public Transform PassengerRoot => _passengerRoot;
        public EvacuationMonster Monster => _monster;
        public int PooledPrimitiveCount => _primitivePool != null ? _primitivePool.AvailableCount : 0;
        public int CreatedPrimitiveCount => _primitivePool != null ? _primitivePool.TotalCreated : 0;
        public int CurrentFloorInstanceId => _floorRoot != null ? _floorRoot.gameObject.GetInstanceID() : 0;
        public int CurrentPickupCount
        {
            get
            {
                if (_floorRoot == null) return 0;
                EvacuationInteractable[] values = _floorRoot.GetComponentsInChildren<EvacuationInteractable>(true);
                int count = 0;
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i].Action == EvacuationAction.Item) count++;
                }
                return count;
            }
        }

        public void Initialize(NinetyNineEvacuationGame game, EvacuationAudio audio)
        {
            _game = game;
            _audio = audio;
            ConfigureAtmosphere();
            CreateMaterials();
            _root = new GameObject("EvacuationWorld").transform;
            _root.SetParent(transform, false);
            _primitivePool = new EvacuationPrimitivePool(_root);
            BuildCabin();
            BuildPlayer();
            _audio.Initialize(Player);
            BuildFloor(game.RunSeed, 99);
            SetDoorSeal(0f);
        }

        public void BuildFloor(int runSeed, int floorNumber)
        {
            EvacuationFloorDirector fallbackDirector = new EvacuationFloorDirector();
            BuildFloor(fallbackDirector.CreatePlan(runSeed, floorNumber, _game.Power, 0));
        }

        public void BuildFloor(EvacuationFloorPlan plan)
        {
            ClearFloor();
            System.Random random = new System.Random(plan.Seed);
            int floorNumber = plan.FloorNumber;
            bool isStartingFloor = plan.IsStartingFloor;
            bool isExitFloor = plan.IsExitFloor;
            EvacuationTheme theme = plan.Theme;
            bool blackout = plan.Blackout;
            bool distorted = plan.Distorted;
            int length = plan.Length;

            _floorRoot = new GameObject("Floor_" + floorNumber + "_" + theme).transform;
            _floorRoot.SetParent(_root, false);
            _floorRoot.gameObject.AddComponent<EvacuationVfx>().Configure(theme, plan.Event, length);
            Material wallMaterial = GetThemeMaterial(theme);
            Color lightColor = GetThemeLight(theme);
            float ceilingHeight = distorted ? 4.3f : 3.15f;

            List<Vector2Int> mainPath = new List<Vector2Int>();
            HashSet<Vector2Int> cells = new HashSet<Vector2Int>();
            Vector2Int cursor = Vector2Int.zero;
            cells.Add(cursor);
            mainPath.Add(cursor);
            for (int i = 1; i < length; i++)
            {
                Vector2Int next;
                if (i % 3 == 0 && random.NextDouble() < 0.66)
                {
                    int side = random.NextDouble() < 0.5 ? -1 : 1;
                    next = cursor + new Vector2Int(side, 0);
                    if (cells.Contains(next))
                    {
                        next = cursor + Vector2Int.up;
                    }
                }
                else
                {
                    next = cursor + Vector2Int.up;
                }
                cursor = next;
                cells.Add(cursor);
                mainPath.Add(cursor);
            }
            for (int i = 2; i < mainPath.Count - 2; i++)
            {
                if (random.NextDouble() < 0.38)
                {
                    Vector2Int branch = mainPath[i] + new Vector2Int(random.NextDouble() < 0.5 ? -1 : 1, 0);
                    cells.Add(branch);
                    if (random.NextDouble() < 0.45)
                    {
                        cells.Add(branch + Vector2Int.up);
                    }
                }
            }

            List<Vector2Int> explorationRooms = new List<Vector2Int>();
            if (!isStartingFloor && !isExitFloor)
            {
                int roomCount = random.Next(2, 5);
                int attempts = roomCount * 4;
                while (explorationRooms.Count < roomCount && attempts-- > 0)
                {
                    Vector2Int anchor = mainPath[random.Next(2, mainPath.Count - 1)];
                    Vector2Int roomCenter;
                    if (TryAddExplorationRoom(anchor, cells, random, out roomCenter))
                    {
                        explorationRooms.Add(roomCenter);
                    }
                }
            }
            int monsterBypassIndex = -1;
            if (plan.SpawnMonster)
            {
                monsterBypassIndex = AddMonsterBypass(mainPath, cells, random);
            }
            _navigationGraph = new EvacuationNavigationGraph(cells);

            Vector2Int? lockerCell = plan.SpawnMonster && explorationRooms.Count > 0
                ? explorationRooms[0]
                : (Vector2Int?)null;

            foreach (Vector2Int cell in cells)
            {
                bool allowThemeProp = !lockerCell.HasValue || cell != lockerCell.Value;
                BuildCell(cell, cells, wallMaterial, theme, ceilingHeight, blackout, lightColor, random,
                    allowThemeProp);
            }
            if (lockerCell.HasValue)
            {
                CreateEmergencyLocker(CellPosition(lockerCell.Value));
            }

            if (isStartingFloor)
            {
                EvacuationItemKind starterItem = random.NextDouble() < 0.5
                    ? EvacuationItemKind.FlashBattery
                    : EvacuationItemKind.Stimulant;
                Vector3 starterPosition = CellPosition(mainPath[Mathf.Min(1, mainPath.Count - 1)]);
                CreatePickup(starterItem, starterPosition + new Vector3(0f, 0.34f, 0.25f));
                CreateNpc(CellPosition(mainPath[2]), false, floorNumber - random.Next(5, 10));
                CreateLight("StartFloorGuide", starterPosition + new Vector3(0f, 2.45f, 0f),
                    new Color(0.95f, 0.66f, 0.38f), 3.4f, 8f, _floorRoot).shadows = LightShadows.Soft;
            }
            else if (!isExitFloor)
            {
                bool criticalPower = _game.Power < 5f;
                bool lowPower = _game.Power < 11f;
                Vector2Int primaryCell = plan.SpawnMonster
                    ? mainPath[Mathf.Max(2, mainPath.Count - 3)]
                    : mainPath[mainPath.Count - 1];
                if (lowPower)
                {
                    Vector2Int recoveryCell = criticalPower
                        ? mainPath[Mathf.Min(1, mainPath.Count - 1)]
                        : mainPath[Mathf.Max(2, mainPath.Count / 2)];
                    CreatePickup(EvacuationItemKind.EmergencyCell,
                        CellPosition(recoveryCell) + new Vector3(0f, 0.34f, -0.35f));
                    EvacuationItemKind deepReward = random.NextDouble() < 0.48
                        ? EvacuationItemKind.PowerCell : RandomSmallItem(random);
                    CreatePickup(deepReward,
                        CellPosition(primaryCell) + new Vector3(0f, 0.42f, 0f));
                }
                else
                {
                    EvacuationItemKind primary = random.NextDouble() < 0.4
                        ? EvacuationItemKind.PowerCell : RandomSmallItem(random);
                    CreatePickup(primary, CellPosition(primaryCell) + new Vector3(0f, 0.42f, 0f));
                }
                if (random.NextDouble() < 0.72)
                {
                    Vector2Int bonusCell = mainPath[random.Next(2, mainPath.Count - 1)];
                    CreatePickup(RandomSmallItem(random), CellPosition(bonusCell) + new Vector3(0.7f, 0.34f, 0.35f));
                }
                int roomLootCount = Mathf.Min(explorationRooms.Count, random.Next(1, 3));
                for (int i = 0; i < roomLootCount; i++)
                {
                    Vector2Int roomCell = explorationRooms[explorationRooms.Count - 1 - i];
                    CreatePickup(RandomSmallItem(random), CellPosition(roomCell) +
                        new Vector3(-0.62f, 0.34f, 0.25f));
                }

                if (plan.SpawnNpc)
                {
                    Vector2Int npcCell = mainPath[Mathf.Max(2, mainPath.Count / 2)];
                    bool mimic = random.NextDouble() < 0.32;
                    int destination = Mathf.Max(1, floorNumber - random.Next(7, 19));
                    CreateNpc(CellPosition(npcCell), mimic, destination);
                }

                if (plan.SpawnEvidence)
                {
                    Vector2Int evidenceCell = mainPath[Mathf.Max(2, mainPath.Count - 2)];
                    CreateEvidence(CellPosition(evidenceCell) + new Vector3(-0.62f, 0.28f, 0.35f),
                        floorNumber, plan.Event);
                }

                if (_game.NeedsDoorFuse)
                {
                    Vector2Int fuseCell = mainPath[Mathf.Min(1, mainPath.Count - 1)];
                    CreatePickup(EvacuationItemKind.Fuse,
                        CellPosition(fuseCell) + new Vector3(0.68f, 0.3f, -0.38f));
                }

                if (plan.SpawnMonster)
                {
                    Vector2Int monsterCell = mainPath[mainPath.Count - 1];
                    CreateMonster(CellPosition(monsterCell) + new Vector3(0f, 0f, 0.6f),
                        plan.Event == FloorEventKind.ChasedSurvivor
                            ? 0.8f : 4f + (float)random.NextDouble() * 3f,
                        plan.Monster, mainPath);
                }
            }

            ApplyFloorEvent(plan, mainPath, random, monsterBypassIndex);
            Vector3 landmarkPosition = explorationRooms.Count > 0
                ? CellPosition(explorationRooms[explorationRooms.Count - 1])
                : CellPosition(mainPath[mainPath.Count - 1]);
            if (!isStartingFloor) CreateThemeLandmark(theme, landmarkPosition, random);
            _floorRoot.gameObject.AddComponent<EvacuationFloorAnomaly>().Configure(plan.Event,
                _game, Player);

            if (theme == EvacuationTheme.Flooded || plan.Event == FloorEventKind.RisingWater)
            {
                _game.SetFloorMovementPenalty(0.82f);
            }
            else
            {
                _game.SetFloorMovementPenalty(1f);
            }
            _audio.SetFloorMood((int)theme);
            RenderSettings.fogDensity = blackout ? 0.047f : distorted ? 0.034f : 0.022f;
            RenderSettings.fogColor = theme == EvacuationTheme.RedHall
                ? new Color(0.065f, 0.004f, 0.004f)
                : new Color(0.009f, 0.022f, 0.024f);
            SetBarrier(false);
            SetDoorSeal(0f);
            _game.NotifyFloorPlan(plan);
        }

        public void BeginTravel()
        {
            SetBarrier(true);
            SetDoorSeal(1f);
            if (_floorRoot != null)
            {
                _floorRoot.gameObject.SetActive(false);
            }
        }

        public void ResumeFloor()
        {
            if (_floorRoot != null)
            {
                _floorRoot.gameObject.SetActive(true);
            }
        }

        public void SetDoorSeal(float seal)
        {
            _doorSeal = Mathf.Clamp01(seal);
            float offset = (1f - _doorSeal) * 1.42f;
            if (_leftDoor != null)
            {
                _leftDoor.localPosition = new Vector3(-0.74f - offset, 1.48f, 2.24f);
                _rightDoor.localPosition = new Vector3(0.74f + offset, 1.48f, 2.24f);
            }
            if (_barrier != null && !_game.IsDescending)
            {
                _barrier.SetActive(_doorSeal > 0.72f);
            }
        }

        public void SetBarrier(bool blocked)
        {
            if (_barrier != null)
            {
                _barrier.SetActive(blocked);
            }
        }

        public void SetFlashlight(bool enabled, float charge)
        {
            if (_flashlight == null)
            {
                return;
            }
            bool flicker = charge < 12f && Mathf.PerlinNoise(Time.time * 12f, charge) < 0.32f;
            _flashlight.enabled = enabled && !flicker;
            _flashlight.intensity = Mathf.Lerp(2.7f, 5.2f, Mathf.Clamp01(charge / 35f));
        }

        public void SetCabinLighting(float power01, bool lowPower, bool criticalPower, bool threatFlicker)
        {
            float energy = Mathf.Clamp01(power01);
            float flickerNoise = Mathf.PerlinNoise(Time.time * (threatFlicker ? 15f : 9f), 0.417f);
            bool flickerOff = threatFlicker
                ? flickerNoise < 0.48f
                : criticalPower ? flickerNoise < 0.34f : lowPower && flickerNoise < 0.1f;
            float intensity = Mathf.Lerp(1.4f, 6.2f, Mathf.SmoothStep(0f, 1f, energy));
            if (lowPower) intensity *= criticalPower ? 0.7f : 0.82f;
            if (flickerOff) intensity *= 0.18f;
            Color lightColor = Color.Lerp(new Color(1f, 0.42f, 0.12f),
                new Color(1f, 0.78f, 0.52f), Mathf.Clamp01(energy * 2f));

            for (int i = 0; i < _cabinLights.Count; i++)
            {
                Light cabinLight = _cabinLights[i];
                if (cabinLight == null) continue;
                cabinLight.enabled = energy > 0.001f;
                cabinLight.intensity = intensity;
                cabinLight.color = lightColor;
            }
            if (_cabinLampGlow != null)
            {
                Color panelColor = flickerOff ? new Color(0.025f, 0.012f, 0.004f) : lightColor;
                _cabinLampGlow.color = panelColor;
                _cabinLampGlow.EnableKeyword("_EMISSION");
                _cabinLampGlow.SetColor("_EmissionColor", panelColor * (criticalPower ? 1.25f : 2.8f));
            }
        }

        public void SetFloorDisplay(int floor)
        {
            if (_floorDisplay != null) _floorDisplay.text = Mathf.Clamp(floor, 1, 99).ToString("00");
        }

        public void SetControlState(EvacuationAction action, bool active, bool danger)
        {
            Renderer renderer;
            if (!_controlIndicators.TryGetValue(action, out renderer) || renderer == null)
            {
                return;
            }
            Color activeColor = action == EvacuationAction.Stop
                ? new Color(1f, 0.06f, 0.025f)
                : action == EvacuationAction.Door || action == EvacuationAction.FusePanel
                    ? new Color(1f, 0.45f, 0.055f)
                    : new Color(0.06f, 1f, 0.65f);
            Color color = danger ? new Color(1f, 0.03f, 0.01f) :
                active ? activeColor : activeColor * 0.16f;
            renderer.sharedMaterial.color = color;
            renderer.sharedMaterial.EnableKeyword("_EMISSION");
            renderer.sharedMaterial.SetColor("_EmissionColor", color * 3.4f);
        }

        public void SetParasiteActive(bool active)
        {
            if (_parasiteVisual != null) _parasiteVisual.SetActive(active);
        }

        public void ReleaseDynamicObject(GameObject target)
        {
            if (target == null) return;
            if (_audio != null) _audio.DetachObject(target);
            if (_primitivePool != null) _primitivePool.ReleaseHierarchy(target.transform);
            Destroy(target);
        }

        public void RemoveMonster(EvacuationMonster monster)
        {
            if (_monster == monster)
            {
                _monster = null;
            }
            if (monster != null)
            {
                ReleaseDynamicObject(monster.gameObject);
            }
        }

        public void NotifyFloorLooted()
        {
            if (_floorRoot == null) return;
            EvacuationFloorAnomaly anomaly = _floorRoot.GetComponent<EvacuationFloorAnomaly>();
            if (anomaly != null) anomaly.TriggerLootReaction();
        }

        private void ConfigureAtmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.045f, 0.06f, 0.065f);
            RenderSettings.ambientEquatorColor = new Color(0.018f, 0.028f, 0.03f);
            RenderSettings.ambientGroundColor = new Color(0.004f, 0.005f, 0.006f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
        }

        private void CreateMaterials()
        {
            _black = MakeMaterial("Lightless", new Color(0.001f, 0.001f, 0.001f), 0f, 0f);
            _metal = MakeMaterial("Blackened Steel", new Color(0.025f, 0.035f, 0.038f), 0.82f, 0.3f);
            _brass = MakeTextured("Aged Brass", "Art/brass_tile", new Color(0.38f, 0.3f, 0.12f), 0.65f, 0.32f);
            _cabinMetal = MakeTextured("Brushed Cabin Steel", "Art/elevator_brushed_steel",
                new Color(0.78f, 0.82f, 0.84f), 0.94f, 0.66f);
            _doorMetal = MakeTextured("Brushed Door Steel", "Art/elevator_brushed_steel",
                new Color(0.96f, 0.98f, 1f), 0.97f, 0.76f);
            _cabinFloor = MakeMaterial("Dark Elevator Floor", new Color(0.028f, 0.035f, 0.038f), 0.52f, 0.34f);
            _hospital = MakeAtlasMaterial("Hospital Tile", 0, 3, 0.05f, 0.18f);
            _office = MakeAtlasMaterial("Office Acoustic Wall", 1, 3, 0.02f, 0.09f);
            _apartment = MakeAtlasMaterial("Apartment Wallpaper", 2, 3, 0.01f, 0.07f);
            _maintenance = MakeAtlasMaterial("Maintenance Metal", 3, 3, 0.58f, 0.2f);
            _flooded = MakeAtlasMaterial("Flooded Concrete", 0, 2, 0.03f, 0.34f);
            _redHall = MakeAtlasMaterial("Impossible Red Plaster", 1, 2, 0.01f, 0.12f);
            _anomalyDecal = MakeAtlasDecal("Anomaly Decal", 0, 3);
            _exitSign = MakeGeneratedAtlasMaterial("False Exit Sign", "Art/building_signage_atlas_v2", 4);
            _elevatorSign = MakeGeneratedAtlasMaterial("Elevator Safety Sign", "Art/building_signage_atlas_v2", 1);
            _powerSign = MakeGeneratedAtlasMaterial("Electrical Hazard Sign", "Art/building_signage_atlas_v2", 2);
            _surveillanceSign = MakeGeneratedAtlasMaterial("Surveillance Sign", "Art/building_signage_atlas_v2", 3);
            _floor = MakeMaterial("Wet Linoleum", new Color(0.045f, 0.075f, 0.073f), 0.08f, 0.4f);
            _redGlow = MakeEmissive("Emergency Red", new Color(0.88f, 0.018f, 0.008f), 5f);
            _cyanGlow = MakeEmissive("Cold Cyan", new Color(0.08f, 0.92f, 0.78f), 3.4f);
            _amberGlow = MakeEmissive("Sodium Amber", new Color(1f, 0.42f, 0.055f), 3.2f);
            _cabinLampGlow = MakeEmissive("Warm Cabin Lamp", new Color(1f, 0.78f, 0.52f), 3.2f);
            _glass = MakeTransparent("Flood Water", new Color(0.025f, 0.22f, 0.25f, 0.34f));
            _survivorClothes = MakeMaterial("Survivor Coat", new Color(0.12f, 0.23f, 0.24f),
                0.03f, 0.16f);
            _mimicClothes = MakeMaterial("Mimic Coat", new Color(0.08f, 0.075f, 0.09f),
                0.03f, 0.16f);
            _worldFont = Font.CreateDynamicFontFromOSFont(new[]
            {
                "Microsoft YaHei", "Microsoft YaHei UI", "PingFang SC", "Heiti SC", "SimHei", "Arial"
            }, 80);
            if (_worldFont != null)
            {
                _worldFont.RequestCharactersInTexture("下降停止门控电池槽保险丝", 80, FontStyle.Bold);
            }
        }

        private void BuildCabin()
        {
            _cabin = new GameObject("EvacuationCabin").transform;
            _cabin.SetParent(_root, false);
            _passengerRoot = new GameObject("Passengers").transform;
            _passengerRoot.SetParent(_cabin, false);
            Box("CabinFloor", new Vector3(0f, -0.08f, 0f), new Vector3(4.7f, 0.16f, 4.7f), _cabinFloor, _cabin);
            Box("CabinCeiling", new Vector3(0f, 3.2f, 0f), new Vector3(4.7f, 0.12f, 4.7f), _metal, _cabin);
            Box("BackWall", new Vector3(0f, 1.55f, -2.3f), new Vector3(4.7f, 3.2f, 0.12f), _cabinMetal, _cabin);
            Box("ElevatorSafetyPlacard", new Vector3(0f, 1.72f, -2.225f),
                new Vector3(0.76f, 1.12f, 0.018f), _elevatorSign, _cabin, false);
            Box("LeftWall", new Vector3(-2.3f, 1.55f, 0f), new Vector3(0.12f, 3.2f, 4.7f), _cabinMetal, _cabin);
            Box("RightWall", new Vector3(2.3f, 1.55f, 0f), new Vector3(0.12f, 3.2f, 4.7f), _cabinMetal, _cabin);
            Box("FrontLeft", new Vector3(-1.86f, 1.55f, 2.28f), new Vector3(0.86f, 3.2f, 0.15f), _metal, _cabin);
            Box("FrontRight", new Vector3(1.86f, 1.55f, 2.28f), new Vector3(0.86f, 3.2f, 0.15f), _metal, _cabin);
            Box("Header", new Vector3(0f, 2.98f, 2.28f), new Vector3(2.9f, 0.36f, 0.15f), _metal, _cabin);
            _leftDoor = Box("LeftDoor", new Vector3(-0.74f, 1.48f, 2.24f), new Vector3(1.46f, 2.78f, 0.11f), _doorMetal, _cabin);
            _rightDoor = Box("RightDoor", new Vector3(0.74f, 1.48f, 2.24f), new Vector3(1.46f, 2.78f, 0.11f), _doorMetal, _cabin);

            Box("BackHandrail", new Vector3(0f, 1.02f, -2.18f),
                new Vector3(3.72f, 0.07f, 0.08f), _brass, _cabin, false);
            Box("LeftHandrail", new Vector3(-2.18f, 1.02f, -0.35f),
                new Vector3(0.08f, 0.07f, 3.4f), _brass, _cabin, false);
            CreateCabinLamp("RearCabinLamp", -1.05f);
            CreateCabinLamp("FrontCabinLamp", 1.02f);
            CreateControlConsole();
            CreateCabinParasite();

            CreateDoorHeaderDisplay();
            _barrier = new GameObject("TravelBarrier");
            _barrier.transform.SetParent(_root, false);
            _barrier.transform.position = new Vector3(0f, 1.5f, 2.36f);
            BoxCollider barrierCollider = _barrier.AddComponent<BoxCollider>();
            barrierCollider.size = new Vector3(3.1f, 3f, 0.14f);
        }

        private void CreateCabinLamp(string name, float z)
        {
            Box(name + "Housing", new Vector3(0f, 3.105f, z),
                new Vector3(2.7f, 0.07f, 0.72f), _metal, _cabin, false);
            Box(name + "Diffuser", new Vector3(0f, 3.06f, z),
                new Vector3(2.42f, 0.025f, 0.54f), _cabinLampGlow, _cabin, false);
            Light light = CreateLight(name + "Light", new Vector3(0f, 2.72f, z),
                new Color(1f, 0.78f, 0.52f), 4.8f, 5.2f, _cabin);
            light.shadows = _cabinLights.Count == 0 ? LightShadows.Soft : LightShadows.None;
            _cabinLights.Add(light);
        }

        private void CreateControlConsole()
        {
            Box("ControlConsoleBack", new Vector3(2.225f, 1.4f, 0.72f),
                new Vector3(0.09f, 2.55f, 1.08f), _metal, _cabin, false);
            Box("ControlConsoleTop", new Vector3(2.16f, 2.68f, 0.72f),
                new Vector3(0.08f, 0.035f, 1.08f), _brass, _cabin, false);
            Box("ControlConsoleEdge", new Vector3(2.16f, 1.4f, 0.18f),
                new Vector3(0.08f, 2.55f, 0.035f), _brass, _cabin, false);
            CreateControl("下降", "启动下降（必须先关门）", EvacuationAction.Descend,
                new Vector3(2.16f, 2.34f, 0.72f), _cyanGlow);
            CreateControl("停止", "立即停车", EvacuationAction.Stop,
                new Vector3(2.16f, 1.86f, 0.72f), _redGlow);
            CreateControl("门控", "开 / 关电梯门", EvacuationAction.Door,
                new Vector3(2.16f, 1.38f, 0.72f), _amberGlow);
            CreateControl("电池槽", "安装电池", EvacuationAction.BatterySlot,
                new Vector3(2.16f, 0.9f, 0.72f), _cyanGlow);
            CreateControl("保险丝", "安装保险丝", EvacuationAction.FusePanel,
                new Vector3(2.16f, 0.42f, 0.72f), _amberGlow);
        }

        private void CreateCabinParasite()
        {
            Transform root = new GameObject("ElevatorParasite").transform;
            root.SetParent(_cabin, false);
            root.localPosition = new Vector3(2.12f, 0.95f, -0.2f);
            root.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box("CableMass", Vector3.zero, new Vector3(0.48f, 0.72f, 0.08f), _black, root, false);
            for (int i = -2; i <= 2; i++)
            {
                Cylinder("Tendril", new Vector3(i * 0.08f, -0.14f, -0.08f),
                    new Vector3(0.018f, 0.3f + Mathf.Abs(i) * 0.04f, 0.018f), _redGlow,
                    root, false, new Vector3(0f, 0f, i * 9f));
            }
            BoxCollider hitbox = root.gameObject.AddComponent<BoxCollider>();
            hitbox.size = new Vector3(0.62f, 0.86f, 0.2f);
            hitbox.isTrigger = true;
            root.gameObject.AddComponent<EvacuationInteractable>().Configure(
                EvacuationAction.ElevatorParasite, "扯下吸附在电池线路上的寄生物");
            _parasiteVisual = root.gameObject;
            _parasiteVisual.SetActive(false);
        }

        private void CreateDoorHeaderDisplay()
        {
            Transform displayRoot = new GameObject("DoorHeaderDisplay").transform;
            displayRoot.SetParent(_cabin, false);
            displayRoot.localPosition = new Vector3(0f, 2.98f, 2.19f);
            Box("DisplayBezel", Vector3.zero, new Vector3(1.34f, 0.38f, 0.07f),
                _metal, displayRoot, false);
            Box("DisplayGlass", new Vector3(0f, 0f, -0.045f), new Vector3(1.14f, 0.26f, 0.025f),
                _black, displayRoot, false);
            _floorDisplay = CreateText("Floor", displayRoot, "99", 0.035f,
                new Color(1f, 0.055f, 0.018f), new Vector3(0f, 0f, -0.068f));
            _floorDisplay.fontStyle = FontStyle.Bold;
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
            GameObject cameraObject = new GameObject("PlayerCamera");
            cameraObject.transform.SetParent(playerObject.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70f;
            camera.nearClipPlane = 0.035f;
            camera.farClipPlane = 90f;
            camera.backgroundColor = new Color(0.001f, 0.003f, 0.004f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.allowHDR = true;
            cameraObject.AddComponent<AudioListener>();
            AnalogPostEffect post = cameraObject.AddComponent<AnalogPostEffect>();
            post.EvacuationGame = _game;
            Player = playerObject.AddComponent<FirstPersonController>();
            Player.Initialize(camera);
            Player.UseStamina = true;

            _flashlight = CreateLight("Hand Flashlight", Vector3.zero, new Color(0.68f, 0.86f, 1f),
                4.8f, 18f, cameraObject.transform);
            _flashlight.type = LightType.Spot;
            _flashlight.spotAngle = 43f;
            _flashlight.innerSpotAngle = 24f;
            _flashlight.shadows = LightShadows.Soft;
            _flashlight.enabled = false;
        }

        private void BuildCell(Vector2Int cell, HashSet<Vector2Int> cells, Material wallMaterial,
            EvacuationTheme theme, float ceilingHeight, bool blackout, Color lightColor,
            System.Random random, bool allowThemeProp)
        {
            Vector3 center = CellPosition(cell);
            Box("Floor", center + new Vector3(0f, -0.08f, 0f), new Vector3(3f, 0.14f, 3f), _floor, _floorRoot);
            Box("Ceiling", center + new Vector3(0f, ceilingHeight, 0f), new Vector3(3f, 0.1f, 3f), wallMaterial, _floorRoot, false);
            if (!cells.Contains(cell + Vector2Int.left))
                Box("WallL", center + new Vector3(-1.48f, ceilingHeight * 0.5f, 0f), new Vector3(0.12f, ceilingHeight, 3f), wallMaterial, _floorRoot);
            if (!cells.Contains(cell + Vector2Int.right))
                Box("WallR", center + new Vector3(1.48f, ceilingHeight * 0.5f, 0f), new Vector3(0.12f, ceilingHeight, 3f), wallMaterial, _floorRoot);
            if (!cells.Contains(cell + Vector2Int.up))
                Box("WallF", center + new Vector3(0f, ceilingHeight * 0.5f, 1.48f), new Vector3(3f, ceilingHeight, 0.12f), wallMaterial, _floorRoot);
            if (cell != Vector2Int.zero && !cells.Contains(cell + Vector2Int.down))
                Box("WallB", center + new Vector3(0f, ceilingHeight * 0.5f, -1.48f), new Vector3(3f, ceilingHeight, 0.12f), wallMaterial, _floorRoot);

            if (!blackout && random.NextDouble() < 0.62)
            {
                Box("Fixture", center + new Vector3(0f, ceilingHeight - 0.1f, 0f),
                    new Vector3(0.65f, 0.05f, 0.2f), theme == EvacuationTheme.RedHall ? _redGlow : _cyanGlow,
                    _floorRoot, false);
                if (_floorLights.Count < 12 && random.NextDouble() < 0.48)
                {
                    Light light = CreateLight("FloorLight", center +
                        new Vector3(0f, ceilingHeight - 0.38f, 0f), lightColor,
                        theme == EvacuationTheme.RedHall ? 2.8f : 2.2f, 5.5f, _floorRoot);
                    light.shadows = _floorLights.Count < 3 ? LightShadows.Soft : LightShadows.None;
                    _floorLights.Add(light);
                }
            }
            if (allowThemeProp)
            {
                AddThemeProp(theme, center, random);
            }
            AddArchitecturalDetail(theme, center, random);
            if (theme == EvacuationTheme.Flooded)
            {
                Box("Water", center + new Vector3(0f, 0.1f, 0f), new Vector3(2.85f, 0.035f, 2.85f), _glass, _floorRoot, false);
            }
        }

        private void AddArchitecturalDetail(EvacuationTheme theme, Vector3 center, System.Random random)
        {
            switch (theme)
            {
                case EvacuationTheme.Hospital:
                    Box("HospitalRailL", center + new Vector3(-1.37f, 1.02f, 0f),
                        new Vector3(0.08f, 0.09f, 2.45f), _maintenance, _floorRoot, false);
                    Box("HospitalRailR", center + new Vector3(1.37f, 1.02f, 0f),
                        new Vector3(0.08f, 0.09f, 2.45f), _maintenance, _floorRoot, false);
                    break;
                case EvacuationTheme.Office:
                    Box("CeilingGrid", center + new Vector3(0f, 3.03f, 0f),
                        new Vector3(2.55f, 0.04f, 0.055f), _maintenance, _floorRoot, false);
                    break;
                case EvacuationTheme.Apartment:
                    Box("SkirtingL", center + new Vector3(-1.38f, 0.15f, 0f),
                        new Vector3(0.07f, 0.3f, 2.6f), _redHall, _floorRoot, false);
                    Box("SkirtingR", center + new Vector3(1.38f, 0.15f, 0f),
                        new Vector3(0.07f, 0.3f, 2.6f), _redHall, _floorRoot, false);
                    break;
                case EvacuationTheme.Maintenance:
                    Cylinder("OverheadPipe", center + new Vector3(-0.82f, 2.72f, 0f),
                        new Vector3(0.08f, 1.35f, 0.08f), _maintenance, _floorRoot, false,
                        new Vector3(90f, 0f, 0f));
                    Cylinder("OverheadPipe", center + new Vector3(0.82f, 2.65f, 0f),
                        new Vector3(0.055f, 1.35f, 0.055f), _brass, _floorRoot, false,
                        new Vector3(90f, 0f, 0f));
                    break;
                case EvacuationTheme.Flooded:
                    Box("Waterline", center + new Vector3(1.39f, 0.55f, 0f),
                        new Vector3(0.04f, 0.18f, 2.55f), _flooded, _floorRoot, false);
                    break;
                case EvacuationTheme.RedHall:
                    if (random.NextDouble() < 0.7)
                    {
                        Box("ImpossibleRib", center + new Vector3(0f, 1.55f, 1.38f),
                            new Vector3(0.12f, 3f, 0.14f), _black, _floorRoot, false);
                    }
                    break;
            }
        }

        private void AddThemeProp(EvacuationTheme theme, Vector3 center, System.Random random)
        {
            if (random.NextDouble() > 0.58)
            {
                return;
            }
            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            switch (theme)
            {
                case EvacuationTheme.Hospital:
                    Transform bed = Box("HospitalBed", center + new Vector3(side * 0.92f, 0.42f, 0.3f),
                        new Vector3(0.85f, 0.18f, 1.55f), _hospital, _floorRoot);
                    Box("BedFrame", center + new Vector3(side * 0.92f, 0.25f, 0.3f),
                        new Vector3(0.92f, 0.08f, 1.7f), _metal, _floorRoot);
                    ConfigureHidingSpot(bed, "躲到病床下面", new Vector3(0f, -1.62f, 0f),
                        new Vector3(-side * 1.1f, -0.34f, 0f));
                    break;
                case EvacuationTheme.Office:
                    Box("Desk", center + new Vector3(side * 0.92f, 0.55f, 0.45f),
                        new Vector3(1.05f, 0.08f, 0.72f), _office, _floorRoot);
                    Box("Monitor", center + new Vector3(side * 0.92f, 0.9f, 0.48f),
                        new Vector3(0.48f, 0.38f, 0.08f), _black, _floorRoot, false);
                    break;
                case EvacuationTheme.Apartment:
                    Transform wardrobe = Box("Wardrobe", center + new Vector3(side * 1.04f, 0.9f, 0.55f),
                        new Vector3(0.7f, 1.8f, 0.75f), _apartment, _floorRoot);
                    ConfigureHidingSpot(wardrobe, "躲进衣柜", new Vector3(0f, -1f, 0f),
                        new Vector3(-side * 1.2f, -0.82f, 0f));
                    break;
                case EvacuationTheme.Maintenance:
                    Cylinder("Pipe", center + new Vector3(side * 1.1f, 1.2f, 0.55f),
                        new Vector3(0.09f, 1.2f, 0.09f), _maintenance, _floorRoot, false);
                    Box("ToolCrate", center + new Vector3(-side * 0.85f, 0.28f, -0.65f),
                        new Vector3(0.75f, 0.52f, 0.65f), _maintenance, _floorRoot);
                    break;
                case EvacuationTheme.RedHall:
                    CreateMannequin(center + new Vector3(side * 0.9f, 0f, 0.4f));
                    break;
            }
        }

        private void CreateThemeLandmark(EvacuationTheme theme, Vector3 center, System.Random random)
        {
            float side = random.NextDouble() < 0.5 ? -1f : 1f;
            switch (theme)
            {
                case EvacuationTheme.Hospital:
                    Box("WardCurtainRail", center + new Vector3(0f, 2.25f, 0.55f),
                        new Vector3(2.4f, 0.05f, 0.05f), _maintenance, _floorRoot, false);
                    Box("WardScreen", center + new Vector3(side * 0.9f, 1.1f, 0.55f),
                        new Vector3(0.06f, 1.9f, 1.45f), _hospital, _floorRoot, false);
                    break;
                case EvacuationTheme.Office:
                    for (int i = -1; i <= 1; i++)
                    {
                        Box("ServerRack", center + new Vector3(side * 1.08f, 0.9f, i * 0.68f),
                            new Vector3(0.48f, 1.8f, 0.54f), _maintenance, _floorRoot, true);
                        Box("ServerStatus", center + new Vector3(side * 0.82f, 0.92f, i * 0.68f),
                            new Vector3(0.02f, 0.65f, 0.32f), _cyanGlow, _floorRoot, false);
                    }
                    break;
                case EvacuationTheme.Apartment:
                    Box("DiningTable", center + new Vector3(0f, 0.65f, 0.45f),
                        new Vector3(1.55f, 0.1f, 0.9f), _brass, _floorRoot, true);
                    Box("AbandonedPlaceSetting", center + new Vector3(0.35f, 0.73f, 0.45f),
                        new Vector3(0.28f, 0.025f, 0.28f), _hospital, _floorRoot, false);
                    break;
                case EvacuationTheme.Maintenance:
                    Box("BackupGenerator", center + new Vector3(side * 0.88f, 0.7f, 0.35f),
                        new Vector3(1.05f, 1.4f, 0.82f), _maintenance, _floorRoot, true);
                    Cylinder("GeneratorCoil", center + new Vector3(side * 0.88f, 0.72f, -0.09f),
                        new Vector3(0.32f, 0.2f, 0.32f), _brass, _floorRoot, false,
                        new Vector3(90f, 0f, 0f));
                    break;
                case EvacuationTheme.Flooded:
                    Cylinder("PumpHousing", center + new Vector3(side * 0.9f, 0.6f, 0.35f),
                        new Vector3(0.48f, 0.68f, 0.48f), _maintenance, _floorRoot, true,
                        new Vector3(0f, 0f, 90f));
                    Cylinder("PumpPipe", center + new Vector3(side * 1.05f, 1.5f, 0.35f),
                        new Vector3(0.12f, 0.9f, 0.12f), _brass, _floorRoot, false);
                    break;
                case EvacuationTheme.RedHall:
                    Box("ImpossibleDoor", center + new Vector3(0f, 1.45f, 1.32f),
                        new Vector3(1.35f, 2.75f, 0.18f), _black, _floorRoot, false);
                    Sphere("DoorEye", center + new Vector3(0f, 1.62f, 1.2f),
                        new Vector3(0.16f, 0.1f, 0.04f), _redGlow, _floorRoot, false);
                    break;
            }
        }

        private void ConfigureHidingSpot(Transform root, string label, Vector3 hidingPoint,
            Vector3 exitPoint)
        {
            GameObject interactionRoot = new GameObject(root.name + "HidingInteraction");
            interactionRoot.transform.SetParent(root.parent, false);
            interactionRoot.transform.position = root.position;
            interactionRoot.transform.rotation = root.rotation;
            BoxCollider hitbox = interactionRoot.AddComponent<BoxCollider>();
            hitbox.size = root.localScale + new Vector3(0.12f, 0.12f, 0.12f);
            hitbox.isTrigger = true;
            EvacuationHidingSpot spot = interactionRoot.AddComponent<EvacuationHidingSpot>();
            spot.Configure(hidingPoint, Quaternion.identity, exitPoint);
            EvacuationInteractable interactable = interactionRoot.AddComponent<EvacuationInteractable>();
            interactable.Configure(EvacuationAction.Hide, label, EvacuationItemKind.PowerCell,
                null, spot);
        }

        private void CreateEmergencyLocker(Vector3 center)
        {
            Transform locker = Box("EmergencyLocker", center + new Vector3(1.02f, 0.92f, 0f),
                new Vector3(0.62f, 1.84f, 0.7f), _maintenance, _floorRoot);
            ConfigureHidingSpot(locker, "躲进应急储物柜", new Vector3(0f, -0.84f, 0f),
                new Vector3(-0.95f, -0.84f, 0f));
        }

        private void CreateEvidence(Vector3 position, int floorNumber, FloorEventKind floorEvent)
        {
            Transform root = new GameObject("Evidence_" + floorNumber).transform;
            root.SetParent(_floorRoot, false);
            root.position = position;
            Box("Folder", Vector3.zero, new Vector3(0.42f, 0.08f, 0.56f), _amberGlow, root, false);
            BoxCollider collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.54f, 0.18f, 0.68f);
            collider.isTrigger = true;
            EvacuationInteractable interactable = root.gameObject.AddComponent<EvacuationInteractable>();
            interactable.Configure(EvacuationAction.Evidence, "调查异常档案",
                EvacuationItemKind.PowerCell, null, null,
                floorEvent + "_" + Mathf.Abs(floorNumber % 9));
            CreateLight("EvidenceGlow", Vector3.up * 0.25f, new Color(1f, 0.34f, 0.04f),
                1.2f, 2.2f, root).shadows = LightShadows.None;
        }

        private void ApplyFloorEvent(EvacuationFloorPlan plan, List<Vector2Int> mainPath,
            System.Random random, int monsterBypassIndex)
        {
            if (mainPath.Count < 3 || plan.Event == FloorEventKind.None)
            {
                return;
            }
            Vector3 mid = CellPosition(mainPath[mainPath.Count / 2]);
            Vector3 far = CellPosition(mainPath[mainPath.Count - 1]);
            switch (plan.Event)
            {
                case FloorEventKind.RisingWater:
                    Box("EventFlood", mid + new Vector3(0f, 0.17f, 0f),
                        new Vector3(2.8f, 0.05f, 8f), _glass, _floorRoot, false);
                    break;
                case FloorEventKind.BaitCache:
                    CreatePickup(EvacuationItemKind.PowerCell, mid + new Vector3(0.7f, 0.42f, 0f));
                    CreatePickup(EvacuationItemKind.Medkit, mid + new Vector3(-0.55f, 0.32f, 0.2f));
                    Box("SecurityWarning", far + new Vector3(1.2f, 1.2f, 0.7f),
                        new Vector3(0.12f, 2.4f, 2.1f), _surveillanceSign, _floorRoot, false);
                    break;
                case FloorEventKind.LockdownPickup:
                    CreatePickup(EvacuationItemKind.PowerCell, mid + new Vector3(0.7f, 0.42f, 0f));
                    CreatePickup(EvacuationItemKind.Medkit, mid + new Vector3(-0.55f, 0.32f, 0.2f));
                    int shutterIndex = Mathf.Clamp(monsterBypassIndex > 0
                        ? monsterBypassIndex : mainPath.Count / 2, 1, mainPath.Count - 1);
                    Vector3 shutterCell = CellPosition(mainPath[shutterIndex]);
                    Vector3 beforeMid = CellPosition(mainPath[shutterIndex - 1]);
                    Vector3 shutterPosition = (beforeMid + shutterCell) * 0.5f + Vector3.up * 4.45f;
                    bool pathAlongZ = Mathf.Abs(shutterCell.z - beforeMid.z) >
                        Mathf.Abs(shutterCell.x - beforeMid.x);
                    Box("EventShutter", shutterPosition, pathAlongZ
                        ? new Vector3(2.78f, 2.8f, 0.16f)
                        : new Vector3(0.16f, 2.8f, 2.78f), _maintenance, _floorRoot);
                    CreateLight("LockdownAlarm", shutterPosition + Vector3.down * 2.8f,
                        new Color(1f, 0.025f, 0.01f), 3.2f, 7f, _floorRoot);
                    break;
                case FloorEventKind.SurvivorCamp:
                    for (int i = -1; i <= 1; i++)
                    {
                        Box("CampBedroll", mid + new Vector3(i * 0.72f, 0.05f, 0.5f),
                            new Vector3(0.55f, 0.06f, 1.15f), _apartment, _floorRoot, false);
                    }
                    CreateLight("CampLantern", mid + new Vector3(0f, 0.35f, -0.45f),
                        new Color(1f, 0.32f, 0.04f), 2f, 4f, _floorRoot);
                    break;
                case FloorEventKind.PowerExchange:
                    CreateEventInteractable("ExchangeMachine",
                        "使用电力交换机（零件优先，也可拆手电电池）",
                        EvacuationAction.PowerExchange, mid + new Vector3(0.92f, 0.8f, 0f),
                        new Vector3(0.75f, 1.6f, 0.62f), _powerSign);
                    CreatePickup(EvacuationItemKind.Fuse, mid + new Vector3(-0.72f, 0.3f, 0f));
                    break;
                case FloorEventKind.RingingPhone:
                    Transform phone = CreateEventInteractable("RingingPhone", "接起持续响铃的电话",
                        EvacuationAction.RingingPhone, mid + new Vector3(0f, 0.72f, 0f),
                        new Vector3(0.42f, 0.18f, 0.26f), _redGlow);
                    _audio.AttachPhoneSource(phone.gameObject);
                    _audio.PlayThreatCue(mid);
                    break;
                case FloorEventKind.ChasedSurvivor:
                    CreateLight("ChaseAlarm", mid + new Vector3(0f, 2.3f, 0f),
                        new Color(1f, 0.015f, 0.005f), 4.5f, 8f, _floorRoot);
                    Box("DroppedBag", mid + new Vector3(0.7f, 0.16f, -0.5f),
                        new Vector3(0.62f, 0.28f, 0.4f), _apartment, _floorRoot, false);
                    break;
                case FloorEventKind.DistantFootsteps:
                    _audio.PlayThreatCue(far);
                    break;
                case FloorEventKind.UnsyncedShadow:
                case FloorEventKind.MovingDarkness:
                    Sphere("MovingDarkness", far + new Vector3(0f, 1.35f, 0f),
                        new Vector3(2.4f, 2.8f, 1.4f), _black, _floorRoot, false);
                    break;
                case FloorEventKind.SilentCache:
                    CreatePickup(RandomSmallItem(random), far + new Vector3(0.6f, 0.32f, 0f));
                    CreatePickup(RandomSmallItem(random), far + new Vector3(-0.6f, 0.32f, 0f));
                    break;
                case FloorEventKind.FalseLobby:
                    Box("FalseExitSign", far + new Vector3(0f, 1.8f, 1.2f),
                        new Vector3(2.1f, 0.72f, 0.08f), _exitSign, _floorRoot, false);
                    CreateEventInteractable("ExitTerminal", "验证出口并离开大楼",
                        EvacuationAction.ExitTerminal, far + new Vector3(0f, 0.82f, 0.15f),
                        new Vector3(0.72f, 1.35f, 0.42f), _maintenance);
                    CreateLight("ExitTerminalBeacon", far + new Vector3(0f, 1.9f, 0.1f),
                        new Color(1f, 0.28f, 0.04f), 3f, 9f, _floorRoot);
                    break;
                case FloorEventKind.ShiftingRooms:
                case FloorEventKind.MirroredCorridor:
                    Box("ImpossibleWall", mid + new Vector3(0f, 1.55f, 0.85f),
                        new Vector3(2.2f, 3f, 0.08f), _glass, _floorRoot, true);
                    break;
            }
            if (plan.Pressure == FloorPressure.Anomaly || plan.Event == FloorEventKind.WrongFloorNumber ||
                plan.Event == FloorEventKind.PassengerMismatch)
            {
                Box("AnomalyMark", far + new Vector3(0f, 1.45f, 1.35f),
                    new Vector3(1.25f, 1.25f, 0.018f), _anomalyDecal, _floorRoot, false);
            }
        }

        private Transform CreateEventInteractable(string objectName, string label,
            EvacuationAction action, Vector3 position, Vector3 size, Material material)
        {
            Transform root = new GameObject(objectName).transform;
            root.SetParent(_floorRoot, false);
            root.position = position;
            Box("Visual", Vector3.zero, size, material, root, false);
            BoxCollider hitbox = root.gameObject.AddComponent<BoxCollider>();
            hitbox.size = size + Vector3.one * 0.12f;
            hitbox.isTrigger = true;
            root.gameObject.AddComponent<EvacuationInteractable>().Configure(action, label);
            return root;
        }

        private void CreatePickup(EvacuationItemKind kind, Vector3 position)
        {
            Material material = kind == EvacuationItemKind.PowerCell ||
                kind == EvacuationItemKind.EmergencyCell ? _maintenance :
                kind == EvacuationItemKind.Medkit ? _redHall : _metal;
            Material locatorMaterial = kind == EvacuationItemKind.Medkit ? _redGlow :
                kind == EvacuationItemKind.PowerCell ? _cyanGlow : _amberGlow;
            Transform root = new GameObject(kind.ToString()).transform;
            root.SetParent(_floorRoot, false);
            root.position = position;
            Box("Visual", Vector3.zero,
                kind == EvacuationItemKind.PowerCell ? new Vector3(0.48f, 0.7f, 0.35f) :
                kind == EvacuationItemKind.EmergencyCell ? new Vector3(0.42f, 0.48f, 0.3f) :
                new Vector3(0.32f, 0.32f, 0.32f),
                material, root, false);
            Box("LocatorStrip", new Vector3(0f, 0.17f, -0.205f),
                kind == EvacuationItemKind.PowerCell ? new Vector3(0.24f, 0.025f, 0.014f) :
                new Vector3(0.14f, 0.018f, 0.014f), locatorMaterial, root, false);
            Vector3 iconSize = kind == EvacuationItemKind.PowerCell
                ? new Vector3(0.3f, 0.56f, 0.018f)
                : new Vector3(0.22f, 0.4f, 0.018f);
            Box("ItemDecal", new Vector3(0f, 0f, -0.19f), iconSize,
                GetItemAtlasMaterial(kind), root, false);
            BoxCollider hitbox = root.gameObject.AddComponent<BoxCollider>();
            hitbox.size = kind == EvacuationItemKind.PowerCell
                ? new Vector3(0.58f, 0.82f, 0.48f)
                : kind == EvacuationItemKind.EmergencyCell
                    ? new Vector3(0.52f, 0.6f, 0.42f)
                : new Vector3(0.42f, 0.42f, 0.42f);
            hitbox.isTrigger = true;
            EvacuationInteractable interactable = root.gameObject.AddComponent<EvacuationInteractable>();
            interactable.Configure(EvacuationAction.Item, ItemLabel(kind), kind);
            Light glow = CreateLight(kind + "Glow", Vector3.up * 0.35f,
                kind == EvacuationItemKind.Medkit ? Color.red :
                kind == EvacuationItemKind.EmergencyCell ? new Color(1f, 0.38f, 0.04f) :
                new Color(0.08f, 0.9f, 0.72f),
                0.62f, 1.55f, root);
            glow.shadows = LightShadows.None;
        }

        public void PrepareMonsterForTest(bool watcher = false)
        {
            if (_monster != null)
            {
                ReleaseDynamicObject(_monster.gameObject);
            }
            CreateMonster(new Vector3(0f, 0f, 5.2f), 0f,
                watcher ? MonsterArchetype.Watcher : MonsterArchetype.Pursuer);
        }

        public void PositionMonsterForCapture(Vector3 position)
        {
            if (_monster != null)
            {
                _monster.transform.position = position;
            }
        }

        private void CreateMonster(Vector3 position, float delay, MonsterArchetype archetype,
            List<Vector2Int> routeCells = null)
        {
            GameObject root = new GameObject(archetype.ToString());
            root.transform.SetParent(_floorRoot, false);
            root.transform.position = position;
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 2.3f;
            controller.radius = 0.32f;
            controller.center = new Vector3(0f, 1.15f, 0f);
            float bodyScale = archetype == MonsterArchetype.CeilingChild ? 0.68f : 1f;
            Material bodyMaterial = archetype == MonsterArchetype.Janitor ? _maintenance : _black;
            Capsule("Torso", new Vector3(0f, 1.12f * bodyScale, 0f),
                new Vector3(0.48f, 1.05f * bodyScale, 0.34f), bodyMaterial, root.transform, false);
            Sphere("Head", new Vector3(0f, 2.25f * bodyScale, 0f),
                new Vector3(0.38f, 0.48f, 0.34f), _black, root.transform, false);
            Cylinder("LeftArm", new Vector3(-0.5f, 1.15f, 0f), new Vector3(0.075f, 0.92f, 0.075f), _black, root.transform, false,
                new Vector3(0f, 0f, -9f));
            Cylinder("RightArm", new Vector3(0.5f, 1.15f, 0f), new Vector3(0.075f, 0.92f, 0.075f), _black, root.transform, false,
                new Vector3(0f, 0f, 9f));
            Sphere("EyeL", new Vector3(-0.13f, 2.32f, -0.31f), new Vector3(0.035f, 0.022f, 0.015f), _redGlow, root.transform, false);
            Sphere("EyeR", new Vector3(0.13f, 2.32f, -0.31f), new Vector3(0.035f, 0.022f, 0.015f), _redGlow, root.transform, false);
            if (archetype == MonsterArchetype.Watcher)
            {
                Sphere("WatcherEye", new Vector3(0f, 2.3f, -0.34f),
                    new Vector3(0.18f, 0.1f, 0.04f), _redGlow, root.transform, false);
            }
            else if (archetype == MonsterArchetype.Janitor)
            {
                Box("JanitorApron", new Vector3(0f, 0.9f, -0.28f),
                    new Vector3(0.66f, 1.25f, 0.05f), _maintenance, root.transform, false);
                Cylinder("JanitorTool", new Vector3(0.67f, 0.85f, 0f),
                    new Vector3(0.04f, 1.35f, 0.04f), _brass, root.transform, false,
                    new Vector3(0f, 0f, 7f));
            }
            else if (archetype == MonsterArchetype.CeilingChild)
            {
                root.transform.localScale = new Vector3(1.15f, 0.78f, 1.35f);
                root.transform.position += Vector3.up * 0.08f;
            }
            _monster = root.AddComponent<EvacuationMonster>();
            List<Vector3> patrolRoute = null;
            if (routeCells != null)
            {
                patrolRoute = new List<Vector3>(routeCells.Count);
                for (int i = 0; i < routeCells.Count; i++) patrolRoute.Add(CellPosition(routeCells[i]));
            }
            int randomSeed = unchecked((_game.RunSeed * 397) ^ Mathf.RoundToInt(position.x * 31f) ^
                Mathf.RoundToInt(position.z * 101f) ^ (int)archetype * 7919);
            _monster.Initialize(_game, Player, _audio, delay, archetype, patrolRoute,
                _navigationGraph, randomSeed);
        }

        private void CreateNpc(Vector3 position, bool mimic, int destination)
        {
            GameObject root = new GameObject(mimic ? "Quiet Survivor" : "Survivor");
            root.transform.SetParent(_floorRoot, false);
            root.transform.position = position;
            Material clothes = mimic ? _mimicClothes : _survivorClothes;
            Capsule("Body", new Vector3(0f, 0.88f, 0f), new Vector3(0.45f, 0.8f, 0.34f), clothes, root.transform, false);
            Sphere("Head", new Vector3(0f, 1.82f, 0f), new Vector3(0.34f, 0.4f, 0.32f),
                mimic ? _black : _brass, root.transform, false);
            Cylinder("LeftArm", new Vector3(-0.43f, 0.92f, 0f),
                new Vector3(0.055f, 0.58f, 0.055f), clothes, root.transform, false,
                new Vector3(0f, 0f, -5f));
            Cylinder("RightArm", new Vector3(0.43f, 0.92f, 0f),
                new Vector3(0.055f, 0.58f, 0.055f), clothes, root.transform, false,
                new Vector3(0f, 0f, 5f));
            Box("CoatHem", new Vector3(0f, 0.42f, 0.02f),
                new Vector3(0.62f, 0.62f, 0.42f), clothes, root.transform, false);
            if (mimic)
            {
                Sphere("WrongEye", new Vector3(0.11f, 1.87f, -0.3f), new Vector3(0.028f, 0.018f, 0.012f), _redGlow, root.transform, false);
            }
            CharacterController controller = root.AddComponent<CharacterController>();
            controller.height = 2f;
            controller.radius = 0.34f;
            controller.center = new Vector3(0f, 1f, 0f);
            controller.stepOffset = 0.24f;
            EvacuationNpc npc = root.AddComponent<EvacuationNpc>();
            npc.Initialize(_game, Player, mimic, destination, _navigationGraph);
            EvacuationInteractable interactable = root.AddComponent<EvacuationInteractable>();
            interactable.Configure(EvacuationAction.Npc, mimic ? "邀请沉默的幸存者" : "邀请幸存者",
                EvacuationItemKind.PowerCell, npc);
        }

        private void CreateMannequin(Vector3 position)
        {
            Transform root = new GameObject("WallFacingResident").transform;
            root.SetParent(_floorRoot, false);
            root.position = position;
            root.rotation = Quaternion.Euler(0f, 180f, 0f);
            Capsule("Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.4f, 0.78f, 0.3f), _office, root, false);
            Sphere("Head", new Vector3(0f, 1.78f, 0f), new Vector3(0.31f, 0.38f, 0.3f), _black, root, false);
        }

        private void CreateControl(string displayName, string label, EvacuationAction action,
            Vector3 position, Material glow)
        {
            Transform root = new GameObject(action + "Control").transform;
            root.SetParent(_cabin, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box("Recess", new Vector3(0f, 0f, 0.025f), new Vector3(0.9f, 0.44f, 0.11f),
                _black, root, false);
            Transform panel = Box("Surface", Vector3.zero, new Vector3(0.82f, 0.4f, 0.075f),
                GetControlAtlasMaterial(action), root);
            EvacuationInteractable interactable = panel.gameObject.AddComponent<EvacuationInteractable>();
            interactable.Configure(action, label);
            Transform indicator = CreateControlIndicator(action, root, glow);
            _controlIndicators[action] = indicator.GetComponent<Renderer>();
        }

        private Transform CreateControlIndicator(EvacuationAction action, Transform root, Material glow)
        {
            Material indicatorMaterial = new Material(glow);
            return Box("StateLamp", new Vector3(0.3f, -0.145f, -0.066f),
                new Vector3(0.075f, 0.022f, 0.014f), indicatorMaterial, root, false);
        }

        private void ClearFloor()
        {
            _floorLights.Clear();
            if (_monster != null && _audio != null) _audio.DetachObject(_monster.gameObject);
            _monster = null;
            _navigationGraph = null;
            EvacuationSignals.Clear();
            if (_floorRoot != null)
            {
                EvacuationVfx vfx = _floorRoot.GetComponent<EvacuationVfx>();
                if (vfx != null) vfx.ReleaseSystems();
                _primitivePool.ReleaseHierarchy(_floorRoot);
                Destroy(_floorRoot.gameObject);
            }
        }

        private static bool TryAddExplorationRoom(Vector2Int anchor, HashSet<Vector2Int> cells,
            System.Random random, out Vector2Int roomCenter)
        {
            Vector2Int[] directions =
            {
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.down
            };
            int start = random.Next(directions.Length);
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int direction = directions[(start + i) % directions.Length];
                Vector2Int entry = anchor + direction;
                Vector2Int depth = entry + direction;
                Vector2Int perpendicular = new Vector2Int(-direction.y, direction.x);
                if (random.NextDouble() < 0.5)
                {
                    perpendicular = -perpendicular;
                }
                Vector2Int wing = depth + perpendicular;
                if (cells.Contains(entry) || cells.Contains(depth) || cells.Contains(wing))
                {
                    continue;
                }
                cells.Add(entry);
                cells.Add(depth);
                cells.Add(wing);
                Vector2Int alcove = entry + perpendicular;
                if (!cells.Contains(alcove) && random.NextDouble() < 0.65)
                {
                    cells.Add(alcove);
                }
                roomCenter = depth;
                return true;
            }
            roomCenter = anchor;
            return false;
        }

        private static int AddMonsterBypass(List<Vector2Int> mainPath, HashSet<Vector2Int> cells,
            System.Random random)
        {
            int midpoint = mainPath.Count / 2;
            for (int offset = 0; offset < mainPath.Count; offset++)
            {
                int i = midpoint + (offset % 2 == 0 ? offset / 2 : -(offset + 1) / 2);
                if (i < 1 || i >= mainPath.Count - 1) continue;
                Vector2Int before = mainPath[i] - mainPath[i - 1];
                Vector2Int after = mainPath[i + 1] - mainPath[i];
                if (before != after)
                {
                    continue;
                }
                Vector2Int side = new Vector2Int(-before.y, before.x);
                if (random.NextDouble() < 0.5)
                {
                    side = -side;
                }
                cells.Add(mainPath[i - 1] + side);
                cells.Add(mainPath[i] + side);
                cells.Add(mainPath[i + 1] + side);
                return i;
            }
            return -1;
        }

        private static Vector3 CellPosition(Vector2Int cell)
        {
            return new Vector3(cell.x * 3f, 0f, 4f + cell.y * 3f);
        }

        private Material GetThemeMaterial(EvacuationTheme theme)
        {
            switch (theme)
            {
                case EvacuationTheme.Hospital: return _hospital;
                case EvacuationTheme.Office: return _office;
                case EvacuationTheme.Apartment: return _apartment;
                case EvacuationTheme.Maintenance: return _maintenance;
                case EvacuationTheme.Flooded: return _flooded;
                default: return _redHall;
            }
        }

        private static Color GetThemeLight(EvacuationTheme theme)
        {
            switch (theme)
            {
                case EvacuationTheme.Hospital: return new Color(0.48f, 0.82f, 0.86f);
                case EvacuationTheme.Office: return new Color(0.92f, 0.64f, 0.34f);
                case EvacuationTheme.Apartment: return new Color(0.55f, 0.72f, 0.62f);
                case EvacuationTheme.RedHall: return new Color(1f, 0.03f, 0.012f);
                default: return new Color(0.25f, 0.76f, 0.78f);
            }
        }

        private static EvacuationItemKind RandomSmallItem(System.Random random)
        {
            double value = random.NextDouble();
            if (value < 0.22) return EvacuationItemKind.Medkit;
            if (value < 0.4) return EvacuationItemKind.Stimulant;
            if (value < 0.62) return EvacuationItemKind.FlashBattery;
            if (value < 0.76) return EvacuationItemKind.Fuse;
            if (value < 0.9) return EvacuationItemKind.Scrap;
            return EvacuationItemKind.Flashlight;
        }

        private static string ItemLabel(EvacuationItemKind kind)
        {
            switch (kind)
            {
                case EvacuationItemKind.PowerCell: return "搬起电梯电池";
                case EvacuationItemKind.EmergencyCell: return "搬起破损电池";
                case EvacuationItemKind.Medkit: return "使用医疗包";
                case EvacuationItemKind.Stimulant: return "注射肾上腺素";
                case EvacuationItemKind.Flashlight: return "拾取手电筒";
                case EvacuationItemKind.FlashBattery: return "更换手电池";
                case EvacuationItemKind.Fuse: return "拾取保险丝";
                default: return "拾取可交易的零件";
            }
        }

        private static Material MakeTextured(string name, string resourcePath, Color tint,
            float metallic, float smoothness)
        {
            Material material = MakeMaterial(name, tint, metallic, smoothness);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Repeat;
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(1.6f, 1.6f);
            }
            return material;
        }

        private static Material MakeAtlasMaterial(string name, int column, int row,
            float metallic, float smoothness)
        {
            Material material = MakeMaterial(name, Color.white, metallic, smoothness);
            Texture2D texture = Resources.Load<Texture2D>("Art/modular_surface_atlas");
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(0.25f, 0.25f);
                material.mainTextureOffset = new Vector2(column * 0.25f, row * 0.25f);
            }
            return material;
        }

        private static Material MakeAtlasDecal(string name, int column, int row)
        {
            Material material = MakeTransparent(name, Color.white);
            Texture2D texture = Resources.Load<Texture2D>("Art/anomaly_decal_atlas");
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                material.mainTexture = texture;
                material.mainTextureScale = new Vector2(0.25f, 0.25f);
                material.mainTextureOffset = new Vector2(column * 0.25f, row * 0.25f);
            }
            return material;
        }

        private Material GetItemAtlasMaterial(EvacuationItemKind kind)
        {
            Material material;
            if (_itemAtlasMaterials.TryGetValue(kind, out material)) return material;
            int index;
            switch (kind)
            {
                case EvacuationItemKind.PowerCell: index = 0; break;
                case EvacuationItemKind.EmergencyCell: index = 1; break;
                case EvacuationItemKind.Fuse: index = 2; break;
                case EvacuationItemKind.Medkit: index = 3; break;
                case EvacuationItemKind.Stimulant: index = 4; break;
                case EvacuationItemKind.Flashlight:
                case EvacuationItemKind.FlashBattery: index = 5; break;
                default: index = 6; break;
            }
            material = MakeUnlitAtlasMaterial(kind + " Icon", "Art/survival_item_atlas_v2", index,
                4, 2);
            _itemAtlasMaterials[kind] = material;
            return material;
        }

        private Material GetControlAtlasMaterial(EvacuationAction action)
        {
            Material material;
            if (_controlAtlasMaterials.TryGetValue(action, out material)) return material;
            int index = action == EvacuationAction.Descend ? 0 :
                action == EvacuationAction.Stop ? 1 :
                action == EvacuationAction.Door ? 2 :
                action == EvacuationAction.BatterySlot ? 3 : 4;
            material = MakeGeneratedAtlasMaterial(action + " Industrial Control",
                "Art/elevator_control_atlas_v3", index, 2, 3);
            _controlAtlasMaterials[action] = material;
            return material;
        }

        private static Material MakeGeneratedAtlasMaterial(string name, string resourcePath, int index,
            int columns = 4, int rows = 2)
        {
            Material material = MakeMaterial(name, Color.white, 0.08f, 0.34f);
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                material.mainTexture = texture;
                float tileWidth = 1f / columns;
                float tileHeight = 1f / rows;
                int rowFromTop = index / columns;
                material.mainTextureScale = new Vector2(tileWidth, tileHeight);
                material.mainTextureOffset = new Vector2((index % columns) * tileWidth,
                    1f - (rowFromTop + 1) * tileHeight);
            }
            return material;
        }

        private static Material MakeUnlitAtlasMaterial(string name, string resourcePath, int index,
            int columns, int rows)
        {
            Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = name, color = Color.white };
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                texture.wrapMode = TextureWrapMode.Clamp;
                material.mainTexture = texture;
                float tileWidth = 1f / columns;
                float tileHeight = 1f / rows;
                int rowFromTop = index / columns;
                material.mainTextureScale = new Vector2(tileWidth, tileHeight);
                material.mainTextureOffset = new Vector2((index % columns) * tileWidth,
                    1f - (rowFromTop + 1) * tileHeight);
            }
            material.enableInstancing = true;
            return material;
        }

        private static Material MakeMaterial(string name, Color color, float metallic, float smoothness)
        {
            Material material = new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            material.enableInstancing = true;
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
            Material material = MakeMaterial(name, color, 0.05f, 0.72f);
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3000;
            return material;
        }

        private Transform Box(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider = true)
        {
            return _primitivePool.Rent(PrimitiveType.Cube, name, parent, position, scale,
                Quaternion.identity, material, collider);
        }

        private Transform Sphere(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider)
        {
            return _primitivePool.Rent(PrimitiveType.Sphere, name, parent, position, scale,
                Quaternion.identity, material, collider);
        }

        private Transform Capsule(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider)
        {
            return _primitivePool.Rent(PrimitiveType.Capsule, name, parent, position, scale,
                Quaternion.identity, material, collider);
        }

        private Transform Cylinder(string name, Vector3 position, Vector3 scale, Material material,
            Transform parent, bool collider, Vector3 rotation = default(Vector3))
        {
            return _primitivePool.Rent(PrimitiveType.Cylinder, name, parent, position, scale,
                Quaternion.Euler(rotation), material, collider);
        }

        private Light CreateLight(string name, Vector3 position, Color color, float intensity,
            float range, Transform parent)
        {
            return _primitivePool.RentLight(name, parent, position, color, intensity, range);
        }

        private TextMesh CreateText(string name, Transform parent, string value,
            float characterSize, Color color, Vector3 position = default(Vector3))
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position == Vector3.zero ? new Vector3(0f, 0f, -0.57f) : position;
            textObject.transform.localRotation = Quaternion.identity;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.font = _worldFont != null ? _worldFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 80;
            text.characterSize = characterSize;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            textObject.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            return text;
        }
    }
}
