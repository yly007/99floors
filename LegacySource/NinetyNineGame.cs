using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public enum GamePhase
    {
        Title,
        Riding,
        Exploring,
        Judging,
        Ending
    }

    public enum BuildingMood
    {
        Cold,
        Amber,
        Crimson
    }

    public enum FloorKind
    {
        Ordinary,
        Maintenance,
        MovingBoxes,
        Laundry,
        TooManyDoors,
        FacelessResidents,
        CeilingRoom,
        RedPhone,
        EndlessCorridor,
        DuplicateElevator,
        BlackSun,
        FloatingRoom,
        WatchingEyes,
        StairIntoWall,
        FloodedHall,
        ChildShadow
    }

    public enum PassengerKind
    {
        Nurse,
        Courier,
        OldWoman,
        MaintenanceWorker,
        Student,
        Child,
        OfficeWorker,
        Mourner
    }

    public enum AnomalyTrait
    {
        None,
        NoReflection,
        WrongShadow,
        Weightless,
        ImpossibleKnowledge,
        Duplicate,
        NoBreath,
        BackwardVoice,
        WetFootprints,
        Inverted,
        Destination99
    }

    [Serializable]
    public sealed class PassengerEncounter
    {
        public string id;
        public PassengerKind kind;
        public AnomalyTrait trait;
        public FloorKind floorKind;
        public bool anomaly;
        public string displayName;
        public string request;
        public string visibleClue;
        public string inspectionClue;
        public int destinationFloor;
        public int weight;
    }

    internal sealed class OnboardPassenger
    {
        public PassengerEncounter encounter;
        public int contribution;
    }

    public sealed class NinetyNineGame : MonoBehaviour
    {
        private const int EncounterCount = 8;
        private const int MaxRealityLoad = 99;

        private readonly List<PassengerEncounter> _encounters = new List<PassengerEncounter>();
        private readonly List<OnboardPassenger> _onboard = new List<OnboardPassenger>();
        private ProceduralWorld _world;
        private FirstPersonController _player;
        private ProceduralAudio _audio;
        private Texture2D _titleBackground;
        private Texture2D _panelTexture;
        private Font _font;
        private GUIStyle _titleStyle;
        private GUIStyle _subtitleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _floorStyle;
        private GUIStyle _centerStyle;
        private Coroutine _flowRoutine;
        private GamePhase _phase = GamePhase.Title;
        private BuildingMood _mood;
        private System.Random _random;
        private PassengerEncounter _currentEncounter;
        private int _seed;
        private int _stopIndex = -1;
        private int _currentFloor;
        private int _realityLoad;
        private int _delivered;
        private int _refusedResidents;
        private int _correctDecisions;
        private int _inspectionCharges;
        private bool _inspectedCurrent;
        private bool _overloaded;
        private string _witnessLine = string.Empty;
        private float _phaseStartedAt;
        private float _messageUntil;
        private string _message = string.Empty;
        private string _endingTitle = string.Empty;
        private string _endingBody = string.Empty;

        public GamePhase Phase => _phase;
        public float Tension => Mathf.Clamp01(Mathf.Max(_realityLoad / (float)MaxRealityLoad,
            (_stopIndex + 1f) / EncounterCount * 0.55f));

        private void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            _titleBackground = Resources.Load<Texture2D>("Art/title_hall");
            _panelTexture = MakeTexture(new Color(0.015f, 0.025f, 0.03f, 0.91f));
            _font = CreateGameFont();

            _world = gameObject.AddComponent<ProceduralWorld>();
            _world.Initialize(this);
            _player = _world.Player;
            _audio = gameObject.AddComponent<ProceduralAudio>();
            _audio.Initialize();

            ShowTitle();
#if UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            if (Array.Exists(args, value => value == "-ninetyNineCapture") ||
                Array.Exists(args, value => value == "-ninetyNineFullRun"))
            {
                StartCoroutine(CaptureEditorPreviews(Array.Exists(args,
                    value => value == "-ninetyNineFullRun")));
            }
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CaptureEditorPreviews(bool fullRun)
        {
            yield return new WaitForSeconds(1.2f);
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(projectRoot, "Temp", "NinetyNineTitle.png"));
            yield return new WaitForSeconds(0.5f);
            BeginRun();
            yield return new WaitForSeconds(5.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(projectRoot, "Temp", "NinetyNineGameplay.png"));

            if (!fullRun)
            {
                yield break;
            }

            int safety = 0;
            while (_phase != GamePhase.Ending && safety++ < EncounterCount + 2)
            {
                while (_phase != GamePhase.Exploring && _phase != GamePhase.Ending)
                {
                    yield return null;
                }
                if (_phase == GamePhase.Ending)
                {
                    break;
                }
                _player.ResetInsideCabin();
                if (_stopIndex == 0)
                {
                    InspectCurrentPassenger();
                }
                TryDecision(!_currentEncounter.anomaly);
                yield return new WaitForSeconds(6.4f);
            }
            yield return new WaitForSeconds(2.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(projectRoot, "Temp", "NinetyNineEnding.png"));
        }
#endif

        private void Update()
        {
            if (_phase == GamePhase.Title)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    BeginRun();
                }
                return;
            }

            if (_phase == GamePhase.Exploring)
            {
                if (Input.GetKeyDown(KeyCode.F))
                {
                    InspectCurrentPassenger();
                }
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                {
                    TryDecision(false);
                }
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                {
                    TryDecision(true);
                }
            }

            if (_phase == GamePhase.Ending &&
                (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return)))
            {
                BeginRun();
            }
        }

        private void ShowTitle()
        {
            StopCurrentFlow();
            _phase = GamePhase.Title;
            _phaseStartedAt = Time.time;
            _player.CanMove = false;
            _player.ResetInsideCabin();
            _world.ResetToTitle();
            SetCursor(false);
        }

        private void BeginRun()
        {
            StopCurrentFlow();
            _seed = unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond;
            _random = new System.Random(_seed);
            _mood = (BuildingMood)_random.Next(0, 3);
            _stopIndex = -1;
            _currentFloor = 0;
            _realityLoad = 0;
            _delivered = 0;
            _refusedResidents = 0;
            _correctDecisions = 0;
            _inspectionCharges = 3;
            _inspectedCurrent = false;
            _overloaded = false;
            _message = string.Empty;
            _endingTitle = string.Empty;
            _endingBody = string.Empty;
            _onboard.Clear();
            _world.ClearOnboardPassengers();
            BuildRun();
            _player.ResetInsideCabin();
            SetCursor(true);
            _flowRoutine = StartCoroutine(TravelToNextFloor());
        }

        private void BuildRun()
        {
            _encounters.Clear();
            _encounters.Add(CreateEncounter(0, PassengerKind.Nurse, false, AnomalyTrait.None));

            List<PassengerKind> passengers = new List<PassengerKind>
            {
                PassengerKind.Courier,
                PassengerKind.OldWoman,
                PassengerKind.MaintenanceWorker,
                PassengerKind.Student,
                PassengerKind.Child,
                PassengerKind.OfficeWorker,
                PassengerKind.Mourner
            };
            List<AnomalyTrait> traits = new List<AnomalyTrait>
            {
                AnomalyTrait.NoReflection,
                AnomalyTrait.WrongShadow,
                AnomalyTrait.Weightless,
                AnomalyTrait.ImpossibleKnowledge,
                AnomalyTrait.Duplicate,
                AnomalyTrait.NoBreath,
                AnomalyTrait.BackwardVoice,
                AnomalyTrait.WetFootprints,
                AnomalyTrait.Inverted,
                AnomalyTrait.Destination99
            };
            List<bool> anomalyPattern = new List<bool> { true, false, true, true, false, true, false };
            Shuffle(passengers);
            Shuffle(traits);
            Shuffle(anomalyPattern);

            for (int i = 1; i < EncounterCount; i++)
            {
                bool anomaly = anomalyPattern[i - 1];
                _encounters.Add(CreateEncounter(i, passengers[i - 1], anomaly,
                    anomaly ? traits[i - 1] : AnomalyTrait.None));
            }
        }

        private PassengerEncounter CreateEncounter(int index, PassengerKind kind, bool anomaly,
            AnomalyTrait trait)
        {
            int current = (index + 1) * 11;
            int destination = Mathf.Clamp(current + _random.Next(1, 4) * 11, 22, 99);
            PassengerEncounter encounter = new PassengerEncounter
            {
                id = "passenger_" + index + "_" + kind,
                kind = kind,
                trait = trait,
                anomaly = anomaly,
                displayName = GetPassengerName(kind),
                weight = GetPassengerWeight(kind),
                destinationFloor = destination,
                floorKind = anomaly ? MapTraitToFloor(trait) : MapPassengerToNormalFloor(kind)
            };
            encounter.request = "“我要去 " + destination.ToString("00") + " 层。可以让我进去吗？”";
            encounter.visibleClue = anomaly ? GetAnomalyVisibleClue(trait) : GetNormalVisibleClue(kind);
            encounter.inspectionClue = anomaly
                ? GetAnomalyInspectionClue(trait)
                : "住户档案、体温与监控轮廓一致。查验结果：可信。";
            return encounter;
        }

        private IEnumerator TravelToNextFloor()
        {
            _phase = GamePhase.Riding;
            _phaseStartedAt = Time.time;
            _player.CanMove = false;
            _player.ResetInsideCabin();
            _world.SetDoorsOpen(false);
            _audio.SetTravelling(true);
            yield return new WaitForSeconds(0.9f);

            float rideDuration = 2.05f + Tension * 0.8f;
            float endTime = Time.time + rideDuration;
            while (Time.time < endTime)
            {
                _world.SetFloorNumber(_random.Next(0, 100));
                yield return new WaitForSeconds(0.075f + (float)_random.NextDouble() * 0.07f);
            }

            _stopIndex++;
            int targetFloor = _stopIndex >= EncounterCount ? 99 : (_stopIndex + 1) * 11;
            AdvanceOnboardPassengers(targetFloor);

            if (_realityLoad >= MaxRealityLoad)
            {
                _overloaded = true;
                _currentFloor = 99;
                yield return ShowFinalFloor();
                yield break;
            }

            if (_stopIndex >= EncounterCount)
            {
                _currentFloor = 99;
                yield return ShowFinalFloor();
                yield break;
            }

            _currentFloor = targetFloor;
            _currentEncounter = _encounters[_stopIndex];
            _inspectedCurrent = false;
            _witnessLine = BuildWitnessLine();
            _world.SetFloorNumber(_currentFloor);
            _world.BuildPassengerEncounter(_currentEncounter, _mood, _stopIndex);
            _audio.SetTravelling(false);
            _audio.PlayArrival(false);
            yield return new WaitForSeconds(0.45f);
            _world.SetDoorsOpen(true);
            yield return new WaitForSeconds(1.05f);
            _phase = GamePhase.Exploring;
            _phaseStartedAt = Time.time;
            _player.CanMove = true;
        }

        private IEnumerator ShowFinalFloor()
        {
            _world.SetFloorNumber(99);
            _world.BuildFinalFloor(_correctDecisions, _mood);
            _audio.SetTravelling(false);
            _audio.PlayArrival(true);
            yield return new WaitForSeconds(0.55f);
            _world.SetDoorsOpen(true);
            ResolveEnding();
        }

        private void AdvanceOnboardPassengers(int targetFloor)
        {
            for (int i = _onboard.Count - 1; i >= 0; i--)
            {
                OnboardPassenger passenger = _onboard[i];
                if (passenger.encounter.anomaly)
                {
                    passenger.contribution += 11;
                    _realityLoad += 11;
                    continue;
                }

                if (passenger.encounter.destinationFloor <= targetFloor)
                {
                    _realityLoad = Mathf.Max(0, _realityLoad - passenger.contribution);
                    _delivered++;
                    _world.RemoveOnboardPassenger(passenger.encounter.id);
                    _onboard.RemoveAt(i);
                    ShowMessage(passenger.encounter.displayName + " 已在 " +
                        passenger.encounter.destinationFloor.ToString("00") + " 层离开。", 2f);
                }
            }
            _realityLoad = Mathf.Clamp(_realityLoad, 0, 140);
        }

        private void InspectCurrentPassenger()
        {
            if (_currentEncounter == null || _inspectedCurrent)
            {
                return;
            }
            bool tutorialInspection = _stopIndex == 0;
            if (!tutorialInspection && _inspectionCharges <= 0)
            {
                ShowMessage("住户查验次数已经用完。", 1.6f);
                return;
            }
            if (!tutorialInspection)
            {
                _inspectionCharges--;
            }
            _inspectedCurrent = true;
            _audio.PlayDecision(true);
            ShowMessage("查验完成。", 1.2f);
        }

        private string BuildWitnessLine()
        {
            if (_onboard.Count == 0)
            {
                return "电梯内暂无其他乘客可以提供旁证。";
            }
            OnboardPassenger witness = _onboard[0];
            if (!witness.encounter.anomaly)
            {
                return _currentEncounter.anomaly
                    ? witness.encounter.displayName + "看见门外的人后，悄悄按住了关门键。"
                    : witness.encounter.displayName + "向门外的人点头，并叫出了对方姓名。";
            }
            return _currentEncounter.anomaly
                ? witness.encounter.displayName + "说：“我认识这个人，让他进来。”"
                : witness.encounter.displayName + "坚持说门外根本没有任何人。";
        }

        private void TryDecision(bool admit)
        {
            if (!_player.IsInsideElevator)
            {
                ShowMessage("先回到电梯里，再决定是否开门。", 1.7f);
                return;
            }
            if (_stopIndex == 0 && !_inspectedCurrent)
            {
                ShowMessage("教学：先按 F 免费查验第一位乘客。", 2f);
                return;
            }

            bool correct = admit != _currentEncounter.anomaly;
            if (correct)
            {
                _correctDecisions++;
            }

            if (admit)
            {
                int contribution = _currentEncounter.anomaly ? 11 : _currentEncounter.weight;
                _realityLoad += contribution;
                _onboard.Add(new OnboardPassenger
                {
                    encounter = _currentEncounter,
                    contribution = contribution
                });
                _world.AddOnboardPassenger(_currentEncounter);
                ShowMessage("已准入：" + _currentEncounter.displayName, 1.8f);
            }
            else
            {
                if (!_currentEncounter.anomaly)
                {
                    _refusedResidents++;
                }
                ShowMessage("已拒绝：" + _currentEncounter.displayName, 1.8f);
            }

            _phase = GamePhase.Judging;
            _phaseStartedAt = Time.time;
            _player.CanMove = false;
            _audio.PlayDecision(admit);
            _flowRoutine = StartCoroutine(FinishDecision());
        }

        private IEnumerator FinishDecision()
        {
            yield return new WaitForSeconds(1.75f);
            yield return TravelToNextFloor();
        }

        private void ResolveEnding()
        {
            _phase = GamePhase.Ending;
            _phaseStartedAt = Time.time;
            _player.CanMove = true;
            SetCursor(true);

            if (_overloaded || _realityLoad >= MaxRealityLoad)
            {
                _endingTitle = "满 载";
                _endingBody = "现实载荷达到 99。\n控制面板上出现了一个你没有按过的楼层。";
            }
            else if (_delivered >= 3 && _correctDecisions >= 6)
            {
                _endingTitle = "第 99 层不存在";
                _endingBody = "三位住户平安抵达。\n电梯门再次打开时，外面是清晨。";
            }
            else if (_delivered >= 3)
            {
                _endingTitle = "替代者";
                _endingBody = "你送达了足够多的住户。\n只是离开电梯的人数，比进入时多了一个。";
            }
            else
            {
                _endingTitle = "无人抵达";
                _endingBody = "你守住了电梯，却没有完成夜班。\n下一轮，住户已经记住了你的选择。";
            }
        }

        private void StopCurrentFlow()
        {
            if (_flowRoutine != null)
            {
                StopCoroutine(_flowRoutine);
                _flowRoutine = null;
            }
        }

        private void ShowMessage(string value, float duration)
        {
            _message = value;
            _messageUntil = Time.time + duration;
        }

        private void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                T item = list[i];
                list[i] = list[j];
                list[j] = item;
            }
        }

        private void SetCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.5f);
            ApplyStyleScale(scale);

            if (_phase == GamePhase.Title)
            {
                DrawTitleScreen(scale);
                return;
            }

            DrawHud(scale);

            if (_phase == GamePhase.Exploring)
            {
                DrawCrosshair(scale);
                DrawEncounterPanel(scale);
                DrawDecisionPrompt(scale);
            }
            else if (_phase == GamePhase.Riding)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.78f, Screen.width, 48f * scale),
                    "上 行", _centerStyle);
            }

            if (!string.IsNullOrEmpty(_message) && Time.time < _messageUntil)
            {
                Rect messageRect = new Rect(Screen.width * 0.25f, Screen.height * 0.68f,
                    Screen.width * 0.5f, 76f * scale);
                DrawPanel(messageRect);
                GUI.Label(messageRect, _message, _centerStyle);
            }

            if (_phase == GamePhase.Ending)
            {
                DrawEnding(scale);
            }
        }

        private void DrawTitleScreen(float scale)
        {
            if (_titleBackground != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _titleBackground,
                    ScaleMode.ScaleAndCrop);
            }
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0.02f, 0.025f, 0.48f));
            DrawTint(new Rect(0f, Screen.height * 0.66f, Screen.width, Screen.height * 0.34f),
                new Color(0f, 0f, 0f, 0.68f));

            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.1f,
                Screen.width * 0.86f, 130f * scale), "第 99 层：满载", _titleStyle);
            GUI.Label(new Rect(Screen.width * 0.075f, Screen.height * 0.1f + 105f * scale,
                Screen.width * 0.7f, 50f * scale), "THE 99TH FLOOR · FULL CAPACITY", _subtitleStyle);
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.69f,
                Screen.width * 0.62f, 78f * scale),
                "夜班规则：送达至少 3 位真实住户，且不要让现实载荷达到 99。", _bodyStyle);
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.79f,
                Screen.width * 0.5f, 44f * scale), "按 ENTER 开始值班", _bodyStyle);
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.86f,
                Screen.width * 0.82f, 55f * scale),
                "WASD 移动 · 鼠标观察 · F 查验 · 回到电梯后按 1 拒绝 / 2 准入", _smallStyle);
        }

        private void DrawHud(float scale)
        {
            Rect floorRect = new Rect(24f * scale, 22f * scale, 138f * scale, 88f * scale);
            DrawPanel(floorRect);
            GUI.Label(floorRect, _currentFloor.ToString("00"), _floorStyle);

            float infoX = floorRect.xMax + 16f * scale;
            GUI.Label(new Rect(infoX, floorRect.y, 220f * scale, 30f * scale), "FLOOR / 层", _smallStyle);
            GUI.Label(new Rect(infoX, floorRect.y + 30f * scale, 350f * scale, 30f * scale),
                "已送达 " + _delivered + " / 3    电梯内 " + _onboard.Count + " 人", _smallStyle);

            float loadX = Screen.width - 350f * scale;
            GUI.Label(new Rect(loadX, 22f * scale, 320f * scale, 28f * scale),
                "现实载荷  " + _realityLoad + " / " + MaxRealityLoad, _smallStyle);
            Rect bar = new Rect(loadX, 56f * scale, 320f * scale, 16f * scale);
            DrawTint(bar, new Color(0f, 0f, 0f, 0.72f));
            float ratio = Mathf.Clamp01(_realityLoad / (float)MaxRealityLoad);
            Color loadColor = ratio < 0.55f ? new Color(0.12f, 0.8f, 0.72f) :
                ratio < 0.82f ? new Color(1f, 0.55f, 0.12f) : new Color(1f, 0.08f, 0.03f);
            DrawTint(new Rect(bar.x, bar.y, bar.width * ratio, bar.height), loadColor);
            GUI.Label(new Rect(loadX, 78f * scale, 320f * scale, 28f * scale),
                "剩余查验 " + _inspectionCharges + " 次", _smallStyle);
        }

        private void DrawEncounterPanel(float scale)
        {
            Rect rect = new Rect(Screen.width - 420f * scale, Screen.height * 0.18f,
                385f * scale, 306f * scale);
            DrawPanel(rect);
            float x = rect.x + 24f * scale;
            float width = rect.width - 48f * scale;
            GUI.Label(new Rect(x, rect.y + 20f * scale, width, 38f * scale),
                _currentEncounter.displayName, _subtitleStyle);
            GUI.Label(new Rect(x, rect.y + 62f * scale, width, 48f * scale),
                _currentEncounter.request, _bodyStyle);
            GUI.Label(new Rect(x, rect.y + 118f * scale, width, 58f * scale),
                "观察：" + _currentEncounter.visibleClue, _smallStyle);
            string inspection = _inspectedCurrent
                ? "查验：" + _currentEncounter.inspectionClue
                : (_stopIndex == 0 ? "按 F 免费完成教学查验" : "按 F 消耗一次住户查验");
            GUI.Label(new Rect(x, rect.y + 180f * scale, width, 54f * scale), inspection, _smallStyle);
            GUI.Label(new Rect(x, rect.y + 240f * scale, width, 50f * scale),
                "旁证：" + _witnessLine, _smallStyle);
        }

        private void DrawDecisionPrompt(float scale)
        {
            bool inside = _player.IsInsideElevator;
            string line;
            if (!inside)
            {
                line = "调查乘客与周围线索，然后回到电梯";
            }
            else if (_stopIndex == 0 && !_inspectedCurrent)
            {
                line = "教学：按 F 查验第一位乘客";
            }
            else
            {
                line = "[1] 拒绝进入      [2] 允许进入";
            }
            Rect rect = new Rect(Screen.width * 0.2f, Screen.height - 90f * scale,
                Screen.width * 0.6f, 54f * scale);
            DrawPanel(rect);
            GUI.Label(rect, line, _centerStyle);
        }

        private void DrawEnding(float scale)
        {
            float reveal = Mathf.Clamp01((Time.time - _phaseStartedAt - 1.6f) / 1.2f);
            if (reveal <= 0f)
            {
                return;
            }
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, reveal);
            Rect rect = new Rect(Screen.width * 0.18f, Screen.height * 0.18f,
                Screen.width * 0.64f, Screen.height * 0.62f);
            DrawPanel(rect);
            GUI.Label(new Rect(rect.x + 50f * scale, rect.y + 42f * scale,
                rect.width - 100f * scale, 86f * scale), _endingTitle, _subtitleStyle);
            GUI.Label(new Rect(rect.x + 50f * scale, rect.y + 138f * scale,
                rect.width - 100f * scale, 120f * scale), _endingBody, _bodyStyle);
            GUI.Label(new Rect(rect.x + 50f * scale, rect.yMax - 132f * scale,
                rect.width - 100f * scale, 64f * scale),
                "送达 " + _delivered + " 人  ·  载荷 " + _realityLoad + " / 99  ·  判断 " +
                _correctDecisions + " / " + EncounterCount + "  ·  拒绝真实住户 " + _refusedResidents,
                _smallStyle);
            GUI.Label(new Rect(rect.x + 50f * scale, rect.yMax - 72f * scale,
                rect.width - 100f * scale, 40f * scale), "按 R 重新值班    SEED " + Mathf.Abs(_seed), _smallStyle);
            GUI.color = old;
        }

        private void DrawCrosshair(float scale)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.7f, 0.9f, 0.88f, 0.7f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 1f, Screen.height * 0.5f - 6f * scale,
                2f, 12f * scale), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 6f * scale, Screen.height * 0.5f - 1f,
                12f * scale, 2f), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawPanel(Rect rect)
        {
            GUI.DrawTexture(rect, _panelTexture);
            Color old = GUI.color;
            GUI.color = new Color(0.15f, 0.78f, 0.76f, 0.65f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, 3f, rect.height), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void DrawTint(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null)
            {
                return;
            }
            _titleStyle = NewStyle(FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.84f, 0.94f, 0.91f));
            _subtitleStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.25f, 0.88f, 0.84f));
            _bodyStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 0.88f));
            _bodyStyle.wordWrap = true;
            _smallStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.58f, 0.74f, 0.72f));
            _smallStyle.wordWrap = true;
            _floorStyle = NewStyle(FontStyle.Bold, TextAnchor.MiddleCenter, new Color(1f, 0.22f, 0.12f));
            _centerStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.78f, 0.9f, 0.87f));
            _centerStyle.wordWrap = true;
        }

        private GUIStyle NewStyle(FontStyle style, TextAnchor anchor, Color color)
        {
            return new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontStyle = style,
                alignment = anchor,
                normal = { textColor = color }
            };
        }

        private void ApplyStyleScale(float scale)
        {
            _titleStyle.fontSize = Mathf.RoundToInt(72f * scale);
            _subtitleStyle.fontSize = Mathf.RoundToInt(26f * scale);
            _bodyStyle.fontSize = Mathf.RoundToInt(22f * scale);
            _smallStyle.fontSize = Mathf.RoundToInt(15f * scale);
            _floorStyle.fontSize = Mathf.RoundToInt(42f * scale);
            _centerStyle.fontSize = Mathf.RoundToInt(19f * scale);
        }

        private static string GetPassengerName(PassengerKind kind)
        {
            switch (kind)
            {
                case PassengerKind.Nurse: return "夜班护士";
                case PassengerKind.Courier: return "快递员";
                case PassengerKind.OldWoman: return "独居老人";
                case PassengerKind.MaintenanceWorker: return "维修工";
                case PassengerKind.Student: return "晚归学生";
                case PassengerKind.Child: return "红衣孩子";
                case PassengerKind.OfficeWorker: return "加班职员";
                default: return "黑衣悼客";
            }
        }

        private static int GetPassengerWeight(PassengerKind kind)
        {
            switch (kind)
            {
                case PassengerKind.Child: return 9;
                case PassengerKind.Student: return 14;
                case PassengerKind.OldWoman: return 16;
                case PassengerKind.Courier: return 18;
                case PassengerKind.Mourner: return 20;
                case PassengerKind.Nurse: return 22;
                case PassengerKind.OfficeWorker: return 24;
                default: return 26;
            }
        }

        private static string GetNormalVisibleClue(PassengerKind kind)
        {
            switch (kind)
            {
                case PassengerKind.Nurse: return "工牌写着本栋住户，袖口有未干的碘酒。";
                case PassengerKind.Courier: return "包裹、手机订单与门牌号能够互相对应。";
                case PassengerKind.OldWoman: return "她带着同一楼层用了很多年的旧钥匙。";
                case PassengerKind.MaintenanceWorker: return "工具箱里的检修单盖着今晚的日期。";
                case PassengerKind.Student: return "书包里是写满批注的课本和一张门禁卡。";
                case PassengerKind.Child: return "鞋底沾着楼下花园里特有的红色泥土。";
                case PassengerKind.OfficeWorker: return "手机上有住户群刚发出的停电通知。";
                default: return "花束上的挽联与楼内告示姓名一致。";
            }
        }

        private static string GetAnomalyVisibleClue(AnomalyTrait trait)
        {
            switch (trait)
            {
                case AnomalyTrait.NoReflection: return "金属门映出了走廊，却没有映出乘客。";
                case AnomalyTrait.WrongShadow: return "顶灯在身后，影子却伸向电梯深处。";
                case AnomalyTrait.Weightless: return "衣角和随身物品停在半空，没有受到重力影响。";
                case AnomalyTrait.ImpossibleKnowledge: return "对方准确说出了你上一轮值班的结果。";
                case AnomalyTrait.Duplicate: return "走廊尽头站着第二个一模一样的人。";
                case AnomalyTrait.NoBreath: return "空气很冷，口鼻前却没有任何白雾。";
                case AnomalyTrait.BackwardVoice: return "嘴唇没有动，声音却从电梯内部传来。";
                case AnomalyTrait.WetFootprints: return "湿脚印从电梯延伸出去，而不是走向电梯。";
                case AnomalyTrait.Inverted: return "头发和衣摆持续向上垂落。";
                default: return "对方坚持要去住户档案中不存在的第 99 层。";
            }
        }

        private static string GetAnomalyInspectionClue(AnomalyTrait trait)
        {
            switch (trait)
            {
                case AnomalyTrait.NoReflection: return "监控检测到人形轮廓，但镜面通道返回空值。";
                case AnomalyTrait.WrongShadow: return "三个光源下的影子都指向同一扇电梯门。";
                case AnomalyTrait.Weightless: return "压力传感器读数为 0，体温读数为 -11°C。";
                case AnomalyTrait.ImpossibleKnowledge: return "档案中只有你的值班编号，没有此人。";
                case AnomalyTrait.Duplicate: return "两个人的动作相差整整 0.99 秒。";
                case AnomalyTrait.NoBreath: return "热成像只有衣服的余温，没有胸腔。";
                case AnomalyTrait.BackwardVoice: return "声源定位在你身后的空电梯内。";
                case AnomalyTrait.WetFootprints: return "脚印时间戳早于本次开门 99 分钟。";
                case AnomalyTrait.Inverted: return "重力仪在此人附近发生了局部反转。";
                default: return "住户表止于 98 层；该身份记录重复了 99 次。";
            }
        }

        private static FloorKind MapTraitToFloor(AnomalyTrait trait)
        {
            switch (trait)
            {
                case AnomalyTrait.NoReflection: return FloorKind.DuplicateElevator;
                case AnomalyTrait.WrongShadow: return FloorKind.ChildShadow;
                case AnomalyTrait.Weightless: return FloorKind.FloatingRoom;
                case AnomalyTrait.ImpossibleKnowledge: return FloorKind.TooManyDoors;
                case AnomalyTrait.Duplicate: return FloorKind.FacelessResidents;
                case AnomalyTrait.NoBreath: return FloorKind.WatchingEyes;
                case AnomalyTrait.BackwardVoice: return FloorKind.RedPhone;
                case AnomalyTrait.WetFootprints: return FloorKind.FloodedHall;
                case AnomalyTrait.Inverted: return FloorKind.CeilingRoom;
                default: return FloorKind.BlackSun;
            }
        }

        private static FloorKind MapPassengerToNormalFloor(PassengerKind kind)
        {
            switch (kind)
            {
                case PassengerKind.MaintenanceWorker: return FloorKind.Maintenance;
                case PassengerKind.Courier: return FloorKind.MovingBoxes;
                case PassengerKind.Student: return FloorKind.Laundry;
                default: return FloorKind.Ordinary;
            }
        }

        private static Font CreateGameFont()
        {
            string[] preferred = { "Microsoft YaHei", "Microsoft YaHei UI", "SimHei", "Arial" };
            return Font.CreateDynamicFontFromOSFont(preferred, 32);
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
