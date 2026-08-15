using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public enum EvacuationPhase
    {
        Title,
        Stopped,
        ClosingDoors,
        Descending,
        OpeningDoors,
        Won,
        Lost
    }

    public sealed class NinetyNineEvacuationGame : MonoBehaviour
    {
        private const float MaxPower = 26f;
        private const float StartCost = 4f;
        private const float TravelCostPerFloor = 0.9f;
        private const float IdleDrain = 0.04f;
        private const float RunDuration = 1800f;
        private const float DoorCloseDuration = 3.4f;
        private const float DoorOpenDuration = 2.8f;
        private const float MaxDescentSpeed = 0.55f;
        private const float MonsterRepelCost = 2.5f;

        private readonly List<EvacuationNpc> _passengers = new List<EvacuationNpc>();
        private readonly Collider[] _interactionHits = new Collider[32];
        private readonly RaycastHit[] _interactionSightHits = new RaycastHit[16];
        private EvacuationFloorGenerator _world;
        private FirstPersonController _player;
        private EvacuationAudio _audio;
        private EvacuationFloorDirector _floorDirector;
        private EvacuationStorySystem _story;
        private EvacuationFloorPlan _currentPlan;
        private EvacuationNpc _dialogueNpc;
        private EvacuationInteractable _focus;
        private Texture2D _titleBackground;
        private Texture2D _panelTexture;
        private Font _font;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _centerStyle;
        private EvacuationPhase _phase = EvacuationPhase.Title;
        private float _power;
        private float _remainingTime;
        private float _health;
        private float _floorFloat;
        private float _descentSpeed;
        private float _brakeTimer;
        private float _doorSeal;
        private float _messageUntil;
        private float _slowUntil;
        private float _stimulantUntil;
        private float _flashCharge;
        private float _mimicCountdown;
        private float _stoppedAutomationTime;
        private int _currentFloor;
        private int _doorIntegrity;
        private int _floorsVisited;
        private int _rescued;
        private int _automationTarget;
        private int _automationWorstStopError;
        private int _scrap;
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
        private string _message = string.Empty;
        private string _dialogueText = string.Empty;
        private string _endingTitle = string.Empty;
        private string _endingBody = string.Empty;
        private float _floorMovementPenalty = 1f;

        public int RunSeed { get; private set; }
        public float Power => _power;
        public float DoorSeal => _doorSeal;
        public bool IsFlashlightOn => _flashlightOn;
        public bool IsDescending => _phase == EvacuationPhase.Descending;
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
            _titleBackground = Resources.Load<Texture2D>("Art/title_hall");
            _panelTexture = MakeTexture(new Color(0.004f, 0.009f, 0.01f, 0.94f));
            _font = CreateGameFont();
            _floorDirector = new EvacuationFloorDirector();
            _story = new EvacuationStorySystem();
            _audio = gameObject.AddComponent<EvacuationAudio>();
            _world = gameObject.AddComponent<EvacuationFloorGenerator>();
            _world.Initialize(this, _audio);
            _player = _world.Player;
            ShowTitle();

#if UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            bool capture = Array.Exists(args, value => value == "-evacuationCapture");
            bool fullRun = Array.Exists(args, value => value == "-evacuationFullRun");
            bool failureRun = Array.Exists(args, value => value == "-evacuationFailureRun");
            bool monsterRun = Array.Exists(args, value => value == "-evacuationMonsterRun");
            if (capture || fullRun || failureRun || monsterRun)
            {
                StartCoroutine(CapturePrototype(fullRun, failureRun, monsterRun));
            }
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CapturePrototype(bool fullRun, bool failureRun, bool monsterRun)
        {
            yield return new WaitForSeconds(1.1f);
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string captureRoot = System.IO.Path.Combine(projectRoot, "Logs", "Captures");
            System.IO.Directory.CreateDirectory(captureRoot);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationTitle.png"));
            yield return new WaitForSeconds(0.35f);
            BeginRun();
            yield return new WaitForSeconds(3f);
            _player.transform.rotation = Quaternion.Euler(0f, -68f, 0f);
            _messageUntil = 0f;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationCabin.png"));
            _player.transform.rotation = Quaternion.identity;
            _player.transform.position = new Vector3(0f, 0.08f, 3.2f);
            _world.SetFlashlight(true, 100f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "EvacuationFloor.png"));
            _world.SetFlashlight(false, 100f);
            _player.ResetInsideCabin();
            if (!fullRun && !failureRun && !monsterRun)
            {
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
                Debug.Log("EVACUATION_FAILURE_TEST=PASS");
                yield break;
            }

            bool safeStartPassed = _world.Monster == null &&
                FindObjectsOfType<EvacuationNpc>().Length == 1;
            Debug.Log("EVACUATION_START_FLOOR_SAFE_TEST=" + (safeStartPassed ? "PASS" : "FAIL"));
            EvacuationNpc startNpc = FindObjectOfType<EvacuationNpc>();
            Collider npcCollider = startNpc != null ? startNpc.GetComponent<Collider>() : null;
            CharacterController playerController = _player.GetComponent<CharacterController>();
            bool npcCollisionPassed = npcCollider != null && playerController != null &&
                Physics.GetIgnoreCollision(npcCollider, playerController);
            Debug.Log("EVACUATION_NPC_COLLISION_TEST=" + (npcCollisionPassed ? "PASS" : "FAIL"));
            EvacuationInteractable[] interactables = FindObjectsOfType<EvacuationInteractable>();
            bool pickupHitboxPassed = Array.Exists(interactables, value => value != null &&
                value.Action == EvacuationAction.Item && value.GetComponent<SphereCollider>() != null &&
                value.GetComponent<SphereCollider>().isTrigger);
            Debug.Log("EVACUATION_PICKUP_HITBOX_TEST=" + (pickupHitboxPassed ? "PASS" : "FAIL"));
            EvacuationInteractable starterPickup = Array.Find(interactables, value => value != null &&
                value.Action == EvacuationAction.Item);
            bool pointBlankInteractionPassed = false;
            if (starterPickup != null && playerController != null)
            {
                playerController.enabled = false;
                _player.transform.position = new Vector3(starterPickup.transform.position.x, 0.08f,
                    starterPickup.transform.position.z - 0.08f);
                _player.transform.rotation = Quaternion.identity;
                playerController.enabled = true;
                Physics.SyncTransforms();
                EvacuationInteractable pointBlankFocus = FindInteractionFocus(_player.ViewCamera);
                pointBlankInteractionPassed = pointBlankFocus == starterPickup;
                Debug.Log("EVACUATION_POINT_BLANK_FOCUS=" +
                    (pointBlankFocus != null ? pointBlankFocus.name : "NULL"));
                _player.ResetInsideCabin();
            }
            Debug.Log("EVACUATION_POINT_BLANK_INTERACTION_TEST=" +
                (pointBlankInteractionPassed ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_YAW_720_TEST=" + (_player.VerifyUnclampedYaw() ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_CROUCH_TOGGLE_TEST=" + (_player.VerifyCrouchToggle() ? "PASS" : "FAIL"));
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
            ToggleDoors();
            while (_phase == EvacuationPhase.OpeningDoors)
            {
                yield return null;
            }
            _power = 19f;

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
                    _messageUntil = 0f;
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
            Debug.Log("EVACUATION_SPEED_TEST=" + (observedMaxSpeed <= MaxDescentSpeed + 0.01f ? "PASS" : "FAIL") +
                " MAX=" + observedMaxSpeed.ToString("0.00"));
            Debug.Log("EVACUATION_DOOR_OPEN_TEST=" + (observedOpeningAnimation ? "PASS" : "FAIL"));
            Debug.Log("EVACUATION_STOP_ACCURACY_TEST=" + (_automationWorstStopError <= 1 ? "PASS" : "FAIL") +
                " WORST=" + _automationWorstStopError);
            Debug.Log("EVACUATION_FULL_RUN=" + (_phase == EvacuationPhase.Won ? "PASS" : "FAIL"));
        }
#endif

        private void Update()
        {
            if (_phase == EvacuationPhase.Title)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    BeginRun();
                }
                return;
            }
            if (_phase == EvacuationPhase.Won || _phase == EvacuationPhase.Lost)
            {
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return))
                {
                    BeginRun();
                }
                return;
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
                UpdateNpcDialogueInput();
                UpdatePassengers();
                UpdateWorldState();
                return;
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
            _phase = EvacuationPhase.Title;
            _player.CanMove = false;
            _player.ResetInsideCabin();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void BeginRun()
        {
            RunSeed = unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond;
            foreach (EvacuationNpc passenger in _passengers)
            {
                if (passenger != null) Destroy(passenger.gameObject);
            }
            _passengers.Clear();
            _phase = EvacuationPhase.Stopped;
            _currentFloor = 99;
            _floorFloat = 99f;
            _power = 19f;
            _remainingTime = RunDuration;
            _health = 100f;
            _doorIntegrity = 2;
            _doorSeal = 0f;
            _descentSpeed = 0f;
            _braking = false;
            _hasFlashlight = true;
            _flashCharge = 42f;
            _flashlightOn = false;
            _carryingCell = false;
            _storedCell = false;
            _hasFuse = false;
            _floorsVisited = 1;
            _rescued = 0;
            _scrap = 0;
            _automationWorstStopError = 0;
            _mimicCountdown = 0f;
            _automation = false;
            _captureThreat = false;
            _acceptedAdministrator = false;
            _dialogueNpc = null;
            _dialogueText = string.Empty;
            _automationVisitedFloor = false;
            _story.Reset();
            EvacuationSignals.Clear();
            _world.BuildFloor(_floorDirector.CreatePlan(RunSeed, 99, _power, _floorsVisited));
            _world.SetDoorSeal(0f);
            _world.SetBarrier(false);
            _player.ResetInsideCabin();
            _player.CanMove = true;
            _player.SpeedMultiplier = 1f;
            _audio.SetTravelling(false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            ShowTransientMessage("从 99 层撤离。电量只能支撑一小段下降。", 4f);
        }

        private void UpdateStopped()
        {
            _power = Mathf.Max(0f, _power - IdleDrain * Time.deltaTime);
            if (_power <= 0f)
            {
                Lose("困 死", "停靠电力耗尽。门机和照明停止工作，楼层里的脚步仍在靠近。");
            }
        }

        private void BeginDescent()
        {
            if (_phase != EvacuationPhase.Stopped || !_player.IsInsideElevator)
            {
                ShowTransientMessage("必须回到电梯内才能启动下降。", 1.5f);
                return;
            }
            if (_doorSeal < 0.999f)
            {
                ShowTransientMessage("必须先使用门控关闭电梯门。", 1.8f);
                return;
            }
            if (_power <= StartCost + 0.05f)
            {
                ShowTransientMessage("电量不足以启动电机。停车本身不再消耗电力。", 2f);
                return;
            }
            _power -= StartCost;
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
            ShowTransientMessage("电机启动。需要停车时使用另一侧制停按钮。", 1.7f);
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
            ShowTransientMessage("门已关闭。现在可以启动下降。", 1.4f);
        }

        private void ToggleDoors()
        {
            if (_phase == EvacuationPhase.Descending)
            {
                ShowTransientMessage("电梯仍在运行，必须先停车。", 1.4f);
                return;
            }
            if (_phase == EvacuationPhase.ClosingDoors || _phase == EvacuationPhase.OpeningDoors)
            {
                ShowTransientMessage("门机正在动作。", 0.8f);
                return;
            }
            if (_doorSeal >= 0.98f)
            {
                _phase = EvacuationPhase.OpeningDoors;
                _audio.PlayDoor();
                EvacuationSignals.Emit(_player.transform.position, 8f, NoiseKind.Door);
                ShowTransientMessage("电梯门正在打开。", 1.1f);
                return;
            }
            if (_doorIntegrity <= 0)
            {
                ShowTransientMessage("门机已经损坏。", 1.2f);
                return;
            }
            _phase = EvacuationPhase.ClosingDoors;
            _audio.PlayDoor();
            EvacuationSignals.Emit(_player.transform.position, 10f, NoiseKind.Door);
            ShowTransientMessage("门控已启动。关门不会自动启动电梯。", 1.5f);
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
                _power = Mathf.Max(0f, _power - Mathf.Max(0f, previous - _floorFloat) * TravelCostPerFloor);
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
            ShowTransientMessage("机械制动已接合。停车不消耗电力。", 1.3f);
        }

        private void CompleteStop()
        {
            _currentFloor = Mathf.Clamp(Mathf.CeilToInt(_floorFloat), 1, 99);
            _floorFloat = _currentFloor;
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
            _world.BuildFloor(_floorDirector.CreatePlan(RunSeed, _currentFloor, _power, _floorsVisited));
            _world.SetDoorSeal(1f);
            _world.SetBarrier(true);
            if (_currentFloor > 1)
            {
                _floorsVisited++;
            }
            _automationVisitedFloor = false;
            ShowTransientMessage("电梯已停稳。使用门控打开门。", 1.6f);
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
                ResolveExit();
                return;
            }
            _phase = EvacuationPhase.Stopped;
            ShowTransientMessage("抵达 " + _currentFloor + " 层。门外没有任何预警。", 2f);
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
                        ShowTransientMessage("你屏住呼吸躲了进去。按 E 离开。", 1.5f);
                    }
                    break;
                case EvacuationAction.Evidence:
                    CollectEvidence(_focus);
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
            float bestScore = float.MaxValue;
            int count = Physics.OverlapSphereNonAlloc(origin, 1.65f, _interactionHits,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            EvaluateInteractionHits(origin, forward, count, true, ref best, ref bestScore);

            count = Physics.OverlapCapsuleNonAlloc(origin, origin + forward * 5.2f, 0.32f,
                _interactionHits, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            EvaluateInteractionHits(origin, forward, count, false, ref best, ref bestScore);
            return best;
        }

        private void EvaluateInteractionHits(Vector3 origin, Vector3 forward, int count, bool nearby,
            ref EvacuationInteractable best, ref float bestScore)
        {
            for (int i = 0; i < count; i++)
            {
                Collider targetCollider = _interactionHits[i];
                if (targetCollider == null)
                {
                    continue;
                }
                EvacuationInteractable candidate =
                    targetCollider.GetComponentInParent<EvacuationInteractable>();
                if (candidate == null)
                {
                    continue;
                }

                Vector3 target = targetCollider.bounds.center;
                Vector3 offset = target - origin;
                float distance = offset.magnitude;
                float facing = distance > 0.001f ? Vector3.Dot(forward, offset / distance) : 1f;
                if (!nearby && facing < 0.82f)
                {
                    continue;
                }
                if (nearby && facing < -0.2f && distance > 0.45f)
                {
                    continue;
                }

                if (!HasInteractionLineOfSight(origin, target, targetCollider, candidate))
                {
                    continue;
                }

                float score = distance + (1f - facing) * (nearby ? 0.28f : 0.8f);
                if (score < bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }
        }

        private bool HasInteractionLineOfSight(Vector3 origin, Vector3 target,
            Collider targetCollider, EvacuationInteractable candidate)
        {
            Vector3 offset = target - origin;
            float distance = offset.magnitude;
            if (distance <= 0.001f || targetCollider.bounds.Contains(origin))
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(origin, offset / distance, _interactionSightHits,
                distance + 0.05f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            float candidateDistance = float.MaxValue;
            float obstructionDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _interactionSightHits[i].collider;
                if (hitCollider == null || hitCollider.GetComponentInParent<FirstPersonController>() == _player)
                {
                    continue;
                }
                EvacuationInteractable hitInteractable =
                    hitCollider.GetComponentInParent<EvacuationInteractable>();
                if (hitInteractable == candidate)
                {
                    candidateDistance = Mathf.Min(candidateDistance, _interactionSightHits[i].distance);
                }
                else if (!hitCollider.isTrigger || hitInteractable != null)
                {
                    obstructionDistance = Mathf.Min(obstructionDistance, _interactionSightHits[i].distance);
                }
            }
            return candidateDistance < float.MaxValue && candidateDistance <= obstructionDistance + 0.01f;
        }

        private void CollectItem(EvacuationInteractable item)
        {
            switch (item.ItemKind)
            {
                case EvacuationItemKind.PowerCell:
                    if (_carryingCell)
                    {
                        ShowTransientMessage("双手已经抱着一块电梯电池。", 1.3f);
                        return;
                    }
                    _carryingCell = true;
                    ShowTransientMessage("电池很重。冲刺消耗增加。", 1.5f);
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
            Destroy(item.gameObject);
        }

        private void InstallPowerCell()
        {
            if (_carryingCell)
            {
                if (_power < MaxPower - 0.5f)
                {
                    _power = Mathf.Min(MaxPower, _power + 12f);
                    _carryingCell = false;
                    _audio.PlayPickup();
                    ShowTransientMessage("电池已安装。当前电量 " + Mathf.CeilToInt(_power) + " / 26。", 1.6f);
                }
                else if (!_storedCell)
                {
                    _storedCell = true;
                    _carryingCell = false;
                    ShowTransientMessage("备用架只能存放这一块电池。", 1.4f);
                }
                else
                {
                    ShowTransientMessage("电池已满，备用架也被占用。", 1.4f);
                }
                return;
            }
            if (_storedCell && _power < MaxPower - 0.5f)
            {
                _storedCell = false;
                _power = Mathf.Min(MaxPower, _power + 12f);
                _audio.PlayPickup();
                ShowTransientMessage("备用电池已接入。", 1.2f);
                return;
            }
            ShowTransientMessage("没有可安装的电池。", 1.1f);
        }

        private void InstallFuse()
        {
            if (!_hasFuse)
            {
                ShowTransientMessage("缺少保险丝。", 1.1f);
                return;
            }
            _hasFuse = false;
            _doorIntegrity = 2;
            _power = Mathf.Min(MaxPower, _power + 3f);
            _audio.PlayPickup();
            ShowTransientMessage("门机恢复，备用线路返还 3 点电力。", 1.5f);
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
            ShowTransientMessage("攻击让你的视野失焦。它停顿了一瞬——快跑！", 2.2f);
            if (_health <= 0f)
            {
                Lose("被 追 上", "最后一次攻击后，你再也没能站起来。电梯门一直开着。");
            }
        }

        public void MonsterEnteredElevator()
        {
            Lose("闯 入 轿 厢", "门只差最后一掌宽。它侧身挤了进来，下降键不再有意义。");
        }

        public void RepelMonster(EvacuationMonster monster)
        {
            if (_phase != EvacuationPhase.Stopped && _phase != EvacuationPhase.ClosingDoors)
            {
                return;
            }
            _power = Mathf.Max(0.01f, _power - MonsterRepelCost);
            _world.RemoveMonster(monster);
            _audio.PlayDoor();
            ShowTransientMessage("门机过载阻挡了它：-2.5 电力。", 2f);
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
                _mimicCountdown = UnityEngine.Random.Range(24f, 42f);
            }
            ShowTransientMessage("乘客已进入电梯。目的地 " + npc.DestinationFloor + " 层。", 1.7f);
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
            Destroy(npc.gameObject);
            if (mimic) _mimicCountdown = 0f;
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
                if (_story.Discover(clueId))
                {
                    _dialogueText += "\n[获得一条证词]";
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
                    _power = Mathf.Min(MaxPower, _power + 3.5f);
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
                _acceptedAdministrator = true;
                _dialogueText = "你在没有文字的协议上按下手印。电梯播报：管理员权限已转移。";
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
            bool discovered = _story.Discover(evidence.EvidenceId);
            ShowTransientMessage(discovered ? "档案记录与已知楼层矛盾。线索 +1。" :
                "这是已经见过的重复记录。", 1.8f);
            EvacuationSignals.Emit(evidence.transform.position, 6f, NoiseKind.Pickup);
            Destroy(evidence.gameObject);
        }

        public void NotifyFloorPlan(EvacuationFloorPlan plan)
        {
            _currentPlan = plan;
            if (plan == null) return;
            if (plan.Event == FloorEventKind.TimeSlip)
            {
                _remainingTime = Mathf.Max(0f, _remainingTime - 18f);
            }
            else if (plan.Event == FloorEventKind.ElevatorParasite)
            {
                _power = Mathf.Max(0.01f, _power - 1.5f);
            }
        }

        private void UpdatePassengers()
        {
            if (_mimicCountdown <= 0f)
            {
                return;
            }
            _mimicCountdown -= Time.deltaTime;
            if (_mimicCountdown <= 8f)
            {
                _world.SetCabinLight(Mathf.PerlinNoise(Time.time * 9f, 0.2f) > 0.35f);
            }
            if (_mimicCountdown <= 0f)
            {
                Lose("伪 人", "灯光最后一次亮起时，乘客的脸贴在你的肩后。轿厢里再没有幸存者。");
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
                if (!passenger.IsMimic && _currentFloor <= passenger.DestinationFloor)
                {
                    _power = Mathf.Min(MaxPower, _power + 6f);
                    _rescued++;
                    _passengers.RemoveAt(i);
                    Destroy(passenger.gameObject);
                    ShowTransientMessage("乘客到站，留下了一块应急电池：+6 电力。", 2f);
                }
            }
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
                        _carryingCell = true;
                        InstallPowerCell();
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

        private void UpdateWorldState()
        {
            string status = _phase == EvacuationPhase.ClosingDoors ? "DOORS CLOSING" :
                _phase == EvacuationPhase.OpeningDoors ? "DOORS OPENING" :
                _phase == EvacuationPhase.Descending ? (_braking ? "BRAKING" : "DESCENDING") :
                _doorIntegrity <= 0 ? "DOOR FAILED" : _carryingCell ? "CARRYING CELL" :
                _doorSeal > 0.98f ? "STOPPED / DOORS CLOSED" : "FLOOR OPEN";
            int displayFloor = _currentPlan != null && _currentPlan.Event == FloorEventKind.WrongFloorNumber
                ? Mathf.Clamp(_currentFloor + 7, 1, 99) : _currentFloor;
            _world.SetDisplays(displayFloor, _power, _remainingTime, status);
            _world.SetControlState(EvacuationAction.Descend,
                _phase == EvacuationPhase.Stopped && _doorSeal > 0.98f, false);
            _world.SetControlState(EvacuationAction.Stop, _phase == EvacuationPhase.Descending, false);
            _world.SetControlState(EvacuationAction.Door,
                _phase == EvacuationPhase.Stopped || _phase == EvacuationPhase.ClosingDoors ||
                _phase == EvacuationPhase.OpeningDoors, _doorIntegrity <= 0);
            _world.SetControlState(EvacuationAction.BatterySlot, _carryingCell || _storedCell, _power < 8f);
            _world.SetControlState(EvacuationAction.FusePanel, _hasFuse, _doorIntegrity <= 0);
        }

        private void ResolveExit()
        {
            bool carriesMimic = _passengers.Exists(value => value != null && value.IsMimic);
            ExitResolution resolution = _automation ? ExitResolution.EscapedAlone :
                _story.Resolve(carriesMimic, _rescued, _acceptedAdministrator);
            if (resolution == ExitResolution.FalseLoop)
            {
                _currentFloor = 99;
                _floorFloat = 99f;
                _power = Mathf.Max(_power, 6f);
                _world.BuildFloor(_floorDirector.CreatePlan(RunSeed, 99, _power, _floorsVisited));
                _doorSeal = 0f;
                _world.SetDoorSeal(0f);
                _world.SetBarrier(false);
                _phase = EvacuationPhase.Stopped;
                ShowTransientMessage("门外仍是第 99 层。你抵达的是一座假大厅。", 4f);
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
            _player.CanMove = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void ShowTransientMessage(string value, float duration)
        {
            _message = value;
            _messageUntil = Time.time + duration;
        }

        private void OnGUI()
        {
            EnsureStyles();
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.65f, 1.5f);
            ApplyStyleScale(scale);
            if (_phase == EvacuationPhase.Title)
            {
                DrawTitle(scale);
                return;
            }

            DrawStatus(scale);
            if (_dialogueNpc != null)
            {
                DrawNpcDialogue(scale);
                return;
            }
            if (_phase == EvacuationPhase.Stopped || _phase == EvacuationPhase.Descending)
            {
                DrawInteraction(scale);
            }
            if (!string.IsNullOrEmpty(_message) && Time.time < _messageUntil)
            {
                Rect messageRect = new Rect(Screen.width * 0.2f, Screen.height * 0.76f,
                    Screen.width * 0.6f, 58f * scale);
                DrawPanel(messageRect, new Color(0.08f, 0.82f, 0.72f));
                GUI.Label(messageRect, _message, _centerStyle);
            }
            if (_phase == EvacuationPhase.Won || _phase == EvacuationPhase.Lost)
            {
                DrawEnding(scale);
            }
        }

        private void DrawTitle(float scale)
        {
            if (_titleBackground != null)
            {
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _titleBackground,
                    ScaleMode.ScaleAndCrop);
            }
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0.006f, 0.008f, 0.58f));
            DrawTint(new Rect(0f, Screen.height * 0.57f, Screen.width, Screen.height * 0.43f),
                new Color(0f, 0f, 0f, 0.79f));
            GUI.Label(new Rect(Screen.width * 0.065f, Screen.height * 0.08f,
                Screen.width * 0.82f, 110f * scale), "99 层撤离", _titleStyle);
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.08f + 94f * scale,
                Screen.width * 0.72f, 40f * scale), "THE LAST ELEVATOR", _headingStyle);
            GUI.Label(new Rect(Screen.width * 0.065f, Screen.height * 0.61f,
                Screen.width * 0.76f, 86f * scale),
                "电量无法直达一楼。下降、抢停、进入未知楼层寻找电池；遇到怪物只能逃回电梯，在它越过门缝前关门。大多数撤离都会失败。", _bodyStyle);
            GUI.Label(new Rect(Screen.width * 0.065f, Screen.height * 0.76f,
                Screen.width * 0.8f, 36f * scale),
                "WASD 移动 · SHIFT 冲刺 · C/CTRL 切换蹲伏 · E 交互 · F 手电筒 · 鼠标无限观察", _smallStyle);
            GUI.Label(new Rect(Screen.width * 0.065f, Screen.height * 0.84f,
                Screen.width * 0.45f, 42f * scale), "按 ENTER 开始撤离", _bodyStyle);
        }

        private void DrawStatus(float scale)
        {
            int minutes = Mathf.Max(0, Mathf.FloorToInt(_remainingTime / 60f));
            int seconds = Mathf.Max(0, Mathf.FloorToInt(_remainingTime % 60f));
            Rect telemetry = new Rect(Screen.width * 0.5f - 245f * scale, 18f * scale,
                490f * scale, 42f * scale);
            DrawPanel(telemetry, _power > 8f ? new Color(0.08f, 0.82f, 0.72f) :
                new Color(1f, 0.08f, 0.02f));
            GUI.Label(telemetry,
                "FLOOR " + _currentFloor.ToString("00") + "     POWER " +
                Mathf.CeilToInt(_power).ToString("00") + "/" + Mathf.CeilToInt(MaxPower) +
                "     TIME " + minutes.ToString("00") + ":" + seconds.ToString("00"), _centerStyle);

            float left = 24f * scale;
            float top = Screen.height - 118f * scale;
            GUI.Label(new Rect(left, top, 280f * scale, 28f * scale),
                "HEALTH  " + Mathf.CeilToInt(_health), _smallStyle);
            DrawBar(new Rect(left, top + 30f * scale, 260f * scale, 10f * scale),
                _health / 100f, new Color(0.86f, 0.04f, 0.025f));
            GUI.Label(new Rect(left, top + 46f * scale, 280f * scale, 28f * scale),
                "STAMINA  " + Mathf.CeilToInt(_player.Stamina01 * 100f), _smallStyle);
            DrawBar(new Rect(left, top + 76f * scale, 260f * scale, 10f * scale),
                _player.Stamina01, new Color(0.08f, 0.82f, 0.68f));
            if (_hasFlashlight)
            {
                GUI.Label(new Rect(Screen.width - 220f * scale, Screen.height - 58f * scale,
                    190f * scale, 28f * scale), "FLASH " + Mathf.CeilToInt(_flashCharge), _smallStyle);
            }
            if (_carryingCell)
            {
                GUI.Label(new Rect(Screen.width - 330f * scale, 24f * scale,
                    300f * scale, 30f * scale), "双手搬运：电梯电池", _smallStyle);
            }
            GUI.Label(new Rect(Screen.width - 330f * scale, 58f * scale,
                300f * scale, 28f * scale), "CLUES " + _story.ClueCount + " / 6   PARTS " + _scrap,
                _smallStyle);
        }

        private void DrawInteraction(float scale)
        {
            float size = 4f * scale;
            DrawTint(new Rect(Screen.width * 0.5f - size * 0.5f, Screen.height * 0.5f - size * 0.5f,
                size, size), _focus == null ? new Color(0.7f, 0.8f, 0.78f, 0.58f) : new Color(0.1f, 1f, 0.72f, 0.92f));
            if (_focus == null)
            {
                return;
            }
            string verb = "按 E";
            GUI.Label(new Rect(Screen.width * 0.32f, Screen.height * 0.57f,
                Screen.width * 0.36f, 42f * scale), verb + " · " + _focus.Label, _centerStyle);
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
            GUI.Label(new Rect(rect.x + 40f * scale, rect.yMax - 110f * scale,
                rect.width - 80f * scale, 70f * scale),
                (_phase == EvacuationPhase.Won ? "抵达楼层 " : "死亡楼层 ") + _currentFloor +
                "  ·  探索 " + _floorsVisited + " 层  ·  救出 " + _rescued +
                " 人  ·  种子 " + RunSeed + "\n按 R 使用新种子重新撤离", _smallStyle);
        }

        private void DrawBar(Rect rect, float value, Color color)
        {
            DrawTint(rect, new Color(0f, 0f, 0f, 0.76f));
            DrawTint(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), color);
        }

        private void DrawPanel(Rect rect, Color accent)
        {
            GUI.DrawTexture(rect, _panelTexture);
            Color old = GUI.color;
            GUI.color = accent;
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
            if (_titleStyle != null) return;
            _titleStyle = NewStyle(FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.88f, 0.96f, 0.93f));
            _headingStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.1f, 0.92f, 0.78f));
            _bodyStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.88f, 0.86f));
            _bodyStyle.wordWrap = true;
            _smallStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.55f, 0.74f, 0.7f));
            _centerStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.8f, 0.92f, 0.88f));
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
            _titleStyle.fontSize = Mathf.RoundToInt(62f * scale);
            _headingStyle.fontSize = Mathf.RoundToInt(28f * scale);
            _bodyStyle.fontSize = Mathf.RoundToInt(21f * scale);
            _smallStyle.fontSize = Mathf.RoundToInt(15f * scale);
            _centerStyle.fontSize = Mathf.RoundToInt(17f * scale);
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
