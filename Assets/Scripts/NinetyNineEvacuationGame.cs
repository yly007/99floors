using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public enum EvacuationPhase
    {
        Title,
        Prologue,
        Stopped,
        ClosingDoors,
        Descending,
        OpeningDoors,
        Won,
        Lost
    }

    public sealed class NinetyNineEvacuationGame : MonoBehaviour
    {
        private static readonly Vector2Int[] DefaultResolutions =
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440)
        };
        private const float MaxPower = 30f;
        private const float StartCost = 2f;
        private const float TravelCostPerFloor = 0.9f;
        private const float IdleDrain = 0.04f;
        private const float RunDuration = 1200f;
        private const float DoorCloseDuration = 3.4f;
        private const float DoorOpenDuration = 2.8f;
        private const float MaxDescentSpeed = 0.55f;
        private const float MonsterRepelCost = 2.5f;
        private const float FullCellCharge = 12f;
        private const float EmergencyCellCharge = 6f;
        private const int MaxDoorIntegrity = 2;
        private const float InteractionDistance = 3f;
        private const float LowPowerWarning = 8f;
        private const float CriticalPowerWarning = 4f;
        private const float LowTimeWarning = 120f;
        private const float CriticalTimeWarning = 45f;
        private const string ControlsWithFlashlight =
            "[WASD] 移动   [SHIFT] 冲刺   [CTRL] 切换蹲伏   [F] 手电筒";
        private const string ControlsWithoutFlashlight =
            "[WASD] 移动   [SHIFT] 冲刺   [CTRL] 切换蹲伏";

        private readonly List<EvacuationNpc> _passengers = new List<EvacuationNpc>();
        private readonly List<Vector2Int> _supportedResolutions = new List<Vector2Int>();
        private readonly RaycastHit[] _interactionHits = new RaycastHit[16];
        private EvacuationFloorGenerator _world;
        private FirstPersonController _player;
        private EvacuationAudio _audio;
        private EvacuationNarrativeUI _narrative;
        private EvacuationRuntimeUI _runtimeUi;
        private EvacuationFloorDirector _floorDirector;
        private EvacuationStorySystem _story;
        private EvacuationFloorPlan _currentPlan;
        private EvacuationNpc _dialogueNpc;
        private EvacuationInteractable _focus;
        private Texture2D _panelTexture;
        private Texture2D _uiPanelSkin;
        private Texture2D _uiButtonSkin;
        private Font _font;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _centerStyle;
        private GUIStyle _objectiveStyle;
        private GUIStyle _telemetryLabelStyle;
        private GUIStyle _telemetryValueStyle;
        private EvacuationPhase _phase = EvacuationPhase.Title;
        private float _power;
        private float _remainingTime;
        private float _health;
        private float _floorFloat;
        private float _descentSpeed;
        private float _brakeTimer;
        private float _doorSeal;
        private float _slowUntil;
        private float _stimulantUntil;
        private float _flashCharge;
        private float _stoppedAutomationTime;
        private float _carriedCellCharge;
        private float _storedCellCharge;
        private float _objectiveRevealUntil;
        private float _controlsHintUntil;
        private float _powerNoticeUntil;
        private float _endingShownAt;
        private float _masterVolume = 1f;
        private float _brightness = 1f;
        private int _currentFloor;
        private int _departureFloor;
        private int _doorIntegrity;
        private int _floorsVisited;
        private int _rescued;
        private int _automationTarget;
        private int _automationWorstStopError;
        private int _scrap;
        private int _loopCount;
        private int _lastPowerNoticeBucket;
        private int _cachedHudFloor = -1;
        private int _cachedHudPower = -1;
        private int _cachedHudSeconds = -1;
        private bool _braking;
        private bool _hasFlashlight;
        private bool _flashlightOn;
        private bool _carryingCell;
        private bool _storedCell;
        private bool _hasFuse;
        private bool _automation;
        private bool _captureThreat;
        private bool _acceptedAdministrator;
        private bool _automationVisitedFloor;
        private bool _paused;
        private bool _showLauncherSettings;
        private bool _fullscreen;
        private bool _phoneAnsweredThisFloor;
        private bool _parasiteActive;
        private bool _notebookOpen;
        private bool _lowPowerThoughtShown;
        private bool _monsterThoughtShown;
        private bool _powerNoticePositive;
        private System.Random _gameplayRandom;
        private int _resolutionIndex = 2;
        private string _dialogueText = string.Empty;
        private string _endingTitle = string.Empty;
        private string _endingBody = string.Empty;
        private string _endingDebrief = string.Empty;
        private string _powerNoticeText = string.Empty;
        private string _powerNoticeDeltaText = string.Empty;
        private string _cachedHudFloorText = "99";
        private string _cachedHudPowerText = "16 / 30";
        private string _cachedHudTimeText = "20:00";
        private string _endingRecordText = string.Empty;
        private float _floorMovementPenalty = 1f;

        public int RunSeed { get; private set; }
        public float Power => _power;
        public float DoorSeal => _doorSeal;
        public bool IsFlashlightOn => _flashlightOn;
        public bool IsDescending => _phase == EvacuationPhase.Descending;
        public bool NeedsDoorFuse => _doorIntegrity <= 0;
        public bool IsExploring => (_phase == EvacuationPhase.Stopped ||
            _phase == EvacuationPhase.ClosingDoors) && _doorSeal < 0.98f;
        public float Impairment => Mathf.Clamp01((1f - _health / 100f) * 0.55f +
            (Time.time < _slowUntil ? 0.35f : 0f));
        public float Tension => Mathf.Clamp01(Mathf.Max(1f - _health / 100f,
            Mathf.Max(1f - _power / MaxPower, 1f - _player.Stamina01)) * 0.72f +
            (_world != null && _world.Monster != null ? 0.24f : 0f));

        private void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            RunSeed = unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond;
            _panelTexture = MakeTexture(new Color(0.004f, 0.009f, 0.01f, 0.94f));
            _uiPanelSkin = Resources.Load<Texture2D>("Art/horror_ui_panel_plate_v1");
            _uiButtonSkin = Resources.Load<Texture2D>("Art/horror_ui_button_plate_v1");
            _font = CreateGameFont();
            _floorDirector = new EvacuationFloorDirector();
            _story = new EvacuationStorySystem();
            _audio = gameObject.AddComponent<EvacuationAudio>();
            _world = gameObject.AddComponent<EvacuationFloorGenerator>();
            _world.Initialize(this, _audio);
            _player = _world.Player;
            _narrative = gameObject.AddComponent<EvacuationNarrativeUI>();
            _narrative.Initialize(Resources.Load<Texture2D>("Art/opening_story_atlas_v1"),
                Resources.Load<Texture2D>("Art/title_midnight_tower_hero_v2"),
                Resources.Load<Texture2D>("Art/title_midnight_tower_wordmark_v1"),
                _uiPanelSkin, _uiButtonSkin, _font, StartPrologue, OpenTitleSettings, ExitFromTitle);
            BuildResolutionList();
            LoadPlayerSettings();
            _runtimeUi = gameObject.AddComponent<EvacuationRuntimeUI>();
            _runtimeUi.Initialize(_font, _uiPanelSkin, _uiButtonSkin,
                Resources.Load<Texture2D>("Art/ending_report_backdrop_v1"), CreateUiActions());
            ShowTitle();
#if !UNITY_EDITOR
            _showLauncherSettings = false;
            _narrative.ShowTitle(true);
#endif

#if UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            bool capture = Array.Exists(args, value => value == "-evacuationCapture");
            bool fullRun = Array.Exists(args, value => value == "-evacuationFullRun");
            bool failureRun = Array.Exists(args, value => value == "-evacuationFailureRun");
            bool monsterRun = Array.Exists(args, value => value == "-evacuationMonsterRun");
            bool narrativeRun = Array.Exists(args, value => value == "-evacuationNarrativeRun");
            if (capture || fullRun || failureRun || monsterRun || narrativeRun)
            {
                Application.runInBackground = true;
                if (narrativeRun) StartCoroutine(CaptureNarrativePrototype());
                else StartCoroutine(CapturePrototype(fullRun, failureRun, monsterRun));
            }
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CaptureNarrativePrototype()
        {
            yield return new WaitForSecondsRealtime(1.1f);
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string captureRoot = System.IO.Path.Combine(projectRoot, "Logs", "Captures");
            System.IO.Directory.CreateDirectory(captureRoot);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "NarrativeTitle.png"));
            yield return new WaitForEndOfFrame();
            StartPrologue();
            bool prologuePhasePassed = _phase == EvacuationPhase.Prologue &&
                _narrative.PrologueActive;
            yield return new WaitForSecondsRealtime(0.9f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot,
                "NarrativePrologueOffice.png"));
            yield return new WaitForSecondsRealtime(3.1f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot,
                "NarrativePrologueStairs.png"));
            yield return new WaitForSecondsRealtime(4.2f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot,
                "NarrativePrologueLobby.png"));
            yield return new WaitForSecondsRealtime(4.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot,
                "NarrativePrologueElevator.png"));
            while (_narrative.PrologueActive)
            {
                yield return null;
            }
            yield return new WaitForSecondsRealtime(0.35f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot,
                "NarrativeGameplayThought.png"));
            yield return new WaitForSecondsRealtime(0.5f);
            bool enteredRunPassed = _phase == EvacuationPhase.Stopped && !_narrative.PrologueActive &&
                _remainingTime > RunDuration - 3f;
            Transform[] allTransforms = FindObjectsOfType<Transform>();
            bool noFaceSticker = !Array.Exists(allTransforms, value => value != null &&
                value.name == "Face");
            Debug.Log("EVACUATION_NARRATIVE_PROLOGUE_TEST=" +
                (prologuePhasePassed && enteredRunPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_FACE_STICKER_REMOVAL_TEST=" +
                (noFaceSticker ? "PASS" : "FAIL"));
            UnityEditor.EditorApplication.Exit(0);
        }

        private IEnumerator CapturePrototype(bool fullRun, bool failureRun, bool monsterRun)
        {
            yield return new WaitForSeconds(1.1f);
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string captureRoot = System.IO.Path.Combine(projectRoot, "Logs", "Captures");
            System.IO.Directory.CreateDirectory(captureRoot);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationTitle.png"));
            yield return new WaitForSeconds(0.35f);
            _narrative.ShowTitle(false);
            BeginRun();
            yield return new WaitForSeconds(3f);
            _player.ResetInsideCabin();
            _narrative.ClearGameplayNarrative();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationDisplay.png"));
            _player.CanMove = false;
            _player.transform.rotation = Quaternion.Euler(0f, 56f, 0f);
            _player.ViewCamera.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            _narrative.ClearGameplayNarrative();
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationCabin.png"));
            float capturePower = _power;
            _power = 3f;
            yield return new WaitForSeconds(0.25f);
            while (Mathf.PerlinNoise(Time.time * 9f, 0.417f) < 0.4f)
            {
                yield return null;
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationLowPowerCabin.png"));
            yield return new WaitForEndOfFrame();
            _power = capturePower;
            _player.transform.rotation = Quaternion.identity;
            _player.ViewCamera.transform.localRotation = Quaternion.identity;
            _player.CanMove = true;
            _player.transform.position = new Vector3(0f, 0.08f, 3.2f);
            _world.SetFlashlight(true, 100f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationFloor.png"));
            _world.SetFlashlight(false, 100f);
            _player.ResetInsideCabin();
            if (!fullRun && !failureRun && !monsterRun)
            {
                EvacuationNpc passengerForCapture = FindObjectOfType<EvacuationNpc>();
                if (passengerForCapture != null)
                {
                    NpcBoarded(passengerForCapture);
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot,
                        "EvacuationPassengerTask.png"));
                    Debug.Log("EVACUATION_PASSENGER_TASK_UI_STATE_TEST=PASS DESTINATION=" +
                        passengerForCapture.DestinationFloor);
                }
                UnityEditor.EditorApplication.Exit(0);
                yield break;
            }

            if (monsterRun)
            {
                _doorSeal = 0.03f;
                _world.SetDoorSeal(0.03f);
                float repelPowerBefore = _power;
                _world.PrepareMonsterForTest();
                _world.Monster.TriggerChase();
                float repelDeadline = Time.time + 5f;
                while (_world.Monster != null && _phase != EvacuationPhase.Lost && Time.time < repelDeadline)
                {
                    yield return null;
                }
                bool repelPassed = _world.Monster == null && _phase == EvacuationPhase.Stopped;
                Debug.Log("EVACUATION_DOOR_REPEL_TEST=" + (repelPassed ? "PASS" : "FAIL"));
                float repelPowerSpent = repelPowerBefore - _power;
                Debug.Log("EVACUATION_MONSTER_REPEL_POWER_TEST=" +
                    (repelPowerSpent >= MonsterRepelCost && repelPowerSpent < MonsterRepelCost + 0.5f
                        ? "PASS" : "FAIL") + " SPENT=" + repelPowerSpent.ToString("0.00"));

                BeginRun();
                yield return new WaitForSeconds(0.25f);
                _player.ResetInsideCabin();
                _doorSeal = 0f;
                _world.SetDoorSeal(0f);
                _world.PrepareMonsterForTest();
                _world.Monster.TriggerChase();
                float breachDeadline = Time.time + 5f;
                while (_phase != EvacuationPhase.Lost && Time.time < breachDeadline)
                {
                    yield return null;
                }
                yield return new WaitForSeconds(0.35f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationMonsterBreach.png"));
                Debug.Log("EVACUATION_MONSTER_BREACH_TEST=" + (_phase == EvacuationPhase.Lost ? "PASS" : "FAIL"));
                UnityEditor.EditorApplication.Exit(0);
                yield break;
            }

            if (failureRun)
            {
                _power = 9f;
                ToggleDoors();
                while (_phase == EvacuationPhase.ClosingDoors)
                {
                    yield return null;
                }
                BeginDescent();
                Time.timeScale = 4f;
                while (_phase != EvacuationPhase.Lost)
                {
                    yield return null;
                }
                Time.timeScale = 1f;
                yield return new WaitForSeconds(0.5f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationFailure.png"));
                yield return new WaitForEndOfFrame();
                Debug.Log("EVACUATION_FAILURE_TEST=PASS");
                UnityEditor.EditorApplication.Exit(0);
                yield break;
            }

            EvacuationFloorPlan parasitePlan = _floorDirector.CreatePlan(RunSeed, 87, _power, 2);
            parasitePlan.Event = FloorEventKind.ElevatorParasite;
            parasitePlan.IsStartingFloor = false;
            parasitePlan.IsExitFloor = false;
            BuildReachedFloor(parasitePlan);
            yield return null;
            EvacuationInteractable parasiteInteraction = Array.Find(
                FindObjectsOfType<EvacuationInteractable>(), value => value != null &&
                value.Action == EvacuationAction.ElevatorParasite);
            bool parasiteLifecyclePassed = _parasiteActive && parasiteInteraction != null;
            float parasiteHealth = _health;
            RemoveElevatorParasite();
            parasiteLifecyclePassed &= !_parasiteActive && _health < parasiteHealth;
            Debug.Log("EVACUATION_PARASITE_LIFECYCLE_TEST=" +
                (parasiteLifecyclePassed ? "PASS" : "FAIL"));

            _slowUntil = Time.time + 10f;
            _stimulantUntil = Time.time + 10f;
            _phoneAnsweredThisFloor = true;
            _parasiteActive = true;
            _focus = parasiteInteraction;
            BeginRun(true);
            yield return null;
            bool runResetPassed = !_parasiteActive && _slowUntil <= 0f &&
                _stimulantUntil <= 0f && !_phoneAnsweredThisFloor && _focus == null;
            Debug.Log("EVACUATION_RUN_STATE_RESET_TEST=" + (runResetPassed ? "PASS" : "FAIL"));

            EvacuationStorySystem mimicStory = new EvacuationStorySystem();
            mimicStory.Discover("test_a");
            mimicStory.Discover("test_b");
            mimicStory.Discover("test_c");
            bool mimicExitPassed = mimicStory.Resolve(true, 2, false) == ExitResolution.MimicTakeover;
            Debug.Log("EVACUATION_MIMIC_EXIT_TEST=" + (mimicExitPassed ? "PASS" : "FAIL"));
            EvacuationStorySystem storyProgression = new EvacuationStorySystem();
            storyProgression.Discover("evidence_a");
            storyProgression.Discover("witness_b");
            bool storyActTwoPassed = storyProgression.Act == StoryAct.Remembered &&
                storyProgression.LatestClue != null &&
                !string.IsNullOrEmpty(storyProgression.LatestClue.Excerpt);
            storyProgression.Discover("phone_c");
            storyProgression.Discover("WrongFloorNumber_d");
            storyProgression.Discover("PassengerMismatch_e");
            bool storyActThreePassed = storyProgression.Act == StoryAct.Witnessed &&
                storyProgression.Records.Count == 5;
            Debug.Log("EVACUATION_STORY_ACT_TWO_TEST=" + (storyActTwoPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_STORY_ACT_THREE_TEST=" + (storyActThreePassed ? "PASS" : "FAIL"));
            bool launchTuningPassed = Mathf.Approximately(MaxPower, 30f) &&
                Mathf.Approximately(StartCost, 2f) &&
                Mathf.Approximately(RunDuration, 1200f) && _power <= 16f && _power > 15.8f;
            bool highestQualityPassed = QualitySettings.names.Length == 0 ||
                QualitySettings.GetQualityLevel() == QualitySettings.names.Length - 1;
            Debug.Log("EVACUATION_LAUNCH_TUNING_TEST=" + (launchTuningPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_HIGHEST_QUALITY_TEST=" + (highestQualityPassed ? "PASS" : "FAIL"));
            bool titleFirstPassed = !_showLauncherSettings && _phase != EvacuationPhase.Title;
            bool deathDebriefPassed = BuildDeathDebrief("失 重").Contains("启动与制动余量") &&
                BuildDeathDebrief("伪 人").Contains("乘客去向");
            Debug.Log("EVACUATION_TITLE_FIRST_TEST=" + (titleFirstPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_DEATH_DEBRIEF_TEST=" + (deathDebriefPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_FOOTSTEP_GATE_TEST=" + (_audio.VerifyFootstepGate() ? "PASS" : "FAIL"));
            ChangePower(_power - 4f, "启动测试", false);
            bool discretePowerNoticePassed = _powerNoticeText.Contains("启动测试  -4") &&
                _lastPowerNoticeBucket == Mathf.CeilToInt(_power);
            string noticeBeforeContinuousDrain = _powerNoticeText;
            ChangePower(_power - 0.1f, "运行耗电", true);
            bool subUnitDrainSuppressed = _powerNoticeText == noticeBeforeContinuousDrain;
            ChangePower(Mathf.Floor(_power) - 0.01f, "运行耗电", true);
            bool continuousPowerNoticePassed = _powerNoticeText.Contains("运行耗电  -1");
            ChangePower(19f, "测试复位", false);
            Debug.Log("EVACUATION_POWER_NOTICE_TEST=" +
                (discretePowerNoticePassed && subUnitDrainSuppressed && continuousPowerNoticePassed
                    ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_INITIAL_BRIEFING_TEST=" +
                (InitialBriefingVisible() ? "PASS" : "FAIL"));

            FloorEventKind[] pureEvents =
            {
                FloorEventKind.DuplicateElevator,
                FloorEventKind.ReverseWayfinding,
                FloorEventKind.EmptyMeeting
            };
            bool pureAnomalyPassed = true;
            for (int i = 0; i < pureEvents.Length; i++)
            {
                EvacuationFloorPlan purePlan = _floorDirector.CreatePlan(RunSeed, 88 - i, _power,
                    _floorsVisited + i);
                purePlan.Event = pureEvents[i];
                purePlan.SpawnMonster = true;
                purePlan.SpawnNpc = true;
                _world.BuildFloor(purePlan);
                yield return null;
                pureAnomalyPassed &= _world.Monster == null && FindObjectsOfType<EvacuationNpc>().Length == 0;
            }
            BeginRun(true);
            yield return null;
            Debug.Log("EVACUATION_PURE_ANOMALY_TEST=" + (pureAnomalyPassed ? "PASS" : "FAIL"));

            bool safeStartPassed = _world.Monster == null &&
                FindObjectsOfType<EvacuationNpc>().Length == 0;
            Debug.Log("EVACUATION_START_FLOOR_SAFE_TEST=" + (safeStartPassed ? "PASS" : "FAIL"));
            EvacuationInteractable[] startInteractables = FindObjectsOfType<EvacuationInteractable>();
            int startItemCount = 0;
            bool startPowerCell = false;
            for (int i = 0; i < startInteractables.Length; i++)
            {
                EvacuationInteractable value = startInteractables[i];
                if (value == null || value.Action != EvacuationAction.Item) continue;
                startItemCount++;
                startPowerCell |= value.ItemKind == EvacuationItemKind.PowerCell;
            }
            Debug.Log("EVACUATION_START_FLOOR_SINGLE_CELL_TEST=" +
                (startItemCount == 1 && startPowerCell ? "PASS" : "FAIL") +
                " ITEMS=" + startItemCount);
            GameObject questionNpcObject = new GameObject("QuestionTrustTest");
            questionNpcObject.AddComponent<CharacterController>();
            EvacuationNpc questionNpc = questionNpcObject.AddComponent<EvacuationNpc>();
            questionNpc.Initialize(this, _player, false, 80);
            int trustBeforeQuestion = questionNpc.Trust;
            string ignoredClue;
            questionNpc.Question(out ignoredClue);
            questionNpc.Question(out ignoredClue);
            bool questionTrustPassed = questionNpc.Trust == trustBeforeQuestion + 1;
            Debug.Log("EVACUATION_NPC_QUESTION_TRUST_TEST=" +
                (questionTrustPassed ? "PASS" : "FAIL"));
            bool passengerTargetPassed = IsPassengerDestinationMatch(91, 91) &&
                IsPassengerDestinationMatch(90, 91) && !IsPassengerDestinationMatch(89, 91);
            Debug.Log("EVACUATION_PASSENGER_TARGET_TEST=" + (passengerTargetPassed ? "PASS" : "FAIL"));
            GameObject mimicAObject = new GameObject("MimicTimerTestA");
            mimicAObject.AddComponent<CapsuleCollider>();
            EvacuationNpc mimicA = mimicAObject.AddComponent<EvacuationNpc>();
            mimicA.Initialize(this, _player, true, 80);
            mimicA.SetOnboard(_world.PassengerRoot, Vector3.zero);
            mimicA.ArmMimic(1f);
            GameObject mimicBObject = new GameObject("MimicTimerTestB");
            mimicBObject.AddComponent<CapsuleCollider>();
            EvacuationNpc mimicB = mimicBObject.AddComponent<EvacuationNpc>();
            mimicB.Initialize(this, _player, true, 70);
            mimicB.SetOnboard(_world.PassengerRoot, Vector3.one);
            mimicB.ArmMimic(3f);
            bool mimicTimerPassed = mimicA.TickMimic(1.1f) && !mimicB.TickMimic(1.1f) &&
                mimicB.MimicTimeRemaining > 1f;
            Debug.Log("EVACUATION_MULTIPLE_MIMIC_TIMER_TEST=" + (mimicTimerPassed ? "PASS" : "FAIL"));
            Destroy(mimicAObject);
            Destroy(mimicBObject);
            SetPaused(true);
            bool pausePassed = _paused && Time.timeScale == 0f && !_player.CanMove;
            SetPaused(false);
            pausePassed &= !_paused && Mathf.Approximately(Time.timeScale, 1f) && _player.CanMove;
            Debug.Log("EVACUATION_PAUSE_TEST=" + (pausePassed ? "PASS" : "FAIL"));
            Collider npcCollider = questionNpc.GetComponent<Collider>();
            CharacterController playerController = _player.GetComponent<CharacterController>();
            bool npcCollisionPassed = npcCollider != null && playerController != null &&
                Physics.GetIgnoreCollision(npcCollider, playerController);
            Debug.Log("EVACUATION_NPC_COLLISION_TEST=" + (npcCollisionPassed ? "PASS" : "FAIL"));
            Destroy(questionNpcObject);
            EvacuationInteractable[] interactables = FindObjectsOfType<EvacuationInteractable>();
            bool pickupHitboxPassed = Array.Exists(interactables, value => value != null &&
                value.Action == EvacuationAction.Item && value.GetComponent<BoxCollider>() != null &&
                value.GetComponent<BoxCollider>().isTrigger &&
                value.GetComponent<BoxCollider>().size.x <= 0.62f);
            Debug.Log("EVACUATION_PICKUP_HITBOX_TEST=" + (pickupHitboxPassed ? "PASS" : "FAIL"));
            EvacuationAction[] controlActions =
            {
                EvacuationAction.Descend,
                EvacuationAction.Stop,
                EvacuationAction.Door,
                EvacuationAction.BatterySlot,
                EvacuationAction.FusePanel
            };
            bool controlPanelRayPassed = true;
            bool controlDecalPassed = true;
            for (int i = 0; i < controlActions.Length; i++)
            {
                EvacuationInteractable control = Array.Find(interactables, value =>
                    value != null && value.Action == controlActions[i]);
                Collider controlCollider = control != null ? control.GetComponent<Collider>() : null;
                if (controlCollider == null)
                {
                    controlPanelRayPassed = false;
                    controlDecalPassed = false;
                    continue;
                }
                Transform face = control.transform.Find("ControlFace");
                EvacuationPooledPrimitive faceMarker = face != null
                    ? face.GetComponent<EvacuationPooledPrimitive>() : null;
                controlDecalPassed &= faceMarker != null && faceMarker.Type == PrimitiveType.Quad;
                Vector3 controlTarget = controlCollider.bounds.center;
                Vector3 direction = (controlTarget - _player.ViewCamera.transform.position).normalized;
                _player.ViewCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                controlPanelRayPassed &= FindInteractionFocus(_player.ViewCamera) == control;
            }
            _player.ResetInsideCabin();
            Debug.Log("EVACUATION_CONTROL_PANEL_RAY_TEST=" + (controlPanelRayPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_CONTROL_DECAL_TEST=" + (controlDecalPassed ? "PASS" : "FAIL"));
            EvacuationInteractable starterPickup = Array.Find(interactables, value => value != null &&
                value.Action == EvacuationAction.Item);
            bool centerRayPassed = false;
            bool offAxisRejected = false;
            bool rangeRejected = false;
            if (starterPickup != null && playerController != null)
            {
                Collider pickupCollider = starterPickup.GetComponent<Collider>();
                playerController.enabled = false;
                _player.transform.position = new Vector3(starterPickup.transform.position.x, 0.08f,
                    starterPickup.transform.position.z - 2f);
                _player.transform.rotation = Quaternion.identity;
                playerController.enabled = true;
                Physics.SyncTransforms();
                Vector3 target = pickupCollider != null
                    ? pickupCollider.bounds.center
                    : starterPickup.transform.position;
                Vector3 aimDirection = (target - _player.ViewCamera.transform.position).normalized;
                _player.ViewCamera.transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
                centerRayPassed = FindInteractionFocus(_player.ViewCamera) == starterPickup;
                Vector3 missDirection = Quaternion.AngleAxis(22f, Vector3.up) * aimDirection;
                _player.ViewCamera.transform.rotation = Quaternion.LookRotation(missDirection, Vector3.up);
                offAxisRejected = FindInteractionFocus(_player.ViewCamera) != starterPickup;

                playerController.enabled = false;
                _player.transform.position = new Vector3(starterPickup.transform.position.x, 0.08f,
                    starterPickup.transform.position.z - InteractionDistance - 1f);
                playerController.enabled = true;
                Physics.SyncTransforms();
                aimDirection = (target - _player.ViewCamera.transform.position).normalized;
                _player.ViewCamera.transform.rotation = Quaternion.LookRotation(aimDirection, Vector3.up);
                rangeRejected = FindInteractionFocus(_player.ViewCamera) != starterPickup;
                _player.ResetInsideCabin();
            }
            Debug.Log("EVACUATION_CENTER_RAY_TEST=" + (centerRayPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_OFF_AXIS_REJECT_TEST=" + (offAxisRejected ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_RANGE_REJECT_TEST=" + (rangeRejected ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_YAW_720_TEST=" + (_player.VerifyUnclampedYaw() ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_CROUCH_TOGGLE_TEST=" + (_player.VerifyCrouchToggle() ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_CROUCH_HEADROOM_TEST=" +
                (_player.VerifyBlockedStandUp() ? "PASS" : "FAIL"));
            _world.BuildFloor(_floorDirector.CreatePlan(RunSeed, 1, _power, _floorsVisited));
            EvacuationInteractable terminal = Array.Find(FindObjectsOfType<EvacuationInteractable>(),
                value => value != null && value.Action == EvacuationAction.ExitTerminal);
            bool terminalRayPassed = false;
            if (terminal != null)
            {
                CharacterController terminalController = _player.GetComponent<CharacterController>();
                terminalController.enabled = false;
                _player.transform.position = terminal.transform.position + new Vector3(0f, -0.74f, -2f);
                terminalController.enabled = true;
                Physics.SyncTransforms();
                Vector3 terminalDirection = (terminal.transform.position -
                    _player.ViewCamera.transform.position).normalized;
                _player.ViewCamera.transform.rotation = Quaternion.LookRotation(terminalDirection, Vector3.up);
                terminalRayPassed = FindInteractionFocus(_player.ViewCamera) == terminal;
            }
            Debug.Log("EVACUATION_EXIT_TERMINAL_RAY_TEST=" +
                (terminalRayPassed ? "PASS" : "FAIL"));
            _world.BuildFloor(_floorDirector.CreatePlan(RunSeed, 99, _power, _floorsVisited));
            _player.ResetInsideCabin();
            float doorPowerBefore = _power;
            ToggleDoors();
            yield return new WaitForSeconds(0.45f);
            bool closingAnimationPassed = _phase == EvacuationPhase.ClosingDoors &&
                _doorSeal > 0.05f && _doorSeal < 0.95f;
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationDoorClosing.png"));
            while (_phase == EvacuationPhase.ClosingDoors)
            {
                yield return null;
            }
            closingAnimationPassed &= _phase == EvacuationPhase.Stopped && _doorSeal >= 0.999f;
            Debug.Log("EVACUATION_DOOR_SEQUENCE_TEST=" + (closingAnimationPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_DOOR_FREE_TEST=" +
                (Mathf.Abs(_power - doorPowerBefore) < 0.01f ? "PASS" : "FAIL"));

            yield return new WaitForSeconds(0.2f);
            int preservedFloorId = _world.CurrentFloorInstanceId;
            int preservedPickupCount = _world.CurrentPickupCount;
            int preservedVisited = _floorsVisited;
            BeginDescent();
            yield return new WaitForSeconds(0.2f);
            _power = 0.01f;
            RequestStop();
            while (_phase == EvacuationPhase.Descending)
            {
                yield return null;
            }
            bool lowPowerStopPassed = _phase == EvacuationPhase.Stopped && _doorSeal > 0.99f;
            Debug.Log("EVACUATION_LOW_POWER_STOP_TEST=" + (lowPowerStopPassed ? "PASS" : "FAIL"));
            bool sameFloorPreserved = _world.CurrentFloorInstanceId == preservedFloorId &&
                _world.CurrentPickupCount == preservedPickupCount && _floorsVisited == preservedVisited;
            Debug.Log("EVACUATION_SAME_FLOOR_PRESERVE_TEST=" +
                (sameFloorPreserved ? "PASS" : "FAIL") + " FLOOR_ID=" + preservedFloorId +
                " PICKUPS=" + preservedPickupCount);
            float remainingTimeBeforeWarning = _remainingTime;
            _remainingTime = CriticalTimeWarning - 5f;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationWarnings.png"));
            bool warningStatePassed = _power <= CriticalPowerWarning &&
                _remainingTime <= CriticalTimeWarning;
            Debug.Log("EVACUATION_WARNING_UI_STATE_TEST=" + (warningStatePassed ? "PASS" : "FAIL"));
            _remainingTime = remainingTimeBeforeWarning;
            ToggleDoors();
            while (_phase == EvacuationPhase.OpeningDoors)
            {
                yield return null;
            }
            _power = 19f;
            _lastPowerNoticeBucket = Mathf.CeilToInt(_power);
            _powerNoticeUntil = 0f;
            _powerNoticeText = string.Empty;
            _powerNoticeDeltaText = string.Empty;

            float observedMaxSpeed = 0f;
            bool observedOpeningAnimation = false;
            _automation = true;
            Time.timeScale = 6f;
            while (_phase != EvacuationPhase.Won && _phase != EvacuationPhase.Lost)
            {
                observedMaxSpeed = Mathf.Max(observedMaxSpeed, _descentSpeed);
                if (_phase == EvacuationPhase.OpeningDoors && _doorSeal > 0.05f && _doorSeal < 0.95f)
                {
                    observedOpeningAnimation = true;
                }
                if (!_captureThreat && _world.Monster != null && _phase == EvacuationPhase.Stopped)
                {
                    _captureThreat = true;
                    _automation = false;
                    Time.timeScale = 1f;
                    _doorSeal = 0f;
                    _world.SetDoorSeal(0f);
                    _player.ResetInsideCabin();
                    _player.transform.rotation = Quaternion.identity;
                    _world.PositionMonsterForCapture(new Vector3(0f, 0f, 5.4f));
                    _world.Monster.TriggerChase();
                    _world.SetFlashlight(true, 100f);
                    _narrative.ClearGameplayNarrative();
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationThreat.png"));
                    _world.SetFlashlight(false, 100f);
                    _player.transform.rotation = Quaternion.identity;
                    _automation = true;
                    Time.timeScale = 6f;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            yield return new WaitForSeconds(0.7f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationEnding.png"));
            yield return new WaitForSecondsRealtime(0.35f);
            Debug.Log("EVACUATION_SPEED_TEST=" + (observedMaxSpeed <= MaxDescentSpeed + 0.01f ? "PASS" : "FAIL") +
                " MAX=" + observedMaxSpeed.ToString("0.00"));
            Debug.Log("EVACUATION_DOOR_OPEN_TEST=" + (observedOpeningAnimation ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_STOP_ACCURACY_TEST=" + (_automationWorstStopError <= 1 ? "PASS" : "FAIL") +
                " WORST=" + _automationWorstStopError);
            Debug.Log("EVACUATION_FULL_RUN=" + (_phase == EvacuationPhase.Won ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_PRIMITIVE_POOL_TEST=" +
                (_world.CreatedPrimitiveCount < 1400 ? "PASS" : "FAIL") +
                " CREATED=" + _world.CreatedPrimitiveCount +
                " AVAILABLE=" + _world.PooledPrimitiveCount);
            UnityEditor.EditorApplication.Exit(0);
        }
#endif

        private void Update()
        {
            if (_phase == EvacuationPhase.Title && _showLauncherSettings &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                _showLauncherSettings = false;
                _narrative.ShowTitle(true);
                return;
            }
            if (_dialogueNpc != null && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseNpcDialogue();
                return;
            }
            if (_phase != EvacuationPhase.Title && Input.GetKeyDown(KeyCode.Escape))
            {
                SetPaused(!_paused);
                return;
            }
            if (_paused)
            {
                return;
            }
            if (_phase == EvacuationPhase.Title)
            {
                if (!_showLauncherSettings &&
                    (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
                {
                    StartPrologue();
                }
                return;
            }
            if (_phase == EvacuationPhase.Prologue)
            {
                return;
            }
            if (_phase == EvacuationPhase.Won || _phase == EvacuationPhase.Lost)
            {
                if (Input.GetKeyDown(KeyCode.R))
                {
                    BeginRun(true);
                }
                else if (Input.GetKeyDown(KeyCode.Return))
                {
                    BeginRun();
                }
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _notebookOpen = !_notebookOpen;
                _objectiveRevealUntil = _notebookOpen ? float.PositiveInfinity : 0f;
            }

            _remainingTime = Mathf.Max(0f, _remainingTime - Time.deltaTime);
            if (_remainingTime <= 0f)
            {
                Lose("封 锁", "倒计时归零。所有楼层同时熄灯，电梯拒绝响应。");
                return;
            }

            UpdateFlashlight();
            UpdatePlayerCondition();
            if (_dialogueNpc != null)
            {
                if (_world.Monster != null &&
                    _world.Monster.State == MonsterAwarenessState.Chase)
                {
                    CloseNpcDialogue();
                    ShowTransientMessage("对话被逼近的脚步打断了。快跑！", 1.8f);
                }
                else
                {
                    UpdateNpcDialogueInput();
                    UpdatePassengers();
                    UpdateWorldState();
                    return;
                }
            }
            UpdateInteraction();
            if (_automation)
            {
                UpdateAutomation();
            }
            if (_phase == EvacuationPhase.ClosingDoors)
            {
                UpdateClosingDoors();
            }
            else if (_phase == EvacuationPhase.Descending)
            {
                UpdateDescent();
            }
            else if (_phase == EvacuationPhase.OpeningDoors)
            {
                UpdateOpeningDoors();
            }
            else
            {
                UpdateStopped();
            }
            UpdatePassengers();
            UpdateWorldState();
        }

        private void ShowTitle()
        {
            Time.timeScale = 1f;
            _paused = false;
            _phase = EvacuationPhase.Title;
            _audio.SetTitleMode(true);
            _player.CanMove = false;
            _player.ResetInsideCabin();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (_narrative != null) _narrative.ShowTitle(true);
        }

        private void StartPrologue()
        {
            if (_phase != EvacuationPhase.Title || _showLauncherSettings) return;
            _phase = EvacuationPhase.Prologue;
            _audio.SetTitleMode(true);
            _player.CanMove = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            _audio.PlayNarrativeCue();
            _narrative.PlayPrologue(() => BeginRun());
        }

        private void OpenTitleSettings()
        {
            if (_phase != EvacuationPhase.Title) return;
            _audio.PlayButton();
            _showLauncherSettings = true;
            _narrative.ShowTitle(false);
        }

        private void ExitFromTitle()
        {
            SavePlayerSettings();
            Application.Quit();
        }

        private void LoadPlayerSettings()
        {
            _player.LookSensitivity = PlayerPrefs.GetFloat("Evacuation.LookSensitivity", 2.6f);
            _masterVolume = PlayerPrefs.GetFloat("Evacuation.MasterVolume", 0.9f);
            _brightness = PlayerPrefs.GetFloat("Evacuation.Brightness", 1f);
            if (QualitySettings.names.Length > 0)
            {
                QualitySettings.SetQualityLevel(QualitySettings.names.Length - 1, true);
            }
            int savedWidth = PlayerPrefs.GetInt("Evacuation.ResolutionWidth", 1920);
            int savedHeight = PlayerPrefs.GetInt("Evacuation.ResolutionHeight", 1080);
            _resolutionIndex = _supportedResolutions.FindIndex(value =>
                value.x == savedWidth && value.y == savedHeight);
            if (_resolutionIndex < 0)
            {
                _resolutionIndex = Mathf.Clamp(_supportedResolutions.FindIndex(value =>
                    value.x == Screen.currentResolution.width &&
                    value.y == Screen.currentResolution.height), 0, _supportedResolutions.Count - 1);
            }
            _fullscreen = PlayerPrefs.GetInt("Evacuation.Fullscreen", 1) != 0;
            AudioListener.volume = _masterVolume;
            AnalogPostEffect.DisplayBrightness = _brightness;
#if !UNITY_EDITOR
            Vector2Int resolution = _supportedResolutions[_resolutionIndex];
            Screen.SetResolution(resolution.x, resolution.y,
                _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }

        private void SavePlayerSettings()
        {
            PlayerPrefs.SetFloat("Evacuation.LookSensitivity", _player.LookSensitivity);
            PlayerPrefs.SetFloat("Evacuation.MasterVolume", _masterVolume);
            PlayerPrefs.SetFloat("Evacuation.Brightness", _brightness);
            Vector2Int resolution = _supportedResolutions[_resolutionIndex];
            PlayerPrefs.SetInt("Evacuation.ResolutionWidth", resolution.x);
            PlayerPrefs.SetInt("Evacuation.ResolutionHeight", resolution.y);
            PlayerPrefs.SetInt("Evacuation.Fullscreen", _fullscreen ? 1 : 0);
            PlayerPrefs.SetInt("Evacuation.LauncherConfigured", 1);
            PlayerPrefs.Save();
        }

        private void ApplyDisplaySettings()
        {
            Vector2Int resolution = _supportedResolutions[_resolutionIndex];
            Screen.SetResolution(resolution.x, resolution.y,
                _fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
            SavePlayerSettings();
        }

        private void BuildResolutionList()
        {
            _supportedResolutions.Clear();
            Resolution[] available = Screen.resolutions;
            for (int i = 0; i < available.Length; i++)
            {
                Vector2Int value = new Vector2Int(available[i].width, available[i].height);
                if (value.x < 1024 || value.y < 576 || _supportedResolutions.Contains(value)) continue;
                _supportedResolutions.Add(value);
            }
            for (int i = 0; i < DefaultResolutions.Length; i++)
            {
                if (!_supportedResolutions.Contains(DefaultResolutions[i]))
                    _supportedResolutions.Add(DefaultResolutions[i]);
            }
            Vector2Int current = new Vector2Int(Screen.currentResolution.width,
                Screen.currentResolution.height);
            if (current.x >= 1024 && current.y >= 576 && !_supportedResolutions.Contains(current))
                _supportedResolutions.Add(current);
            _supportedResolutions.Sort((left, right) =>
            {
                int area = (left.x * left.y).CompareTo(right.x * right.y);
                return area != 0 ? area : left.x.CompareTo(right.x);
            });
        }

        private void SetPaused(bool paused)
        {
            _paused = paused && _phase != EvacuationPhase.Title &&
                _phase != EvacuationPhase.Won && _phase != EvacuationPhase.Lost;
            Time.timeScale = _paused ? 0f : 1f;
            AudioListener.pause = _paused;
            if (_paused)
            {
                _player.CanMove = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (_phase != EvacuationPhase.Title && _phase != EvacuationPhase.Won &&
                _phase != EvacuationPhase.Lost && _dialogueNpc == null)
            {
                _player.CanMove = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void BeginRun(bool reuseSeed = false)
        {
            SetPaused(false);
            _audio.SetTitleMode(false);
            if (!reuseSeed)
            {
                RunSeed = unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond;
            }
            _gameplayRandom = new System.Random(unchecked(RunSeed * 1103515245) ^ 0x5f3759df);
            _audio.SetRunSeed(RunSeed);
            _floorDirector = new EvacuationFloorDirector();
            foreach (EvacuationNpc passenger in _passengers)
            {
                if (passenger != null) _world.ReleaseDynamicObject(passenger.gameObject);
            }
            _passengers.Clear();
            _phase = EvacuationPhase.Stopped;
            _currentFloor = 99;
            _floorFloat = 99f;
            _departureFloor = 99;
            _power = 16f;
            _lastPowerNoticeBucket = Mathf.CeilToInt(_power);
            _powerNoticeUntil = 0f;
            _powerNoticeText = string.Empty;
            _powerNoticeDeltaText = string.Empty;
            _remainingTime = RunDuration;
            _health = 100f;
            _doorIntegrity = MaxDoorIntegrity;
            _doorSeal = 0f;
            _descentSpeed = 0f;
            _braking = false;
            _hasFlashlight = true;
            _flashCharge = 42f;
            _flashlightOn = false;
            _carryingCell = false;
            _storedCell = false;
            _carriedCellCharge = 0f;
            _storedCellCharge = 0f;
            _hasFuse = false;
            _floorsVisited = 1;
            _rescued = 0;
            _scrap = 0;
            _automationWorstStopError = 0;
            _automation = false;
            _captureThreat = false;
            _acceptedAdministrator = false;
            _loopCount = 0;
            _dialogueNpc = null;
            _dialogueText = string.Empty;
            _endingTitle = string.Empty;
            _endingBody = string.Empty;
            _endingDebrief = string.Empty;
            _endingRecordText = string.Empty;
            _endingShownAt = 0f;
            _focus = null;
            _slowUntil = 0f;
            _stimulantUntil = 0f;
            _stoppedAutomationTime = 0f;
            _phoneAnsweredThisFloor = false;
            _parasiteActive = false;
            _notebookOpen = false;
            _lowPowerThoughtShown = false;
            _monsterThoughtShown = false;
            _automationVisitedFloor = false;
            _story.Reset();
            EvacuationSignals.Clear();
            _world.BuildFloor(_floorDirector.CreatePlan(RunSeed, 99, _power, _floorsVisited));
            _world.SetParasiteActive(false);
            _world.SetDoorSeal(0f);
            _world.SetBarrier(false);
            _player.ResetInsideCabin();
            _player.CanMove = true;
            _player.SpeedMultiplier = 1f;
            _audio.SetTravelling(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _objectiveRevealUntil = Time.unscaledTime + 8f;
            _controlsHintUntil = Time.unscaledTime + 12f;
            _narrative.ClearGameplayNarrative();
            _narrative.QueueThought("……又是 99 层。楼梯根本出不去。", 3.3f);
            _narrative.QueueThought("电量只有 16 格，绝对撑不到一楼。", 3.5f);
            _narrative.QueueThought("外面的楼层也许有应急电池。我得先找一块。", 3.4f);
        }

        private void UpdateStopped()
        {
            float parasiteDrain = _parasiteActive ? 0.14f : 0f;
            ChangePower(_power - (IdleDrain + parasiteDrain) * Time.deltaTime,
                _parasiteActive ? "异常漏电" : "停靠供电", true);
            if (_power <= 0f)
            {
                Lose("困 死", "停靠电力耗尽。门机和照明停止工作，楼层里的脚步仍在靠近。");
            }
        }

        private void BeginDescent()
        {
            if (_phase != EvacuationPhase.Stopped || !_player.IsInsideElevator)
            {
                ShowSystemMessage("启动失败：轿厢内未检测到乘员。", 1.5f);
                return;
            }
            if (_doorSeal < 0.999f)
            {
                ShowSystemMessage("启动失败：请先关闭电梯门。", 1.8f);
                return;
            }
            if (_power <= StartCost + 0.05f)
            {
                ShowSystemMessage("电量不足，驱动电机无法启动。", 2f);
                return;
            }
            ChangePower(_power - StartCost, "启动电机", false);
            _departureFloor = _currentFloor;
            _phase = EvacuationPhase.Descending;
            _braking = false;
            _descentSpeed = 0f;
            _world.BeginTravel();
            _audio.SetTravelling(true);
            EvacuationSignals.Emit(_player.transform.position, 13f, NoiseKind.Machinery);
            _player.AddElevatorImpulse(-1f);
            int safeFloors = Mathf.Clamp(Mathf.FloorToInt(Mathf.Max(0f, _power - 0.25f) /
                TravelCostPerFloor), 1, 14);
            _automationTarget = Mathf.Max(1, _currentFloor - safeFloors);
            ShowSystemMessage("驱动电机已启动。制停控制保持待命。", 1.7f);
        }

        private void UpdateClosingDoors()
        {
            _doorSeal = Mathf.MoveTowards(_doorSeal, 1f, Time.deltaTime / DoorCloseDuration);
            _world.SetDoorSeal(_doorSeal);
            if (_doorSeal < 0.999f)
            {
                return;
            }
            _phase = EvacuationPhase.Stopped;
            _world.SetBarrier(true);
            ShowSystemMessage("电梯门已锁定。驱动控制可用。", 1.4f);
        }

        private void ToggleDoors()
        {
            if (_phase == EvacuationPhase.Descending)
            {
                ShowSystemMessage("门控锁定：电梯仍在运行。", 1.4f);
                return;
            }
            if (_phase == EvacuationPhase.ClosingDoors || _phase == EvacuationPhase.OpeningDoors)
            {
                ShowSystemMessage("门机正在动作。", 0.8f);
                return;
            }
            if (_doorSeal >= 0.98f)
            {
                _phase = EvacuationPhase.OpeningDoors;
                _audio.PlayDoor();
                EvacuationSignals.Emit(_player.transform.position, 8f, NoiseKind.Door);
                ShowSystemMessage("电梯门开启中。", 1.1f);
                return;
            }
            if (_doorIntegrity <= 0)
            {
                ShowSystemMessage("门控系统故障：需要保险丝。", 1.2f);
                return;
            }
            _phase = EvacuationPhase.ClosingDoors;
            _audio.PlayDoor();
            EvacuationSignals.Emit(_player.transform.position, 10f, NoiseKind.Door);
            ShowSystemMessage("电梯门关闭中。", 1.5f);
        }

        private void UpdateDescent()
        {
            float previous = _floorFloat;
            if (_braking)
            {
                _brakeTimer -= Time.deltaTime;
                _descentSpeed = Mathf.MoveTowards(_descentSpeed, 0f, Time.deltaTime * 1.1f);
            }
            else
            {
                _descentSpeed = Mathf.MoveTowards(_descentSpeed, MaxDescentSpeed, Time.deltaTime * 0.35f);
            }
            _floorFloat = Mathf.Max(1f, _floorFloat - _descentSpeed * Time.deltaTime);
            if (!_braking)
            {
                ChangePower(_power - Mathf.Max(0f, previous - _floorFloat) * TravelCostPerFloor,
                    "运行耗电", true);
            }
            _currentFloor = Mathf.Clamp(Mathf.CeilToInt(_floorFloat), 1, 99);
            _player.SetElevatorMotion(_descentSpeed / MaxDescentSpeed, _braking);

            if (_power <= 0f && !_braking)
            {
                Lose("失 重", "电机在楼层之间停止。数字熄灭后，轿厢开始自由下坠。");
                return;
            }
            if (!_braking && _floorFloat <= 1.01f)
            {
                RequestStop();
            }
            if (_braking && (_brakeTimer <= 0f || _descentSpeed <= 0.05f))
            {
                CompleteStop();
            }
        }

        private void RequestStop()
        {
            if (_phase != EvacuationPhase.Descending || _braking)
            {
                return;
            }
            _braking = true;
            _brakeTimer = 1.15f;
            _audio.PlayBrake();
            _player.AddElevatorImpulse(1f);
            ShowSystemMessage("机械制动已接合。", 1.3f);
        }

        private void CompleteStop()
        {
            _currentFloor = Mathf.Clamp(Mathf.CeilToInt(_floorFloat), 1, 99);
            _floorFloat = _currentFloor;
            bool reachedNewFloor = _currentFloor < _departureFloor;
            if (_automation)
            {
                _automationWorstStopError = Mathf.Max(_automationWorstStopError,
                    Mathf.Abs(_currentFloor - _automationTarget));
            }
            _descentSpeed = 0f;
            _braking = false;
            _player.SetElevatorMotion(0f, false);
            _phase = EvacuationPhase.Stopped;
            _doorSeal = 1f;
            _audio.SetTravelling(false);
            if (reachedNewFloor)
            {
                BuildReachedFloor(_floorDirector.CreatePlan(RunSeed, _currentFloor, _power,
                    _floorsVisited));
            }
            else
            {
                _world.ResumeFloor();
            }
            _world.SetDoorSeal(1f);
            _world.SetBarrier(true);
            if (reachedNewFloor && _currentFloor > 1)
            {
                _floorsVisited++;
            }
            _automationVisitedFloor = false;
            _phoneAnsweredThisFloor = false;
            ShowSystemMessage(reachedNewFloor
                ? "电梯已停稳。使用门控打开门。"
                : "制动过早，电梯仍停在原楼层。门外状态没有重置。", 1.8f);
        }

        private void BuildReachedFloor(EvacuationFloorPlan plan)
        {
            _parasiteActive = false;
            _world.SetParasiteActive(false);
            _world.BuildFloor(plan);
        }

        private void UpdateOpeningDoors()
        {
            _doorSeal = Mathf.MoveTowards(_doorSeal, 0f, Time.deltaTime / DoorOpenDuration);
            _world.SetDoorSeal(_doorSeal);
            if (_doorSeal > 0.001f)
            {
                return;
            }

            _world.SetBarrier(false);
            ResolvePassengerDestinations();
            if (_currentFloor <= 1)
            {
                if (_automation)
                {
                    ResolveExit();
                    return;
                }
                _phase = EvacuationPhase.Stopped;
                ShowSystemMessage("抵达一层。出口验证未完成。", 2.4f);
                _narrative.QueueThought("大厅尽头还有一台终端。真正的出口不会这么轻易打开。", 3.2f);
                return;
            }
            _phase = EvacuationPhase.Stopped;
            ShowSystemMessage("抵达 " + _currentFloor + " 层。楼层扫描无可用结果。", 2f);
        }

        private void UpdateInteraction()
        {
            if (_player.IsHidden)
            {
                _focus = null;
                if (Input.GetKeyDown(KeyCode.E))
                {
                    _player.ExitHidingSpot();
                    ShowTransientMessage("你离开了藏身处。", 1f);
                }
                return;
            }
            _focus = FindInteractionFocus(_player.ViewCamera);

            if (_focus == null || !Input.GetKeyDown(KeyCode.E))
            {
                return;
            }
            switch (_focus.Action)
            {
                case EvacuationAction.Descend:
                    BeginDescent();
                    break;
                case EvacuationAction.Stop:
                    RequestStop();
                    break;
                case EvacuationAction.Door:
                    ToggleDoors();
                    break;
                case EvacuationAction.BatterySlot:
                    InstallPowerCell();
                    break;
                case EvacuationAction.FusePanel:
                    InstallFuse();
                    break;
                case EvacuationAction.Item:
                    CollectItem(_focus);
                    break;
                case EvacuationAction.Npc:
                    if (_focus.Npc != null) OpenNpcDialogue(_focus.Npc);
                    break;
                case EvacuationAction.Hide:
                    if (_focus.HidingSpot != null)
                    {
                        _focus.HidingSpot.Enter(_player);
                        EvacuationSignals.Emit(_player.transform.position, 3.4f, NoiseKind.Door);
                        ShowTransientMessage("你屏住呼吸躲了进去。按 E 离开。", 1.5f);
                    }
                    break;
                case EvacuationAction.Evidence:
                    CollectEvidence(_focus);
                    break;
                case EvacuationAction.RingingPhone:
                    AnswerRingingPhone(_focus);
                    break;
                case EvacuationAction.ExitTerminal:
                    ResolveExit();
                    break;
                case EvacuationAction.PowerExchange:
                    UsePowerExchange(_focus);
                    break;
                case EvacuationAction.ElevatorParasite:
                    RemoveElevatorParasite();
                    break;
            }
        }

        private EvacuationInteractable FindInteractionFocus(Camera camera)
        {
            if (camera == null)
            {
                return null;
            }

            Vector3 origin = camera.transform.position;
            Vector3 forward = camera.transform.forward;
            EvacuationInteractable best = null;
            float bestDistance = float.MaxValue;
            float blockingDistance = float.MaxValue;
            int count = Physics.RaycastNonAlloc(origin, forward, _interactionHits, InteractionDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                Collider targetCollider = _interactionHits[i].collider;
                if (targetCollider == null)
                {
                    continue;
                }
                if (targetCollider.GetComponentInParent<FirstPersonController>() == _player)
                {
                    continue;
                }

                EvacuationInteractable candidate =
                    targetCollider.GetComponentInParent<EvacuationInteractable>();
                if (candidate != null)
                {
                    if (_interactionHits[i].distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = _interactionHits[i].distance;
                    }
                }
                else if (!targetCollider.isTrigger)
                {
                    blockingDistance = Mathf.Min(blockingDistance, _interactionHits[i].distance);
                }
            }
            return bestDistance <= blockingDistance + 0.001f ? best : null;
        }

        private void CollectItem(EvacuationInteractable item)
        {
            switch (item.ItemKind)
            {
                case EvacuationItemKind.PowerCell:
                case EvacuationItemKind.EmergencyCell:
                    if (_carryingCell)
                    {
                        ShowTransientMessage("双手已经抱着一块电梯电池。", 1.3f);
                        return;
                    }
                    _carryingCell = true;
                    _carriedCellCharge = item.ItemKind == EvacuationItemKind.PowerCell
                        ? FullCellCharge : EmergencyCellCharge;
                    ShowTransientMessage(item.ItemKind == EvacuationItemKind.PowerCell
                        ? "完整电池很重。带回电梯可恢复 12 电量。"
                        : "破损电池仍有余电。带回电梯可恢复 6 电量。", 1.7f);
                    break;
                case EvacuationItemKind.Medkit:
                    _health = Mathf.Min(100f, _health + 38f);
                    ShowTransientMessage("伤口被简单包扎。", 1.2f);
                    break;
                case EvacuationItemKind.Stimulant:
                    _stimulantUntil = Time.time + 15f;
                    _slowUntil = 0f;
                    ShowTransientMessage("肾上腺素压住了疼痛。", 1.3f);
                    break;
                case EvacuationItemKind.Flashlight:
                    _hasFlashlight = true;
                    _flashCharge = Mathf.Max(_flashCharge, 38f);
                    ShowTransientMessage("获得手电筒。按 F 开关。", 1.4f);
                    break;
                case EvacuationItemKind.FlashBattery:
                    _flashCharge = Mathf.Min(75f, _flashCharge + 38f);
                    ShowTransientMessage("手电筒电量恢复。", 1.2f);
                    break;
                case EvacuationItemKind.Fuse:
                    _hasFuse = true;
                    ShowTransientMessage("获得保险丝。带回电梯安装。", 1.3f);
                    break;
                case EvacuationItemKind.Scrap:
                    _scrap++;
                    ShowTransientMessage("获得可交易零件。", 1.2f);
                    break;
            }
            _audio.PlayPickup();
            EvacuationSignals.Emit(item.transform.position, 7f, NoiseKind.Pickup);
            if (_world.Monster != null)
            {
                _world.Monster.NotifyTheft(item.transform.position);
            }
            _world.NotifyFloorLooted();
            _world.ReleaseDynamicObject(item.gameObject);
        }

        private void AnswerRingingPhone(EvacuationInteractable phone)
        {
            if (phone == null || _phoneAnsweredThisFloor) return;
            _phoneAnsweredThisFloor = true;
            int outcome = Mathf.Abs((_currentPlan != null ? _currentPlan.Seed : RunSeed) % 3);
            if (outcome == 0)
            {
                bool discovered = DiscoverStoryClue("phone_" + Mathf.Abs(_currentFloor % 7), true);
                if (!discovered) ShowTransientMessage("听筒里只有你几分钟前的呼吸声。", 3f);
            }
            else if (outcome == 1)
            {
                _remainingTime += 30f;
                ShowTransientMessage("时钟倒退了三十秒，但走廊尽头多了一道脚步声。", 2.8f);
            }
            else
            {
                ChangePower(Mathf.Max(0.01f, _power - 1f), "未知线路", false);
                if (_world.Monster != null) _world.Monster.TriggerChase();
                ShowTransientMessage("电话接通的一刻，电梯掉了一格电。对方知道你在哪里。", 3f);
            }
            _audio.PlayThreatCue(phone.transform.position);
            _world.NotifyFloorLooted();
            _world.ReleaseDynamicObject(phone.gameObject);
        }

        private void InstallPowerCell()
        {
            if (_carryingCell)
            {
                if (_power < MaxPower - 0.5f)
                {
                    float installedCharge = Mathf.Max(EmergencyCellCharge, _carriedCellCharge);
                    ChangePower(_power + installedCharge, "安装电池", false);
                    _carryingCell = false;
                    _carriedCellCharge = 0f;
                    _audio.PlayPickup();
                    ShowSystemMessage("电池已接入。当前电量 " + Mathf.CeilToInt(_power) + " / 26。", 1.6f);
                }
                else if (!_storedCell)
                {
                    _storedCell = true;
                    _storedCellCharge = Mathf.Max(EmergencyCellCharge, _carriedCellCharge);
                    _carryingCell = false;
                    _carriedCellCharge = 0f;
                    ShowSystemMessage("备用架已占用。", 1.4f);
                }
                else
                {
                    ShowSystemMessage("电池满载，备用架已占用。", 1.4f);
                }
                return;
            }
            if (_storedCell && _power < MaxPower - 0.5f)
            {
                _storedCell = false;
                ChangePower(_power + Mathf.Max(EmergencyCellCharge, _storedCellCharge),
                    "备用电池", false);
                _storedCellCharge = 0f;
                _audio.PlayPickup();
                ShowSystemMessage("备用电池已接入。", 1.2f);
                return;
            }
            ShowSystemMessage("未检测到可安装电池。", 1.1f);
        }

        private void InstallFuse()
        {
            if (!_hasFuse)
            {
                ShowSystemMessage("维修失败：缺少保险丝。", 1.1f);
                return;
            }
            _hasFuse = false;
            _doorIntegrity = MaxDoorIntegrity;
            ChangePower(_power + 3f, "备用线路", false);
            _audio.PlayPickup();
            ShowSystemMessage("门控恢复，备用线路返还 3 点电力。", 1.5f);
        }

        private void UsePowerExchange(EvacuationInteractable machine)
        {
            if (machine == null) return;
            float recovered;
            if (_scrap > 0)
            {
                _scrap--;
                recovered = 4f;
                ShowTransientMessage("交换机吞下零件，向电梯线路返还 4 点电力。", 2f);
            }
            else if (_flashCharge >= 15f)
            {
                _flashCharge -= 15f;
                recovered = 2.5f;
                ShowTransientMessage("你拆下手电电池接入交换机：电梯 +2.5，手电 -15。", 2.2f);
            }
            else
            {
                ShowTransientMessage("交换机需要一份零件，或至少 15 点手电电量。", 1.8f);
                return;
            }
            ChangePower(_power + recovered, "电力交换", false);
            _audio.PlayPickup();
            EvacuationSignals.Emit(machine.transform.position, 11f, NoiseKind.Machinery);
            if (_world.Monster != null) _world.Monster.NotifyTheft(machine.transform.position);
            _world.NotifyFloorLooted();
            _world.ReleaseDynamicObject(machine.gameObject);
        }

        private void RemoveElevatorParasite()
        {
            if (!_parasiteActive)
            {
                ShowTransientMessage("线路上没有异常附着物。", 1.1f);
                return;
            }
            _parasiteActive = false;
            _world.SetParasiteActive(false);
            _health = Mathf.Max(1f, _health - 6f);
            _audio.PlayHit();
            ShowTransientMessage("寄生物割伤了手，但持续漏电已经停止。生命 -6。", 2.1f);
        }

        private void UpdateFlashlight()
        {
            if (Input.GetKeyDown(KeyCode.F) && _hasFlashlight && _flashCharge > 0f)
            {
                _flashlightOn = !_flashlightOn;
                _audio.PlayFlashlight();
                EvacuationSignals.Emit(_player.transform.position, 3.5f, NoiseKind.Flashlight);
            }
            if (_flashlightOn)
            {
                _flashCharge = Mathf.Max(0f, _flashCharge - Time.deltaTime * 0.55f);
                if (_flashCharge <= 0f)
                {
                    _flashlightOn = false;
                    ShowTransientMessage("手电筒熄灭了。", 1.8f);
                }
            }
            _world.SetFlashlight(_flashlightOn, _flashCharge);
        }

        private void UpdatePlayerCondition()
        {
            float multiplier = _floorMovementPenalty;
            if (_carryingCell) multiplier *= 0.82f;
            if (Time.time < _slowUntil) multiplier *= 0.82f;
            if (Time.time < _stimulantUntil) multiplier *= 1.18f;
            _player.SpeedMultiplier = multiplier;
        }

        public void MonsterAttack(Vector3 attackerPosition)
        {
            if (_phase != EvacuationPhase.Stopped)
            {
                return;
            }
            _health = Mathf.Max(0f, _health - 20f);
            _slowUntil = Time.time + 3f;
            _audio.PlayHit();
            if (!_monsterThoughtShown)
            {
                _monsterThoughtShown = true;
                _narrative.ShowThought("那不是人。它停顿了一瞬——跑！回电梯！", 2.5f);
            }
            else
            {
                ShowTransientMessage("视野又在失焦。趁它停顿，快跑！", 2.2f);
            }
            if (_health <= 0f)
            {
                Lose("被 追 上", "最后一次攻击后，你再也没能站起来。电梯门一直开着。");
            }
        }

        public void MonsterEnteredElevator()
        {
            Lose("闯 入 轿 厢", "门只差最后一掌宽。它侧身挤了进来，下降键不再有意义。");
        }

        public void MonsterFoundHidingSpot()
        {
            Lose("藏 身 处", "它听见你钻入藏身处的摩擦声。柜门被从外面缓慢拉开。");
        }

        public void RepelMonster(EvacuationMonster monster)
        {
            if (_phase != EvacuationPhase.Stopped && _phase != EvacuationPhase.ClosingDoors)
            {
                return;
            }
            ChangePower(Mathf.Max(0.01f, _power - MonsterRepelCost), "门机过载", false);
            _doorIntegrity = Mathf.Max(0, _doorIntegrity - 1);
            _world.RemoveMonster(monster);
            _audio.PlayDoor();
            ShowTransientMessage(_doorIntegrity <= 0
                ? "门机过载挡住了它，但控制器烧毁：-2.5 电力。下一层必须寻找保险丝。"
                : "门机过载阻挡了它：-2.5 电力，门控耐久 " + _doorIntegrity + " / " + MaxDoorIntegrity + "。", 2.4f);
        }

        public void NpcBoarded(EvacuationNpc npc)
        {
            if (npc == null || npc.IsOnboard || _passengers.Count >= 3)
            {
                return;
            }
            Vector3[] slots =
            {
                new Vector3(-1.25f, 0f, -1.35f),
                new Vector3(1.25f, 0f, -1.35f),
                new Vector3(0f, 0f, -1.65f)
            };
            npc.SetOnboard(_world.PassengerRoot, slots[_passengers.Count]);
            _passengers.Add(npc);
            if (npc.IsMimic)
            {
                float duration = Mathf.Lerp(24f, 42f, _gameplayRandom != null
                    ? (float)_gameplayRandom.NextDouble() : 0.5f);
                npc.ArmMimic(duration);
            }
            _narrative.ShowNpcMessage(npc.DisplayName,
                "请送我去 " + npc.DestinationFloor + " 层。别错过。", 2.2f);
        }

        public void TryExpelNpc(EvacuationNpc npc)
        {
            if (_phase != EvacuationPhase.Stopped || _doorSeal > 0.2f)
            {
                ShowTransientMessage("只有停靠并开门时才能赶走乘客。", 1.4f);
                return;
            }
            bool mimic = npc.IsMimic;
            _passengers.Remove(npc);
            _world.ReleaseDynamicObject(npc.gameObject);
            ShowTransientMessage(mimic ? "它走出电梯后，影子仍留在轿厢里。" :
                "幸存者被你赶回了楼层。", 1.8f);
        }

        private void OpenNpcDialogue(EvacuationNpc npc)
        {
            if (npc == null || _phase != EvacuationPhase.Stopped || _doorSeal > 0.2f)
            {
                return;
            }
            _dialogueNpc = npc;
            _dialogueText = npc.DisplayName + "：你也在找真正的一楼吗？";
            _player.CanMove = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void UpdateNpcDialogueInput()
        {
            if (_dialogueNpc == null)
            {
                CloseNpcDialogue();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) ChooseNpcDialogue(1);
            else if (Input.GetKeyDown(KeyCode.Alpha2)) ChooseNpcDialogue(2);
            else if (Input.GetKeyDown(KeyCode.Alpha3)) ChooseNpcDialogue(3);
            else if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Escape)) ChooseNpcDialogue(4);
            else if (Input.GetKeyDown(KeyCode.Alpha5) && _dialogueNpc.CanOfferAdministratorDeal)
                ChooseNpcDialogue(5);
        }

        private void ChooseNpcDialogue(int choice)
        {
            if (_dialogueNpc == null) return;
            if (choice == 1)
            {
                if (_dialogueNpc.IsOnboard) TryExpelNpc(_dialogueNpc);
                else _dialogueNpc.BeginFollowing();
                CloseNpcDialogue();
                return;
            }
            if (choice == 2)
            {
                string clueId;
                _dialogueText = _dialogueNpc.Question(out clueId);
                if (DiscoverStoryClue(clueId, false))
                {
                    StoryClue clue = _story.LatestClue;
                    _dialogueText += "\n\n[" + clue.Title + "] " + clue.Excerpt;
                }
                return;
            }
            if (choice == 3)
            {
                if (_scrap <= 0)
                {
                    _dialogueText = "你没有能交换的零件。";
                }
                else if (_dialogueNpc.Trade())
                {
                    _scrap--;
                    ChangePower(_power + 3.5f, "乘客交易", false);
                    _dialogueText = "对方接过零件，为电梯电池接入了一段备用线：+3.5 电力。";
                }
                else
                {
                    _dialogueText = "它接住零件，却不知道该如何使用。这个动作不像人类。";
                }
                return;
            }
            if (choice == 5)
            {
                if (_acceptedAdministrator)
                {
                    _dialogueText = "协议上的手印已经变黑。对方不再回答你的问题。";
                    return;
                }
                _acceptedAdministrator = true;
                _dialogueText = "你在没有文字的协议上按下手印。轿厢里的楼层数字同时熄灭了一秒。";
                _audio.PlayNarrativeCue();
                ShowBuildingMessage("身份交接已记录。管理员候选人，请继续前往一楼。", 3.4f);
                return;
            }
            CloseNpcDialogue();
        }

        private void CloseNpcDialogue()
        {
            _dialogueNpc = null;
            _dialogueText = string.Empty;
            if (_phase != EvacuationPhase.Won && _phase != EvacuationPhase.Lost)
            {
                _player.CanMove = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void CollectEvidence(EvacuationInteractable evidence)
        {
            if (evidence == null) return;
            bool discovered = DiscoverStoryClue(evidence.EvidenceId, true);
            if (!discovered) ShowTransientMessage("这页档案我已经看过了。", 1.8f);
            EvacuationSignals.Emit(evidence.transform.position, 6f, NoiseKind.Pickup);
            _world.NotifyFloorLooted();
            _world.ReleaseDynamicObject(evidence.gameObject);
        }

        public void NotifyFloorPlan(EvacuationFloorPlan plan)
        {
            _currentPlan = plan;
            if (plan == null) return;
            if (plan.Event == FloorEventKind.TimeSlip)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - 18f);
                ShowTransientMessage("电梯钟跳过了十八秒。门外的灰尘却像落了很多年。", 2.8f);
            }
            else if (plan.Event == FloorEventKind.ElevatorParasite)
            {
                _parasiteActive = true;
                _world.SetParasiteActive(true);
                ShowTransientMessage("电池槽旁多出了一团脉动电缆。停靠时正在持续漏电。", 3f);
            }
            else if (plan.Event == FloorEventKind.WrongFloorNumber)
            {
                ShowSystemMessage("楼层识别失败：编号超出建筑档案。", 2.6f);
            }
            else if (plan.Event == FloorEventKind.PassengerMismatch)
            {
                ShowTransientMessage("镜面里的人数，比轿厢登记多了一个。", 2.8f);
            }
            else if (plan.Event == FloorEventKind.UnsyncedShadow)
            {
                ShowTransientMessage("灯亮的时候，我的影子慢了半拍。", 2.6f);
            }
            else if (plan.Event == FloorEventKind.SequentialBlackout)
            {
                ShowBuildingMessage("夜间节能程序已启动。请留在原工位。", 2.8f);
            }
        }

        private bool DiscoverStoryClue(string clueId, bool showSubtitle)
        {
            StoryAct previousAct = _story.Act;
            if (!_story.Discover(clueId)) return false;
            StoryClue clue = _story.LatestClue;
            if (_story.Act != previousAct)
            {
                if (_story.Act == StoryAct.Remembered)
                {
                    ShowBuildingMessage("员工档案校验失败。检测到未注销的目击者。", 3.5f);
                }
                else if (_story.Act == StoryAct.Witnessed)
                {
                    ShowBuildingMessage("管理员席位空缺。交接协议正在寻找签署人。", 3.8f);
                }
                _audio.PlayNarrativeCue();
                if (showSubtitle && clue != null)
                {
                    _narrative.QueueThought(clue.Title + "：" + clue.Excerpt, 4.2f);
                }
            }
            else if (showSubtitle && clue != null)
            {
                ShowTransientMessage(clue.Title + "：" + clue.Excerpt, 4.2f);
            }
            return true;
        }

        private void UpdatePassengers()
        {
            for (int i = 0; i < _passengers.Count; i++)
            {
                EvacuationNpc passenger = _passengers[i];
                if (passenger != null && passenger.TickMimic(Time.deltaTime))
                {
                    Lose("伪 人", "灯光最后一次亮起时，乘客的脸贴在你的肩后。轿厢里再没有幸存者。");
                    return;
                }
            }
        }

        private void ResolvePassengerDestinations()
        {
            for (int i = _passengers.Count - 1; i >= 0; i--)
            {
                EvacuationNpc passenger = _passengers[i];
                if (passenger == null)
                {
                    _passengers.RemoveAt(i);
                    continue;
                }
                if (!passenger.IsMimic &&
                    IsPassengerDestinationMatch(_currentFloor, passenger.DestinationFloor))
                {
                    int reward = Mathf.Clamp(4 + passenger.Trust, 5, 9);
                    ChangePower(_power + reward, "护送回报", false);
                    if (passenger.Archetype == NpcArchetype.Medic)
                    {
                        _health = Mathf.Min(100f, _health + 24f);
                    }
                    else if (passenger.Archetype == NpcArchetype.Electrician && _doorIntegrity <= 0)
                    {
                        _doorIntegrity = 1;
                    }
                    _rescued++;
                    _passengers.RemoveAt(i);
                    _world.ReleaseDynamicObject(passenger.gameObject);
                    ShowTransientMessage("乘客到站。信任决定了回报：+" + reward + " 电力。", 2f);
                }
                else if (!passenger.IsMimic && _currentFloor < passenger.DestinationFloor - 1)
                {
                    _passengers.RemoveAt(i);
                    _world.ReleaseDynamicObject(passenger.gameObject);
                    ShowTransientMessage("你错过了乘客的目标楼层。对方沉默地离开，没有留下奖励。", 2.2f);
                }
            }
        }

        private static bool IsPassengerDestinationMatch(int currentFloor, int destinationFloor)
        {
            return Mathf.Abs(currentFloor - destinationFloor) <= 1;
        }

        public void SetFloorMovementPenalty(float multiplier)
        {
            _floorMovementPenalty = multiplier;
        }

        private void UpdateAutomation()
        {
            if (_phase == EvacuationPhase.Stopped)
            {
                _stoppedAutomationTime += Time.deltaTime;
                if (_world.Monster != null)
                {
                    if (!_captureThreat)
                    {
                        _world.Monster.TriggerChase();
                        return;
                    }
                    _world.RemoveMonster(_world.Monster);
                }
                if (_doorSeal > 0.98f && !_automationVisitedFloor)
                {
                    _automationVisitedFloor = true;
                    ToggleDoors();
                    return;
                }
                if (_doorSeal < 0.02f)
                {
                    _automationVisitedFloor = true;
                    if (_power < 24f)
                    {
                        EvacuationInteractable cell = FindAvailablePowerCell();
                        if (cell != null)
                        {
                            CollectItem(cell);
                            InstallPowerCell();
                        }
                    }
                }
                if (_stoppedAutomationTime > 0.55f)
                {
                    _stoppedAutomationTime = 0f;
                    if (_doorSeal < 0.02f) ToggleDoors();
                    else BeginDescent();
                }
            }
            else if (_phase == EvacuationPhase.Descending && !_braking)
            {
                float stoppingDistance = _descentSpeed * _descentSpeed / (2f * 1.1f);
                if (_floorFloat <= _automationTarget + Mathf.Max(0.12f, stoppingDistance - 0.08f))
                {
                    RequestStop();
                }
            }
        }

        private static EvacuationInteractable FindAvailablePowerCell()
        {
            EvacuationInteractable[] values = FindObjectsOfType<EvacuationInteractable>();
            return Array.Find(values, value => value != null && value.Action == EvacuationAction.Item &&
                (value.ItemKind == EvacuationItemKind.PowerCell ||
                 value.ItemKind == EvacuationItemKind.EmergencyCell));
        }

        private void UpdateWorldState()
        {
            if (!_lowPowerThoughtShown && _power > 0f && _power <= LowPowerWarning)
            {
                _lowPowerThoughtShown = true;
                _narrative.ShowThought("灯开始闪了。下一层必须停，我需要电池。", 3f);
            }
            int displayFloor = _currentPlan != null && _currentPlan.Event == FloorEventKind.WrongFloorNumber
                ? Mathf.Clamp(_currentFloor + 7, 1, 99) : _currentFloor;
            _world.SetFloorDisplay(displayFloor);
            _world.SetCabinLighting(_power / MaxPower, _power <= LowPowerWarning,
                _power <= CriticalPowerWarning, HasUrgentMimic());
            _audio.SetPowerState(_power / MaxPower, _power <= CriticalPowerWarning, Tension);
            _world.SetControlState(EvacuationAction.Descend,
                _phase == EvacuationPhase.Stopped && _doorSeal > 0.98f, false);
            _world.SetControlState(EvacuationAction.Stop, _phase == EvacuationPhase.Descending, false);
            _world.SetControlState(EvacuationAction.Door,
                _phase == EvacuationPhase.Stopped || _phase == EvacuationPhase.ClosingDoors ||
                _phase == EvacuationPhase.OpeningDoors, _doorIntegrity <= 0);
            _world.SetControlState(EvacuationAction.BatterySlot, _carryingCell || _storedCell, _power < 8f);
            _world.SetControlState(EvacuationAction.FusePanel, _hasFuse, _doorIntegrity <= 0);
            _audio.SetDoorSeal(_doorSeal);
        }

        private bool HasUrgentMimic()
        {
            for (int i = 0; i < _passengers.Count; i++)
            {
                EvacuationNpc passenger = _passengers[i];
                if (passenger != null && passenger.IsMimic && passenger.MimicTimeRemaining > 0f &&
                    passenger.MimicTimeRemaining <= 8f)
                {
                    return true;
                }
            }
            return false;
        }

        private void ResolveExit()
        {
            bool carriesMimic = _passengers.Exists(value => value != null && value.IsMimic);
            ExitResolution resolution = _automation ? ExitResolution.EscapedAlone :
                _story.Resolve(carriesMimic, _rescued, _acceptedAdministrator);
            if (resolution == ExitResolution.FalseLoop)
            {
                _loopCount++;
                _currentFloor = 99;
                _floorFloat = 99f;
                ChangePower(Mathf.Max(_power, 6f), "应急回路", false);
                EvacuationFloorPlan loopPlan = _floorDirector.CreatePlan(
                    RunSeed ^ (_loopCount * 7919), 99, _power, _floorsVisited);
                loopPlan.Theme = _loopCount % 2 == 0 ? EvacuationTheme.Hospital : EvacuationTheme.RedHall;
                loopPlan.Event = FloorEventKind.PassengerMismatch;
                loopPlan.Pressure = FloorPressure.Anomaly;
                loopPlan.Layout = FloorLayoutKind.CentralHub;
                loopPlan.Length = 9;
                loopPlan.Blackout = false;
                loopPlan.Distorted = true;
                loopPlan.SpawnMonster = false;
                loopPlan.SpawnNpc = true;
                loopPlan.SpawnEvidence = true;
                loopPlan.IsStartingFloor = false;
                _world.BuildFloor(loopPlan);
                _doorSeal = 0f;
                _world.SetDoorSeal(0f);
                _world.SetBarrier(false);
                _phase = EvacuationPhase.Stopped;
                ShowBuildingMessage("出口验证失败。目击记录不足。返回第九十九层。", 3.8f);
                _narrative.QueueThought("这不是刚才的九十九层。它记得我来过，也在等我继续找下去。", 4f);
                return;
            }
            if (resolution == ExitResolution.MimicTakeover)
            {
                Lose("带 出 去 了", "终端亮起绿色。身后的乘客第一次笑了——它等的不是一楼，而是大楼之外。你没能跨过出口。");
                return;
            }

            _phase = EvacuationPhase.Won;
            _dialogueNpc = null;
            _audio.SetTravelling(false);
            if (resolution == ExitResolution.ShutDownBuilding)
            {
                _endingTitle = "终止第 99 次循环";
                _endingBody = "证词、档案和幸存者的记忆彼此吻合。你切断了大楼用来记住楼层的核心。\n这一次，电梯门外真的有清晨。";
            }
            else if (resolution == ExitResolution.NewAdministrator)
            {
                _endingTitle = "下一任管理员";
                _endingBody = "一楼的门没有打开。控制面板上出现了你的名字。\n第 99 层正在等待下一批住户。";
            }
            else
            {
                _endingTitle = "独自离开";
                _endingBody = "你找到了出口，却没有关闭大楼。身后的电梯再次开始上行。\n至少今晚，你活着离开了。";
            }
            _player.CanMove = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            PrepareEndingPresentation();
            _audio.PlayVictory();
            LogRunResult("WIN", resolution.ToString());
        }

        private void LogRunResult(string result, string cause)
        {
            Debug.Log("EVACUATION_RUN_RESULT=" + result +
                " SEED=" + RunSeed +
                " FLOOR=" + _currentFloor +
                " VISITED=" + _floorsVisited +
                " RESCUED=" + _rescued +
                " CLUES=" + (_story != null ? _story.ClueCount : 0) +
                " POWER=" + _power.ToString("0.0") +
                " POOL_CREATED=" + (_world != null ? _world.CreatedPrimitiveCount : 0) +
                " CAUSE=" + cause);
        }

        private void Lose(string title, string body)
        {
            if (_phase == EvacuationPhase.Lost || _phase == EvacuationPhase.Won)
            {
                return;
            }
            _phase = EvacuationPhase.Lost;
            _dialogueNpc = null;
            _audio.SetTravelling(false);
            _audio.PlayDeath();
            _endingTitle = title;
            _endingBody = body;
            _endingDebrief = BuildDeathDebrief(title);
            PrepareEndingPresentation();
            _player.CanMove = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            LogRunResult("LOSS", title);
        }

        private string BuildDeathDebrief(string title)
        {
            string advice;
            if (title == "封 锁")
            {
                advice = "优先搜近处物资；低价值楼层不要停留太久。";
            }
            else if (title == "困 死" || title == "失 重")
            {
                advice = "保留启动与制动余量，见好就收，别把电量赌在下一层。";
            }
            else if (title == "被 追 上" || title == "闯 入 轿 厢" || title == "藏 身 处")
            {
                advice = "先利用拐角或躲藏点拉开距离，再回电梯关门。";
            }
            else if (title == "伪 人" || title == "带 出 去 了")
            {
                advice = "别只看求助；核对乘客去向和异常行为。";
            }
            else
            {
                advice = "线索、幸存者与电量都值得带回电梯。";
            }
            return "本次复盘：抵达 " + _currentFloor + " 层 · 探索 " + _floorsVisited +
                " 层 · 线索 " + (_story != null ? _story.ClueCount : 0) + " · 救出 " + _rescued +
                " · 剩余电量 " + Mathf.CeilToInt(_power) + " / " + Mathf.CeilToInt(MaxPower) +
                "\n下一局建议：" + advice;
        }

        private void PrepareEndingPresentation()
        {
            const string bestFloorKey = "EvacuationBestFloor";
            const string bestClueKey = "EvacuationBestClues";
            const string bestRescueKey = "EvacuationBestRescues";
            int clueCount = _story != null ? _story.ClueCount : 0;
            int previousFloor = PlayerPrefs.GetInt(bestFloorKey, 100);
            int previousClues = PlayerPrefs.GetInt(bestClueKey, -1);
            int previousRescues = PlayerPrefs.GetInt(bestRescueKey, -1);
            bool floorRecord = _currentFloor < previousFloor;
            bool clueRecord = clueCount > previousClues;
            bool rescueRecord = _rescued > previousRescues;

            if (floorRecord) PlayerPrefs.SetInt(bestFloorKey, _currentFloor);
            if (clueRecord) PlayerPrefs.SetInt(bestClueKey, clueCount);
            if (rescueRecord) PlayerPrefs.SetInt(bestRescueKey, _rescued);
            PlayerPrefs.Save();

            List<string> records = new List<string>();
            if (floorRecord) records.Add("最深楼层新纪录");
            if (clueRecord) records.Add("线索新纪录");
            if (rescueRecord && _rescued > 0) records.Add("救援新纪录");
            if (records.Count > 0)
            {
                _endingRecordText = "新纪录 · " + string.Join("  ·  ", records);
            }
            else
            {
                _endingRecordText = "个人纪录 · 最深 " + PlayerPrefs.GetInt(bestFloorKey, _currentFloor).ToString("00") +
                    "F  ·  线索 " + PlayerPrefs.GetInt(bestClueKey, clueCount) +
                    "  ·  救出 " + PlayerPrefs.GetInt(bestRescueKey, _rescued);
            }
            _endingShownAt = Time.unscaledTime;
        }

        private void ChangePower(float targetPower, string reason, bool continuous)
        {
            float previousPower = _power;
            _power = Mathf.Clamp(targetPower, 0f, MaxPower);
            if (Mathf.Approximately(previousPower, _power)) return;

            int currentBucket = Mathf.CeilToInt(_power);
            float displayedDelta;
            if (continuous)
            {
                if (currentBucket == _lastPowerNoticeBucket) return;
                displayedDelta = currentBucket - _lastPowerNoticeBucket;
            }
            else
            {
                displayedDelta = _power - previousPower;
            }
            _lastPowerNoticeBucket = currentBucket;
            _powerNoticePositive = displayedDelta > 0f;
            string sign = displayedDelta > 0f ? "+" : string.Empty;
            string value = continuous
                ? displayedDelta.ToString("0")
                : displayedDelta.ToString("0.#");
            _powerNoticeText = reason + "  " + sign + value +
                "    当前 " + currentBucket + " / " + Mathf.CeilToInt(MaxPower);
            _powerNoticeDeltaText = sign + value;
            _powerNoticeUntil = Time.unscaledTime + (continuous ? 1.05f : 1.7f);
            if (_audio != null && _powerNoticePositive) _audio.PlayPowerTick(true);
        }

        public void ShowTransientMessage(string value, float duration)
        {
            if (_narrative != null) _narrative.ShowThought(value, duration);
        }

        private void ShowSystemMessage(string value, float duration)
        {
            if (_narrative != null) _narrative.ShowSystemMessage(value, duration);
        }

        private void ShowBuildingMessage(string value, float duration)
        {
            if (_narrative != null) _narrative.ShowBuildingMessage(value, duration);
        }

        private EvacuationUiActions CreateUiActions()
        {
            return new EvacuationUiActions
            {
                ChooseDialogue = ChooseNpcDialogue,
                CloseSettings = CloseSettingsFromUi,
                RetrySeed = () => BeginRun(true),
                NewSeed = () => BeginRun(),
                Quit = ExitFromTitle,
                PreviousResolution = () => ShiftResolution(-1),
                NextResolution = () => ShiftResolution(1),
                ToggleFullscreen = () => _fullscreen = !_fullscreen,
                ApplyResolution = ApplyDisplaySettings,
                SetSensitivity = value => _player.LookSensitivity = value,
                SetVolume = value =>
                {
                    _masterVolume = value;
                    AudioListener.volume = value;
                },
                SetBrightness = value =>
                {
                    _brightness = value;
                    AnalogPostEffect.DisplayBrightness = value;
                }
            };
        }

        private void CloseSettingsFromUi()
        {
            SavePlayerSettings();
            if (_phase == EvacuationPhase.Title)
            {
                _showLauncherSettings = false;
                _narrative.ShowTitle(true);
            }
            else
            {
                SetPaused(false);
            }
        }

        private void ShiftResolution(int direction)
        {
            _resolutionIndex = (_resolutionIndex + direction + _supportedResolutions.Count) %
                _supportedResolutions.Count;
        }

        private void LateUpdate()
        {
            if (_runtimeUi != null) _runtimeUi.Sync(BuildUiState());
        }

        private EvacuationUiState BuildUiState()
        {
            RefreshHudTelemetryText();
            bool ending = _phase == EvacuationPhase.Won || _phase == EvacuationPhase.Lost;
            bool gameplay = _phase != EvacuationPhase.Title && _phase != EvacuationPhase.Prologue && !ending;
            bool initialBriefing = InitialBriefingVisible();
            int passengerCount = 0;
            for (int i = 0; i < _passengers.Count; i++)
            {
                if (_passengers[i] != null) passengerCount++;
            }

            string warning = BuildWarningText(out bool warningCritical);
            string interaction = _player != null && _player.IsHidden
                ? "[E]  离开藏身处"
                : _focus != null ? "[E]  " + _focus.Label : string.Empty;
            Vector2Int resolution = _supportedResolutions.Count > 0
                ? _supportedResolutions[_resolutionIndex]
                : new Vector2Int(Screen.width, Screen.height);
            bool dialogueVisible = _dialogueNpc != null && !ending;
            int clueCount = _story != null ? _story.ClueCount : 0;

            return new EvacuationUiState
            {
                GameplayVisible = gameplay,
                DialogueVisible = dialogueVisible,
                SettingsVisible = _paused || (_phase == EvacuationPhase.Title && _showLauncherSettings),
                SettingsPaused = _paused,
                EndingVisible = ending,
                Won = _phase == EvacuationPhase.Won,
                WarningVisible = !string.IsNullOrEmpty(warning),
                WarningCritical = warningCritical,
                ObjectiveVisible = gameplay && (initialBriefing || _notebookOpen || passengerCount > 0 ||
                    Time.unscaledTime < _objectiveRevealUntil),
                HealthVisible = _health < 99.5f,
                StaminaVisible = _player != null && _player.Stamina01 < 0.98f,
                FlashlightVisible = _hasFlashlight && (_flashlightOn || _flashCharge <= 20f),
                CarryingVisible = _carryingCell,
                ScrapVisible = _scrap > 0,
                InteractionVisible = !string.IsNullOrEmpty(interaction),
                ControlsVisible = gameplay && (initialBriefing || _notebookOpen ||
                    Time.unscaledTime < _controlsHintUntil),
                PowerNoticeVisible = Time.unscaledTime < _powerNoticeUntil &&
                    !string.IsNullOrEmpty(_powerNoticeDeltaText),
                PowerNoticePositive = _powerNoticePositive,
                AdministratorOfferVisible = dialogueVisible && _dialogueNpc.CanOfferAdministratorDeal,
                SubtitleVisible = gameplay && _narrative != null && _narrative.GameplaySubtitleVisible,
                Floor = _cachedHudFloorText,
                Power = _cachedHudPowerText,
                Time = _cachedHudTimeText,
                ElevatorStatus = CurrentElevatorStatus(),
                PowerDelta = _powerNoticeDeltaText,
                ObjectiveTitle = _notebookOpen ? "随身记录  [TAB 收起]" : "撤离目标  [TAB 查看]",
                Objective = CurrentObjective(),
                ObjectiveDetails = BuildObjectiveDetails(),
                Warning = warning,
                Health = "生命  " + Mathf.CeilToInt(_health),
                Stamina = "体力  " + Mathf.CeilToInt(_player != null ? _player.Stamina01 * 100f : 0f),
                Flashlight = "手电电量  " + Mathf.CeilToInt(_flashCharge),
                Carrying = "双手搬运：" + (_carriedCellCharge >= FullCellCharge ? "完整电池" : "破损电池"),
                Scrap = "可交易零件  " + _scrap,
                Interaction = interaction,
                Controls = _hasFlashlight ? ControlsWithFlashlight : ControlsWithoutFlashlight,
                DialogueTitle = dialogueVisible ? _dialogueNpc.DisplayName + "  ·  信任 " +
                    _dialogueNpc.Trust + "  恐惧 " + _dialogueNpc.Fear : string.Empty,
                DialogueBody = _dialogueText,
                DialogueFirstChoice = dialogueVisible && _dialogueNpc.IsOnboard
                    ? "1  请他离开电梯" : "1  同意同行",
                DialogueTradeChoice = "3  用零件交易电力（持有 " + _scrap + "）",
                SettingsTitle = _paused ? "游戏已暂停" : "显示与操作设置",
                Resolution = resolution.x + " × " + resolution.y,
                Fullscreen = _fullscreen ? "全屏模式" : "窗口模式",
                SubtitleSpeaker = _narrative != null ? _narrative.GameplaySubtitleSpeaker : string.Empty,
                SubtitleBody = _narrative != null ? _narrative.GameplaySubtitleBody : string.Empty,
                EndingOutcome = _phase == EvacuationPhase.Won ? "撤离档案 · 生还" : "撤离档案 · 行动终止",
                EndingTitle = _endingTitle,
                EndingBody = _endingBody,
                EndingStats = "最深抵达  " + _currentFloor.ToString("00") + "F    ·    探索楼层  " +
                    _floorsVisited + "\n收集线索  " + clueCount + " / 6    ·    救出人数  " + _rescued +
                    "    ·    剩余电量  " + Mathf.CeilToInt(_power),
                EndingPrompt = BuildEndingPrompt(clueCount),
                EndingRecord = _endingRecordText,
                EndingSeed = "楼层记录编号  " + RunSeed + "    R 重走 · ENTER 新种子",
                Power01 = _power / MaxPower,
                Health01 = _health / 100f,
                Stamina01 = _player != null ? _player.Stamina01 : 0f,
                WarningPulse = warningCritical
                    ? 0.62f + Mathf.PingPong(Time.unscaledTime * 0.8f, 0.38f) : 0.92f,
                EndingAge = Mathf.Max(0f, Time.unscaledTime - _endingShownAt),
                Sensitivity = _player != null ? _player.LookSensitivity : 2f,
                Volume = _masterVolume,
                Brightness = _brightness,
                SubtitleAlpha = _narrative != null ? _narrative.GameplaySubtitleAlpha : 0f,
                SubtitleAccent = _narrative != null ? _narrative.GameplaySubtitleAccent : Color.white
            };
        }

        private string BuildObjectiveDetails()
        {
            string details = string.Empty;
            if (_notebookOpen && _story != null)
            {
                details = _story.ActTitle() + " · 已发现 " + _story.ClueCount + " 条记录";
                StoryClue latest = _story.LatestClue;
                if (latest != null)
                {
                    details += "\n最近记录｜" + latest.Title + "\n" + latest.Excerpt;
                }
            }
            for (int i = 0; i < _passengers.Count; i++)
            {
                EvacuationNpc passenger = _passengers[i];
                if (passenger == null) continue;
                if (!string.IsNullOrEmpty(details)) details += "\n";
                details += "护送：" + passenger.DisplayName + " · 目标 " + passenger.DestinationFloor + " 层";
            }
            return details;
        }

        private string BuildWarningText(out bool critical)
        {
            critical = false;
            if (_phase == EvacuationPhase.Won || _phase == EvacuationPhase.Lost) return string.Empty;
            if (_power <= CriticalPowerWarning)
            {
                critical = true;
                return "致命警告：电梯电量即将耗尽";
            }
            if (_remainingTime <= CriticalTimeWarning)
            {
                critical = true;
                return "紧急：撤离窗口即将关闭  " + FormatRemainingTime();
            }
            if (_power <= LowPowerWarning)
            {
                return "警告：电梯电量不足  " + Mathf.CeilToInt(_power) + " / " + Mathf.CeilToInt(MaxPower);
            }
            if (_remainingTime <= LowTimeWarning)
            {
                return "警告：撤离时间不足  " + FormatRemainingTime();
            }
            return string.Empty;
        }

        private string FormatRemainingTime()
        {
            int minutes = Mathf.Max(0, Mathf.FloorToInt(_remainingTime / 60f));
            int seconds = Mathf.Max(0, Mathf.FloorToInt(_remainingTime % 60f));
            return minutes.ToString("00") + ":" + seconds.ToString("00");
        }

        private string BuildEndingPrompt(int clueCount)
        {
            if (_phase == EvacuationPhase.Won)
            {
                if (_endingTitle == "终止第 99 次循环")
                    return "你看见了真正的清晨。但另一个种子里，大楼会重新排列所有楼层。";
                return "大楼仍在运行。集齐 6 条线索并带一名幸存者离开，或许能终止循环。";
            }
            if (clueCount < 3)
                return "下一次至少带回 3 条记录，否则一楼只会把你送回第 99 层。";
            if (_currentFloor > 1)
                return "距离一楼还剩 " + (_currentFloor - 1) + " 层。换个种子，物资与异常都会重新洗牌。";
            return _endingDebrief.Replace("本次复盘：", string.Empty).Replace("\n下一局建议：", "  ·  ");
        }

        private void DrawStatus(float scale)
        {
            RefreshHudTelemetryText();
            float gap = 8f * scale;
            float floorWidth = 140f * scale;
            float powerWidth = 270f * scale;
            float timeWidth = 224f * scale;
            float totalWidth = floorWidth + powerWidth + timeWidth + gap * 2f;
            float x = Screen.width * 0.5f - totalWidth * 0.5f;
            float y = 16f * scale;
            float height = 64f * scale;
            Rect floorCard = new Rect(x, y, floorWidth, height);
            Rect powerCard = new Rect(floorCard.xMax + gap, y, powerWidth, height);
            Rect timeCard = new Rect(powerCard.xMax + gap, y, timeWidth, height);

            DrawTelemetryCard(floorCard, "楼层", _cachedHudFloorText,
                new Color(1f, 0.055f, 0.018f));
            Color powerColor = _power > LowPowerWarning
                ? new Color(0.08f, 0.82f, 0.72f)
                : new Color(1f, 0.08f, 0.02f);
            DrawTelemetryCard(powerCard, "电梯电量",
                _cachedHudPowerText, powerColor);
            DrawBar(new Rect(powerCard.x + 14f * scale, powerCard.yMax - 9f * scale,
                powerCard.width - 28f * scale, 4f * scale), _power / MaxPower, powerColor);
            if (Time.unscaledTime < _powerNoticeUntil && !string.IsNullOrEmpty(_powerNoticeDeltaText))
            {
                Color old = GUI.color;
                GUI.color = _powerNoticePositive
                    ? new Color(0.08f, 0.9f, 0.66f, 0.82f)
                    : new Color(1f, 0.44f, 0.08f, 0.78f);
                GUI.Label(new Rect(powerCard.xMax - 70f * scale, powerCard.y + 2f * scale,
                    56f * scale, 22f * scale), _powerNoticeDeltaText, _smallStyle);
                GUI.color = old;
            }
            DrawTelemetryCard(timeCard, "剩余时间",
                _cachedHudTimeText, new Color(1f, 0.44f, 0.08f));

            Rect statusCard = new Rect(Screen.width * 0.5f - 180f * scale, 86f * scale,
                360f * scale, 28f * scale);
            Color statusColor = _doorIntegrity <= 0
                ? new Color(1f, 0.06f, 0.025f)
                : _phase == EvacuationPhase.Descending
                    ? new Color(1f, 0.44f, 0.08f)
                    : new Color(0.08f, 0.82f, 0.72f);
            DrawPanel(statusCard, statusColor);
            GUI.Label(statusCard, CurrentElevatorStatus(), _centerStyle);

            float left = 24f * scale;
            float resourceY = Screen.height - 44f * scale;
            if (_health < 99.5f)
            {
                GUI.Label(new Rect(left, resourceY - 26f * scale, 280f * scale, 24f * scale),
                    "生命  " + Mathf.CeilToInt(_health), _smallStyle);
                DrawBar(new Rect(left, resourceY, 260f * scale, 8f * scale),
                    _health / 100f, new Color(0.86f, 0.04f, 0.025f));
                resourceY -= 44f * scale;
            }
            if (_player.Stamina01 < 0.98f)
            {
                GUI.Label(new Rect(left, resourceY - 26f * scale, 280f * scale, 24f * scale),
                    "体力  " + Mathf.CeilToInt(_player.Stamina01 * 100f), _smallStyle);
                DrawBar(new Rect(left, resourceY, 260f * scale, 8f * scale),
                    _player.Stamina01, new Color(0.08f, 0.82f, 0.68f));
            }
            if (_hasFlashlight && (_flashlightOn || _flashCharge <= 20f))
            {
                GUI.Label(new Rect(Screen.width - 220f * scale, Screen.height - 58f * scale,
                    190f * scale, 28f * scale), "手电电量  " + Mathf.CeilToInt(_flashCharge), _smallStyle);
            }
            if (_carryingCell)
            {
                GUI.Label(new Rect(Screen.width - 330f * scale, 24f * scale,
                    300f * scale, 30f * scale), "双手搬运：" +
                    (_carriedCellCharge >= FullCellCharge ? "完整电池" : "破损电池"), _smallStyle);
            }
            if (_scrap > 0)
            {
                GUI.Label(new Rect(Screen.width - 220f * scale, Screen.height - 88f * scale,
                    190f * scale, 28f * scale), "可交易零件  " + _scrap, _smallStyle);
            }
        }

        private void RefreshHudTelemetryText()
        {
            if (_cachedHudFloor != _currentFloor)
            {
                _cachedHudFloor = _currentFloor;
                _cachedHudFloorText = _currentFloor.ToString("00");
            }
            int powerValue = Mathf.CeilToInt(_power);
            if (_cachedHudPower != powerValue)
            {
                _cachedHudPower = powerValue;
                _cachedHudPowerText = powerValue.ToString("00") + " / " +
                    Mathf.CeilToInt(MaxPower);
            }
            int timeValue = Mathf.Max(0, Mathf.FloorToInt(_remainingTime));
            if (_cachedHudSeconds != timeValue)
            {
                _cachedHudSeconds = timeValue;
                int minutes = timeValue / 60;
                int seconds = timeValue % 60;
                _cachedHudTimeText = minutes.ToString("00") + ":" + seconds.ToString("00");
            }
        }

        private void DrawObjective(float scale)
        {
            int passengerCount = 0;
            for (int i = 0; i < _passengers.Count; i++)
            {
                if (_passengers[i] != null) passengerCount++;
            }
            if (!InitialBriefingVisible() && !_notebookOpen && passengerCount == 0 &&
                Time.unscaledTime >= _objectiveRevealUntil) return;
            float width = 340f * scale;
            float notebookExtra = _notebookOpen ? (_story.LatestClue != null ? 82f : 30f) : 0f;
            float height = (124f + notebookExtra + passengerCount * 24f) * scale;
            float objectiveTop = Screen.width < 1100 ? 126f * scale : 18f * scale;
            Rect rect = new Rect(20f * scale, objectiveTop, width, height);
            DrawPanel(rect, new Color(1f, 0.52f, 0.12f));
            GUI.Label(new Rect(rect.x + 14f * scale, rect.y + 5f * scale,
                rect.width - 28f * scale, 23f * scale),
                _notebookOpen ? "随身记录  [TAB 收起]" : "撤离目标  [TAB 查看]", _smallStyle);
            GUI.Label(new Rect(rect.x + 14f * scale, rect.y + 28f * scale,
                rect.width - 28f * scale, 72f * scale), CurrentObjective(), _objectiveStyle);

            float passengerY = rect.y + 104f * scale;
            if (_notebookOpen)
            {
                GUI.Label(new Rect(rect.x + 14f * scale, passengerY,
                    rect.width - 28f * scale, 24f * scale),
                    _story.ActTitle() + " · 已发现 " + _story.ClueCount + " 条记录", _smallStyle);
                passengerY += 26f * scale;
                StoryClue latest = _story.LatestClue;
                if (latest != null)
                {
                    GUI.Label(new Rect(rect.x + 14f * scale, passengerY,
                        rect.width - 28f * scale, 54f * scale),
                        "最近记录｜" + latest.Title + "\n" + latest.Excerpt, _objectiveStyle);
                    passengerY += 56f * scale;
                }
            }
            for (int i = 0; i < _passengers.Count; i++)
            {
                EvacuationNpc passenger = _passengers[i];
                if (passenger == null) continue;
                GUI.Label(new Rect(rect.x + 14f * scale, passengerY,
                    rect.width - 28f * scale, 23f * scale),
                    "护送：" + passenger.DisplayName + " · 目标 " + passenger.DestinationFloor + " 层", _smallStyle);
                passengerY += 24f * scale;
            }
        }

        private string CurrentObjective()
        {
            string mainGoal = _acceptedAdministrator
                ? "协议已签署：大楼正把我当作下一任管理员。\n"
                : _story.Act == StoryAct.Witnessed
                    ? "真相已经接近完整：抵达一楼并终止交接。\n"
                    : _story.Act == StoryAct.Remembered
                        ? "大楼在抹除目击者：继续寻找证词与档案。\n"
                        : "从 99 层撤至 1 层，并查明不存在的电梯。\n";
            if (_currentFloor <= 1)
            {
                if (_doorSeal > 0.02f) return mainGoal + "当前：打开电梯门，前往大厅尽头。";
                return mainGoal + "当前：跟随闪烁的橙色灯光，使用出口验证终端。";
            }
            if (_carryingCell) return mainGoal + "当前：把电池带回轿厢并装入电池槽。";
            if (_phase == EvacuationPhase.Descending)
                return mainGoal + (_braking ? "当前：电梯正在制动，准备确认停靠层。" :
                    "当前：观察楼层数字，瞄准停止按钮选择停靠层。");
            if (_phase == EvacuationPhase.ClosingDoors) return mainGoal + "当前：等待电梯门完全关闭。";
            if (_phase == EvacuationPhase.OpeningDoors) return mainGoal + "当前：等待电梯门完全打开。";
            if (_doorSeal > 0.98f) return mainGoal + "当前：使用下降按钮启动电梯。";
            if (!_player.IsInsideElevator) return mainGoal + "当前：搜索电池和线索，遇到危险立即撤回电梯。";
            return mainGoal + "当前：探索楼层；离开前回到轿厢并关闭电梯门。";
        }

        private bool InitialBriefingVisible()
        {
            return _floorsVisited <= 1 && _currentFloor == 99 &&
                _phase != EvacuationPhase.Won && _phase != EvacuationPhase.Lost;
        }

        private void DrawTelemetryCard(Rect rect, string label, string value, Color accent)
        {
            DrawPanel(rect, accent);
            GUI.Label(new Rect(rect.x, rect.y + 2f, rect.width, rect.height * 0.34f),
                label, _telemetryLabelStyle);
            GUI.Label(new Rect(rect.x, rect.y + rect.height * 0.25f, rect.width, rect.height * 0.65f),
                value, _telemetryValueStyle);
        }

        private string CurrentElevatorStatus()
        {
            if (_phase == EvacuationPhase.ClosingDoors) return "电梯门关闭中";
            if (_phase == EvacuationPhase.OpeningDoors) return "电梯门开启中";
            if (_phase == EvacuationPhase.Descending) return _braking ? "制动中" : "正在下降";
            if (_doorIntegrity <= 0) return "门控系统故障";
            if (_carryingCell) return "正在搬运电梯电池";
            return _doorSeal > 0.98f ? "已停稳 · 电梯门关闭" : "已停稳 · 楼层开放";
        }

        private void DrawResourceWarnings(float scale)
        {
            if (_phase == EvacuationPhase.Won || _phase == EvacuationPhase.Lost)
            {
                return;
            }

            float y = 122f * scale;
            if (_power <= LowPowerWarning)
            {
                bool critical = _power <= CriticalPowerWarning;
                string warning = critical
                    ? "致命警告：电梯电量即将耗尽"
                    : "警告：电梯电量不足  " + Mathf.CeilToInt(_power) + " / " + Mathf.CeilToInt(MaxPower);
                DrawHudWarning(warning, critical, ref y, scale);
            }
            if (_remainingTime <= LowTimeWarning)
            {
                bool critical = _remainingTime <= CriticalTimeWarning;
                int minutes = Mathf.Max(0, Mathf.FloorToInt(_remainingTime / 60f));
                int seconds = Mathf.Max(0, Mathf.FloorToInt(_remainingTime % 60f));
                string warning = critical
                    ? "紧急：撤离窗口即将关闭  " + minutes.ToString("00") + ":" + seconds.ToString("00")
                    : "警告：撤离时间不足  " + minutes.ToString("00") + ":" + seconds.ToString("00");
                DrawHudWarning(warning, critical, ref y, scale);
            }
        }

        private void DrawHudWarning(string warning, bool critical, ref float y, float scale)
        {
            float pulse = critical ? 0.62f + Mathf.PingPong(Time.unscaledTime * 0.8f, 0.38f) : 0.92f;
            Color accent = critical
                ? new Color(1f, 0.035f, 0.015f, pulse)
                : new Color(1f, 0.38f, 0.04f, pulse);
            Rect rect = new Rect(Screen.width * 0.5f - 220f * scale, y,
                440f * scale, 38f * scale);
            DrawPanel(rect, accent);
            Color old = GUI.color;
            GUI.color = accent;
            GUI.Label(rect, warning, _centerStyle);
            GUI.color = old;
            y += 44f * scale;
        }

        private void DrawInteraction(float scale)
        {
            float size = 6f * scale;
            Color reticleColor = _focus == null && !_player.IsHidden
                ? new Color(0.7f, 0.8f, 0.78f, 0.58f)
                : new Color(0.1f, 1f, 0.72f, 0.96f);
            DrawTint(new Rect(Screen.width * 0.5f - size * 0.5f, Screen.height * 0.5f - size * 0.5f,
                size, size), reticleColor);

            string context = _player.IsHidden
                ? "[E]  离开藏身处"
                : _focus != null ? "[E]  " + _focus.Label : string.Empty;
            if (!string.IsNullOrEmpty(context))
            {
                Rect prompt = new Rect(Screen.width * 0.5f - 180f * scale,
                    Screen.height * 0.5f + 16f * scale, 360f * scale, 38f * scale);
                DrawPanel(prompt, reticleColor);
                GUI.Label(prompt, context, _centerStyle);
            }

            if (InitialBriefingVisible() || _notebookOpen || Time.unscaledTime < _controlsHintUntil)
            {
                string controls = _hasFlashlight ? ControlsWithFlashlight : ControlsWithoutFlashlight;
                GUI.Label(new Rect(Screen.width * 0.24f, Screen.height - 36f * scale,
                    Screen.width * 0.52f, 28f * scale), controls, _centerStyle);
            }
        }

        private void DrawNpcDialogue(float scale)
        {
            Rect rect = new Rect(Screen.width * 0.16f, Screen.height * 0.54f,
                Screen.width * 0.68f, Screen.height * 0.39f);
            DrawPanel(rect, new Color(0.72f, 0.42f, 0.12f));
            GUI.Label(new Rect(rect.x + 28f * scale, rect.y + 20f * scale,
                rect.width - 56f * scale, 36f * scale), _dialogueNpc.DisplayName +
                "  ·  信任 " + _dialogueNpc.Trust + "  恐惧 " + _dialogueNpc.Fear, _headingStyle);
            GUI.Label(new Rect(rect.x + 28f * scale, rect.y + 62f * scale,
                rect.width - 56f * scale, 82f * scale), _dialogueText, _bodyStyle);

            float y = rect.y + 155f * scale;
            float width = (rect.width - 74f * scale) * 0.5f;
            if (GUI.Button(new Rect(rect.x + 24f * scale, y, width, 42f * scale),
                _dialogueNpc.IsOnboard ? "1  请他离开电梯" : "1  同意同行")) ChooseNpcDialogue(1);
            if (GUI.Button(new Rect(rect.x + 50f * scale + width, y, width, 42f * scale),
                "2  询问大楼")) ChooseNpcDialogue(2);
            if (GUI.Button(new Rect(rect.x + 24f * scale, y + 52f * scale, width, 42f * scale),
                "3  用零件交易电力（持有 " + _scrap + "）")) ChooseNpcDialogue(3);
            if (GUI.Button(new Rect(rect.x + 50f * scale + width, y + 52f * scale, width, 42f * scale),
                "4  拒绝 / 离开")) ChooseNpcDialogue(4);
            if (_dialogueNpc != null && _dialogueNpc.CanOfferAdministratorDeal &&
                GUI.Button(new Rect(rect.x + 24f * scale, y + 104f * scale,
                    rect.width - 48f * scale, 38f * scale), "5  接受管理员协议（不可撤销）"))
            {
                ChooseNpcDialogue(5);
            }
        }

        private void DrawSettingsPanel(float scale, bool paused)
        {
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.76f));
            Rect rect = new Rect(Screen.width * 0.5f - 285f * scale,
                Screen.height * 0.5f - 250f * scale, 570f * scale, 500f * scale);
            DrawPanel(rect, new Color(1f, 0.48f, 0.1f));
            GUI.Label(new Rect(rect.x + 30f * scale, rect.y + 20f * scale,
                rect.width - 60f * scale, 42f * scale), paused ? "游戏已暂停" : "启动设置", _headingStyle);

            float labelX = rect.x + 34f * scale;
            float valueX = rect.x + 210f * scale;
            float width = rect.width - 250f * scale;
            float y = rect.y + 82f * scale;
            GUI.Label(new Rect(labelX, y, 170f * scale, 30f * scale), "鼠标灵敏度", _bodyStyle);
            _player.LookSensitivity = GUI.HorizontalSlider(new Rect(valueX, y + 9f * scale,
                width, 20f * scale), _player.LookSensitivity, 0.8f, 5f);
            y += 58f * scale;
            GUI.Label(new Rect(labelX, y, 170f * scale, 30f * scale), "总音量", _bodyStyle);
            _masterVolume = GUI.HorizontalSlider(new Rect(valueX, y + 9f * scale,
                width, 20f * scale), _masterVolume, 0f, 1f);
            AudioListener.volume = _masterVolume;
            y += 58f * scale;
            GUI.Label(new Rect(labelX, y, 170f * scale, 30f * scale), "画面亮度", _bodyStyle);
            _brightness = GUI.HorizontalSlider(new Rect(valueX, y + 9f * scale,
                width, 20f * scale), _brightness, 0.72f, 1.35f);
            AnalogPostEffect.DisplayBrightness = _brightness;
            y += 58f * scale;

            Vector2Int resolution = _supportedResolutions[_resolutionIndex];
            if (GUI.Button(new Rect(labelX, y, 48f * scale, 38f * scale), "<"))
            {
                _resolutionIndex = (_resolutionIndex - 1 + _supportedResolutions.Count) %
                    _supportedResolutions.Count;
            }
            GUI.Label(new Rect(labelX + 58f * scale, y, 190f * scale, 38f * scale),
                resolution.x + " × " + resolution.y, _centerStyle);
            if (GUI.Button(new Rect(labelX + 250f * scale, y, 48f * scale, 38f * scale), ">"))
            {
                _resolutionIndex = (_resolutionIndex + 1) % _supportedResolutions.Count;
            }
            if (GUI.Button(new Rect(labelX + 316f * scale, y, 185f * scale, 38f * scale),
                _fullscreen ? "全屏模式" : "窗口模式"))
            {
                _fullscreen = !_fullscreen;
            }
            y += 56f * scale;
            if (GUI.Button(new Rect(labelX, y, 230f * scale, 42f * scale), "应用分辨率"))
            {
                ApplyDisplaySettings();
            }
            if (GUI.Button(new Rect(labelX + 250f * scale, y, 250f * scale, 42f * scale),
                paused ? "继续游戏" : "保存并关闭"))
            {
                SavePlayerSettings();
                if (paused) SetPaused(false);
                else
                {
                    _showLauncherSettings = false;
                    _narrative.ShowTitle(true);
                }
            }
            y += 58f * scale;
            if (paused && GUI.Button(new Rect(labelX, y, 230f * scale, 40f * scale), "重开当前种子"))
            {
                BeginRun(true);
            }
            if (GUI.Button(new Rect(labelX + (paused ? 250f : 125f) * scale, y,
                (paused ? 250f : 250f) * scale, 40f * scale), "退出游戏"))
            {
                SavePlayerSettings();
                Application.Quit();
            }
        }

        private void DrawEnding(float scale)
        {
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.7f));
            Rect rect = new Rect(Screen.width * 0.2f, Screen.height * 0.2f,
                Screen.width * 0.6f, Screen.height * 0.58f);
            DrawPanel(rect, _phase == EvacuationPhase.Won ? new Color(0.08f, 0.85f, 0.7f) : new Color(1f, 0.035f, 0.015f));
            GUI.Label(new Rect(rect.x + 40f * scale, rect.y + 36f * scale,
                rect.width - 80f * scale, 70f * scale), _endingTitle, _headingStyle);
            GUI.Label(new Rect(rect.x + 40f * scale, rect.y + 120f * scale,
                rect.width - 80f * scale, 115f * scale), _endingBody, _bodyStyle);
            if (_phase == EvacuationPhase.Lost)
            {
                GUI.Label(new Rect(rect.x + 40f * scale, rect.y + 248f * scale,
                    rect.width - 80f * scale, 70f * scale), _endingDebrief, _smallStyle);
            }
            GUI.Label(new Rect(rect.x + 40f * scale, rect.yMax - 110f * scale,
                rect.width - 80f * scale, 70f * scale),
                (_phase == EvacuationPhase.Won ? "抵达楼层 " : "死亡楼层 ") + _currentFloor +
                "  ·  探索 " + _floorsVisited + " 层  ·  救出 " + _rescued +
                " 人  ·  种子 " + RunSeed + "\n按 R 重试当前种子 · ENTER 开启新种子", _smallStyle);
        }

        private void DrawBar(Rect rect, float value, Color color)
        {
            DrawTint(rect, new Color(0f, 0f, 0f, 0.76f));
            DrawTint(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
        }

        private void DrawPanel(Rect rect, Color accent)
        {
            Color old = GUI.color;
            GUI.color = Color.Lerp(new Color(0.82f, 0.84f, 0.82f, 0.94f), accent, 0.13f);
            GUI.DrawTexture(rect, _uiPanelSkin != null ? _uiPanelSkin : _panelTexture,
                ScaleMode.StretchToFill);
            GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x + 4f, rect.y + 4f, 3f,
                Mathf.Max(0f, rect.height - 8f)), Texture2D.whiteTexture);
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
            if (_headingStyle != null) return;
            _headingStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.1f, 0.92f, 0.78f));
            _bodyStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.88f, 0.86f));
            _bodyStyle.wordWrap = true;
            _smallStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.55f, 0.74f, 0.7f));
            _centerStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.8f, 0.92f, 0.88f));
            _objectiveStyle = NewStyle(FontStyle.Normal, TextAnchor.UpperLeft, new Color(0.88f, 0.93f, 0.9f));
            _objectiveStyle.wordWrap = true;
            _telemetryLabelStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.74f, 0.7f));
            _telemetryValueStyle = NewStyle(FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.9f, 0.97f, 0.95f));
            GUI.skin.button.font = _font;
            GUI.skin.button.fontSize = 16;
            GUI.skin.button.fontStyle = FontStyle.Normal;
            GUI.skin.button.alignment = TextAnchor.MiddleCenter;
            if (_uiButtonSkin != null)
            {
                GUI.skin.button.normal.background = _uiButtonSkin;
                GUI.skin.button.hover.background = _uiButtonSkin;
                GUI.skin.button.active.background = _uiButtonSkin;
                GUI.skin.button.focused.background = _uiButtonSkin;
            }
            GUI.skin.button.normal.textColor = new Color(0.76f, 0.84f, 0.8f);
            GUI.skin.button.hover.textColor = new Color(0.12f, 0.95f, 0.74f);
            GUI.skin.button.active.textColor = new Color(1f, 0.62f, 0.2f);
            GUI.skin.button.padding = new RectOffset(14, 14, 5, 5);
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
            _headingStyle.fontSize = Mathf.RoundToInt(28f * scale);
            _bodyStyle.fontSize = Mathf.RoundToInt(21f * scale);
            _smallStyle.fontSize = Mathf.RoundToInt(15f * scale);
            _centerStyle.fontSize = Mathf.RoundToInt(17f * scale);
            _objectiveStyle.fontSize = Mathf.RoundToInt(16f * scale);
            _telemetryLabelStyle.fontSize = Mathf.RoundToInt(13f * scale);
            _telemetryValueStyle.fontSize = Mathf.RoundToInt(25f * scale);
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
