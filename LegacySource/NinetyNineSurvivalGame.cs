using System;
using System.Collections;
using UnityEngine;

namespace NinetyNine
{
    public enum SurvivalPhase
    {
        Title,
        Active,
        Won,
        Lost
    }

    public enum SurvivalControl
    {
        Monitor,
        Door,
        Ultraviolet,
        Light,
        Intercom,
        FuseBox,
        Brake
    }

    public sealed class SurvivalInteractable : MonoBehaviour
    {
        public SurvivalControl Control { get; private set; }
        public string Label { get; private set; }
        public float HoldDuration { get; private set; }

        public void Configure(SurvivalControl control, string label, float holdDuration)
        {
            Control = control;
            Label = label;
            HoldDuration = holdDuration;
        }
    }

    public sealed class NinetyNineSurvivalGame : MonoBehaviour
    {
        private const float RunDuration = 99f;
        private const float MaxPower = 99f;

        private ProceduralWorld _world;
        private FirstPersonController _player;
        private ProceduralAudio _audio;
        private Texture2D _titleBackground;
        private Texture2D _panelTexture;
        private Font _font;
        private GUIStyle _titleStyle;
        private GUIStyle _headingStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _smallStyle;
        private GUIStyle _centerStyle;
        private SurvivalPhase _phase = SurvivalPhase.Title;
        private System.Random _random;
        private SurvivalInteractable _focusedControl;
        private SurvivalControl? _heldControl;
        private float _remainingTime;
        private float _power;
        private float _nextAdvanceTime;
        private float _cameraHold;
        private float _doorHold;
        private float _brakeCooldown;
        private float _intercomCooldown;
        private float _messageUntil;
        private float _interactionHold;
        private float _nextCrawlerTime;
        private float _crawlerLevel;
        private float _nextMirrorTime;
        private float _mirrorLevel;
        private float _nextFuseTime;
        private float _fuseLevel;
        private int _floor;
        private int _watcherStage;
        private int _doorIntegrity;
        private bool _lightOn;
        private bool _cameraOn;
        private bool _doorClosed;
        private bool _uvOn;
        private bool _crawlerActive;
        private bool _mirrorActive;
        private bool _fuseFault;
        private bool _holdLatched;
        private bool _automation;
        private bool _threatCaptureTaken;
        private string _message = string.Empty;
        private string _endingTitle = string.Empty;
        private string _endingBody = string.Empty;

        public float Tension => Mathf.Clamp01(Mathf.Max(
            Mathf.Max(_watcherStage / 4f, _crawlerLevel),
            Mathf.Max(_mirrorLevel, Mathf.Max(_fuseLevel, 1f - _power / MaxPower))) * 0.86f +
            (1f - _remainingTime / RunDuration) * 0.14f);

        private void Start()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            _titleBackground = Resources.Load<Texture2D>("Art/title_hall");
            _panelTexture = MakeTexture(new Color(0.008f, 0.016f, 0.018f, 0.92f));
            _font = CreateGameFont();

            _world = gameObject.AddComponent<ProceduralWorld>();
            _world.InitializeSurvival(this);
            _player = _world.Player;
            _audio = gameObject.AddComponent<ProceduralAudio>();
            _audio.Initialize();
            ShowTitle();

#if UNITY_EDITOR
            string[] args = Environment.GetCommandLineArgs();
            bool capture = Array.Exists(args, value => value == "-ninetyNineSurvivalCapture");
            bool fullRun = Array.Exists(args, value => value == "-ninetyNineSurvivalFullRun");
            if (capture || fullRun)
            {
                StartCoroutine(CapturePrototype(fullRun));
            }
#endif
        }

#if UNITY_EDITOR
        private IEnumerator CapturePrototype(bool fullRun)
        {
            yield return new WaitForSeconds(1.1f);
            string projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
            string captureRoot = System.IO.Path.Combine(projectRoot, "Logs", "Captures");
            System.IO.Directory.CreateDirectory(captureRoot);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "SurvivalTitle.png"));
            yield return new WaitForSeconds(0.4f);
            BeginRun();
            yield return new WaitForSeconds(5f);
            _player.transform.rotation = Quaternion.Euler(0f, -58f, 0f);
            yield return new WaitForEndOfFrame();
            RaycastHit interactionHit;
            if (Physics.Raycast(_player.ViewCamera.transform.position, _player.ViewCamera.transform.forward,
                out interactionHit, 3.25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                SurvivalInteractable capturedControl = interactionHit.collider.GetComponentInParent<SurvivalInteractable>();
                Debug.Log("SURVIVAL_CAPTURE_FOCUS=" + (capturedControl == null ? "NONE" : capturedControl.Control.ToString()));
            }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "SurvivalActive.png"));
            _player.transform.rotation = Quaternion.identity;
            if (!fullRun)
            {
                yield break;
            }

            _automation = true;
            Time.timeScale = 4f;
            while (_phase == SurvivalPhase.Active)
            {
                if (!_threatCaptureTaken && _remainingTime < 42f && _mirrorActive)
                {
                    _threatCaptureTaken = true;
                    _player.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "SurvivalThreats.png"));
                    yield return null;
                    _player.transform.rotation = Quaternion.identity;
                }
                yield return null;
            }
            Time.timeScale = 1f;
            yield return new WaitForSeconds(1.4f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(captureRoot, "SurvivalEnding.png"));
        }
#endif

        private void Update()
        {
            if (_phase == SurvivalPhase.Title)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    BeginRun();
                }
                return;
            }

            if (_phase == SurvivalPhase.Won || _phase == SurvivalPhase.Lost)
            {
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.Return))
                {
                    BeginRun();
                }
                return;
            }

            UpdateInteraction();
            if (_automation)
            {
                UpdateAutomation();
            }
            UpdateRun();
        }

        private void ShowTitle()
        {
            _phase = SurvivalPhase.Title;
            _player.CanMove = false;
            _player.ResetInsideCabin();
            _world.ResetToTitle();
            _world.ResetSurvivalDevices();
            SetCursor(false);
        }

        private void BeginRun()
        {
            int seed = unchecked(Environment.TickCount * 397) ^ DateTime.Now.Millisecond;
            _random = new System.Random(seed);
            _phase = SurvivalPhase.Active;
            _remainingTime = RunDuration;
            _power = MaxPower;
            _floor = 0;
            _watcherStage = 0;
            _doorIntegrity = 2;
            _lightOn = true;
            _cameraOn = false;
            _doorClosed = false;
            _uvOn = false;
            _crawlerActive = false;
            _mirrorActive = false;
            _fuseFault = false;
            _cameraHold = 0f;
            _doorHold = 0f;
            _crawlerLevel = 0f;
            _mirrorLevel = 0f;
            _fuseLevel = 0f;
            _brakeCooldown = 0f;
            _intercomCooldown = 0f;
            _interactionHold = 0f;
            _heldControl = null;
            _holdLatched = false;
            _automation = false;
            _threatCaptureTaken = false;
            _message = "设备分散在轿厢各处。转身、观察、按住 E，活到第 99 层。";
            _messageUntil = Time.time + 4f;
            _world.BeginSurvival();
            _world.ResetSurvivalDevices();
            _world.SetFloorNumber(0);
            _world.SetCabinLightEnabled(true);
            _world.SetDoorsOpen(true);
            _world.SetWatcherStage(0);
            _player.ResetInsideCabin();
            _player.CanMove = true;
            _audio.SetTravelling(true);
            SetCursor(true);
            ScheduleNextAdvance();
            _nextCrawlerTime = Time.time + 17f;
            _nextMirrorTime = Time.time + 37f;
            _nextFuseTime = Time.time + 53f;
            UpdateWorldState();
        }

        private void UpdateInteraction()
        {
            _focusedControl = null;
            Camera view = _player.ViewCamera;
            if (view != null)
            {
                RaycastHit hit;
                if (Physics.Raycast(view.transform.position, view.transform.forward, out hit, 3.25f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    _focusedControl = hit.collider.GetComponentInParent<SurvivalInteractable>();
                }
            }

            bool holding = Input.GetKey(KeyCode.E);
            SetCameraActive(holding && HasFocus(SurvivalControl.Monitor) && !_fuseFault);
            SetDoorActive(holding && HasFocus(SurvivalControl.Door) && !_fuseFault);
            SetUvActive(holding && HasFocus(SurvivalControl.Ultraviolet) && !_fuseFault);

            if (_focusedControl == null)
            {
                ResetInteractionHold();
                return;
            }

            SurvivalControl control = _focusedControl.Control;
            if (Input.GetKeyDown(KeyCode.E))
            {
                if (control == SurvivalControl.Light)
                {
                    ToggleLight();
                }
                else if (control == SurvivalControl.Intercom)
                {
                    UseIntercom();
                }
            }

            if (control != SurvivalControl.FuseBox && control != SurvivalControl.Brake)
            {
                ResetInteractionHold();
                return;
            }

            if (!holding)
            {
                ResetInteractionHold();
                return;
            }

            if (_heldControl != control)
            {
                _heldControl = control;
                _interactionHold = 0f;
                _holdLatched = false;
            }
            if (_holdLatched)
            {
                return;
            }

            _interactionHold += Time.deltaTime;
            if (_interactionHold >= _focusedControl.HoldDuration)
            {
                _holdLatched = true;
                if (control == SurvivalControl.FuseBox)
                {
                    RepairFuse();
                }
                else
                {
                    UseBrake();
                }
            }
        }

        private void ResetInteractionHold()
        {
            if (!Input.GetKey(KeyCode.E) || _focusedControl == null)
            {
                _heldControl = null;
                _interactionHold = 0f;
                _holdLatched = false;
            }
        }

        private bool HasFocus(SurvivalControl control)
        {
            return _focusedControl != null && _focusedControl.Control == control;
        }

        private void UpdateAutomation()
        {
            SetCameraActive(_watcherStage >= 2 && _watcherStage < 4 && !_fuseFault);
            SetDoorActive(_watcherStage >= 4 && !_fuseFault);
            SetUvActive(_crawlerActive && _crawlerLevel > 0.48f && !_fuseFault);
            if (_mirrorActive && _mirrorLevel > 0.42f && _lightOn)
            {
                ToggleLight();
            }
            else if (!_mirrorActive && !_lightOn)
            {
                ToggleLight();
            }
            if (_fuseFault && _fuseLevel > 0.38f)
            {
                RepairFuse();
            }
        }

        private void UpdateRun()
        {
            float delta = Time.deltaTime;
            _remainingTime = Mathf.Max(0f, _remainingTime - delta);
            _floor = Mathf.Clamp(Mathf.FloorToInt((RunDuration - _remainingTime) / RunDuration * 99f), 0, 99);
            _brakeCooldown = Mathf.Max(0f, _brakeCooldown - delta);
            _intercomCooldown = Mathf.Max(0f, _intercomCooldown - delta);

            float drain = 0.09f;
            if (_lightOn) drain += 0.12f;
            if (_cameraOn) drain += 0.64f;
            if (_doorClosed) drain += 0.58f;
            if (_uvOn) drain += 0.78f;
            _power = Mathf.Max(0f, _power - drain * delta);

            UpdateWatcher(delta);
            if (_phase != SurvivalPhase.Active) return;
            UpdateCrawler(delta);
            if (_phase != SurvivalPhase.Active) return;
            UpdateMirror(delta);
            if (_phase != SurvivalPhase.Active) return;
            UpdateFuse(delta);
            UpdateWorldState();

            if (_power <= 0f)
            {
                Lose("断 电", "最后一格电力熄灭了。\n黑暗中，轿厢里多了一次呼吸。");
                return;
            }
            if (_remainingTime <= 0f)
            {
                Win();
            }
        }

        private void UpdateWatcher(float delta)
        {
            if (_cameraOn)
            {
                _cameraHold += delta;
                if (_cameraHold >= 2.55f && _watcherStage > 0)
                {
                    _cameraHold = 0f;
                    SetWatcherStage(_watcherStage - 1);
                    ShowMessage("监控里的轮廓被信号钉在了原地。", 1.4f);
                }
            }
            else
            {
                _cameraHold = 0f;
                if (Time.time >= _nextAdvanceTime)
                {
                    SetWatcherStage(_watcherStage + 1);
                    ScheduleNextAdvance();
                    ShowMessage(GetApproachMessage(), 1.8f);
                }
            }

            if (_watcherStage < 4)
            {
                _doorHold = 0f;
                return;
            }

            if (_doorClosed)
            {
                _doorHold += delta;
                if (_doorHold >= 1.35f)
                {
                    _doorHold = 0f;
                    _power = Mathf.Max(0f, _power - 4f);
                    SetWatcherStage(2);
                    _nextAdvanceTime = Time.time + 4.5f;
                    _audio.PlayArrival(true);
                    ShowMessage("门外传来重击。你把它压回了走廊。", 1.8f);
                }
                return;
            }

            _doorHold += delta;
            if (_doorHold < 2.15f)
            {
                return;
            }
            _doorHold = 0f;
            _doorIntegrity--;
            _audio.PlayArrival(true);
            if (_doorIntegrity <= 0)
            {
                Lose("闯 入", "你听见门滑开的声音。\n监控里的走廊却仍然空无一人。");
                return;
            }
            SetWatcherStage(1);
            _nextAdvanceTime = Time.time + 4f;
            ShowMessage("它把手伸进来了一次。门锁只剩最后一次机会。", 2.1f);
        }

        private void UpdateCrawler(float delta)
        {
            if (!_crawlerActive && Time.time >= _nextCrawlerTime)
            {
                _crawlerActive = true;
                _crawlerLevel = 0.16f;
                _audio.PlayDecision(false);
                ShowMessage("头顶传来金属刮擦声。天花板检修口正在下沉。", 2.2f);
            }
            if (!_crawlerActive)
            {
                return;
            }

            float progress = 1f - _remainingTime / RunDuration;
            _crawlerLevel += (_uvOn ? -0.36f : Mathf.Lerp(0.072f, 0.13f, progress)) * delta;
            if (_crawlerLevel <= 0f)
            {
                _crawlerActive = false;
                _crawlerLevel = 0f;
                _nextCrawlerTime = Time.time + RandomRange(17f, 23f);
                ShowMessage("紫外灯下的肢体缩回了检修口。", 1.7f);
            }
            else if (_crawlerLevel >= 1f)
            {
                Lose("检 修 口", "你最后一次抬头时，天花板已经打开。\n某种倒着爬行的东西落在你身后。");
            }
        }

        private void UpdateMirror(float delta)
        {
            if (!_mirrorActive && Time.time >= _nextMirrorTime)
            {
                _mirrorActive = true;
                _mirrorLevel = 0.18f;
                _audio.PlayDecision(false);
                ShowMessage("背后的镜子敲了三下。镜中的你没有转身。", 2.3f);
            }
            if (!_mirrorActive)
            {
                return;
            }

            float progress = 1f - _remainingTime / RunDuration;
            _mirrorLevel += (_lightOn ? Mathf.Lerp(0.075f, 0.125f, progress) : -0.24f) * delta;
            if (_mirrorLevel <= 0f)
            {
                _mirrorActive = false;
                _mirrorLevel = 0f;
                _nextMirrorTime = Time.time + RandomRange(21f, 27f);
                ShowMessage("灯灭后，镜中的呼吸慢慢与你重合。", 1.8f);
            }
            else if (_mirrorLevel >= 1f)
            {
                Lose("镜 中 人", "镜中的你先一步伸手关掉了灯。\n黑暗里，只剩它站在轿厢这一侧。");
            }
        }

        private void UpdateFuse(float delta)
        {
            if (!_fuseFault && Time.time >= _nextFuseTime)
            {
                _fuseFault = true;
                _fuseLevel = 0.05f;
                SetCameraActive(false);
                SetDoorActive(false);
                SetUvActive(false);
                _audio.PlayArrival(false);
                ShowMessage("配电箱爆出火花。所有防护设备掉线。", 2.1f);
            }
            if (!_fuseFault)
            {
                return;
            }

            _fuseLevel += delta * 0.105f;
            if (_fuseLevel < 1f)
            {
                return;
            }
            _power = Mathf.Max(0f, _power - 18f);
            _fuseFault = false;
            _fuseLevel = 0f;
            _nextFuseTime = Time.time + RandomRange(18f, 24f);
            ShowMessage("过载脉冲烧掉了 18 点电力。配电暂时自行复位。", 2.2f);
        }

        private void SetCameraActive(bool active)
        {
            _cameraOn = active && _power > 0f;
        }

        private void SetDoorActive(bool active)
        {
            bool next = active && _power > 0f;
            if (_doorClosed == next)
            {
                return;
            }
            _doorClosed = next;
            _world.SetDoorsOpen(!_doorClosed);
        }

        private void SetUvActive(bool active)
        {
            _uvOn = active && _power > 0f;
        }

        private void ToggleLight()
        {
            _lightOn = !_lightOn;
            _world.SetCabinLightEnabled(_lightOn);
            ShowMessage(_lightOn ? "轿厢照明：开启" : "轿厢照明：关闭", 1.1f);
        }

        private void RepairFuse()
        {
            if (!_fuseFault)
            {
                ShowMessage("配电线路正常。", 1.1f);
                return;
            }
            _fuseFault = false;
            _fuseLevel = 0f;
            _power = Mathf.Max(0f, _power - 2f);
            _nextFuseTime = Time.time + RandomRange(18f, 24f);
            _audio.PlayDecision(true);
            ShowMessage("线路重新接通。防护设备恢复响应。", 1.5f);
        }

        private void UseBrake()
        {
            if (_brakeCooldown > 0f)
            {
                ShowMessage("制动器冷却中：" + Mathf.CeilToInt(_brakeCooldown) + " 秒", 1.3f);
                return;
            }
            if (_power < 8f)
            {
                ShowMessage("紧急制动需要 8 点电力。", 1.3f);
                return;
            }
            _power -= 8f;
            _brakeCooldown = 11f;
            SetWatcherStage(Mathf.Max(0, _watcherStage - 2));
            _nextAdvanceTime += 4f;
            _audio.PlayArrival(false);
            ShowMessage("紧急制动！走廊与头顶的声音同时被甩远。", 1.7f);
        }

        private void UseIntercom()
        {
            if (_intercomCooldown > 0f)
            {
                ShowMessage("对讲机只有持续的电流声。", 1.2f);
                return;
            }
            _intercomCooldown = 7f;
            string threat = _crawlerActive ? "头顶有东西" : _mirrorActive ? "镜面后有人" : GetWatcherDistance();
            ShowMessage("对讲机：“最近的移动位于——" + threat + "。”", 2.2f);
            if (_watcherStage < 3)
            {
                SetWatcherStage(_watcherStage + 1);
                ScheduleNextAdvance();
            }
        }

        private void Win()
        {
            _phase = SurvivalPhase.Won;
            _floor = 99;
            SetCameraActive(false);
            SetDoorActive(false);
            SetUvActive(false);
            _world.SetFloorNumber(99);
            _world.SetSurvivalDisplays(0, _power, "ARRIVED");
            _audio.SetTravelling(false);
            _audio.PlayArrival(true);
            _endingTitle = "抵达第 99 层";
            _endingBody = "所有设备同时安静下来。\n镜子、检修口和走廊里，各少了一个你。";
            _player.CanMove = true;
        }

        private void Lose(string title, string body)
        {
            _phase = SurvivalPhase.Lost;
            SetCameraActive(false);
            SetDoorActive(false);
            SetUvActive(false);
            _world.SetSurvivalDisplays(Mathf.CeilToInt(_remainingTime), _power, "FAILED");
            _audio.SetTravelling(false);
            _endingTitle = title;
            _endingBody = body;
            _player.CanMove = true;
        }

        private void SetWatcherStage(int value)
        {
            _watcherStage = Mathf.Clamp(value, 0, 4);
            _world.SetWatcherStage(_watcherStage);
        }

        private void ScheduleNextAdvance()
        {
            float progress = 1f - _remainingTime / RunDuration;
            float interval = Mathf.Lerp(7.3f, 3.7f, progress);
            _nextAdvanceTime = Time.time + interval + RandomRange(0.2f, 1.6f);
        }

        private float RandomRange(float minimum, float maximum)
        {
            return Mathf.Lerp(minimum, maximum, (float)_random.NextDouble());
        }

        private string GetApproachMessage()
        {
            switch (_watcherStage)
            {
                case 1: return "监控线路捕捉到走廊尽头的移动。";
                case 2: return "门外的脚步声开始追上电梯。";
                case 3: return "一道轮廓停在门外。监控只能拖慢它。";
                default: return "门缝里伸进了几根手指。去按住门控。";
            }
        }

        private string GetWatcherDistance()
        {
            switch (_watcherStage)
            {
                case 0: return "走廊尽头";
                case 1: return "监控区 C";
                case 2: return "监控区 B";
                case 3: return "门外";
                default: return "门缝";
            }
        }

        private void UpdateWorldState()
        {
            string alert = _fuseFault ? "FUSE" : _crawlerActive && _mirrorActive ? "CEILING / MIRROR" :
                _crawlerActive ? "CEILING" : _mirrorActive ? "MIRROR" : _watcherStage >= 3 ? "DOOR" : "SCAN";
            _world.SetSurvivalDisplays(Mathf.CeilToInt(_remainingTime), _power, alert);
            _world.SetSurvivalControlState(SurvivalControl.Monitor, _cameraOn, _fuseFault);
            _world.SetSurvivalControlState(SurvivalControl.Door, _doorClosed, _fuseFault);
            _world.SetSurvivalControlState(SurvivalControl.Ultraviolet, _uvOn, _fuseFault);
            _world.SetSurvivalControlState(SurvivalControl.Light, _lightOn, false);
            _world.SetSurvivalControlState(SurvivalControl.FuseBox, _fuseFault, false);
            _world.SetCrawlerState(_crawlerActive, _crawlerLevel, _uvOn);
            _world.SetMirrorState(_mirrorActive, _mirrorLevel);
            _world.SetFuseFault(_fuseFault, _fuseLevel);
        }

        private void ShowMessage(string value, float duration)
        {
            _message = value;
            _messageUntil = Time.time + duration;
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

            if (_phase == SurvivalPhase.Title)
            {
                DrawTitle(scale);
                return;
            }

            if (!_lightOn && _phase == SurvivalPhase.Active)
            {
                DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.46f));
            }
            DrawThreatEdges();

            if (_phase == SurvivalPhase.Active)
            {
                DrawInteraction(scale);
            }
            if (!string.IsNullOrEmpty(_message) && Time.time < _messageUntil)
            {
                Rect messageRect = new Rect(Screen.width * 0.22f, Screen.height * 0.76f,
                    Screen.width * 0.56f, 58f * scale);
                DrawPanel(messageRect, new Color(0.1f, 0.78f, 0.72f));
                GUI.Label(messageRect, _message, _centerStyle);
            }
            if (_phase == SurvivalPhase.Won || _phase == SurvivalPhase.Lost)
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
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0.01f, 0.015f, 0.52f));
            DrawTint(new Rect(0f, Screen.height * 0.58f, Screen.width, Screen.height * 0.42f),
                new Color(0f, 0f, 0f, 0.76f));
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.08f,
                Screen.width * 0.8f, 110f * scale), "第 99 层：轿厢故障", _titleStyle);
            GUI.Label(new Rect(Screen.width * 0.075f, Screen.height * 0.08f + 96f * scale,
                Screen.width * 0.7f, 42f * scale), "FAULT 99 · DIEGETIC SURVIVAL", _headingStyle);
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.61f,
                Screen.width * 0.76f, 76f * scale),
                "所有开关都在电梯里。走廊人影：监控拖延、门控阻挡；头顶爬行物：按住 UV；镜中替身：关灯；配电火花：按住保险箱复位。", _bodyStyle);
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.76f,
                Screen.width * 0.62f, 42f * scale), "WASD 移动 · 鼠标观察 · 对准设备按/按住 E", _smallStyle);
            GUI.Label(new Rect(Screen.width * 0.07f, Screen.height * 0.84f,
                Screen.width * 0.5f, 42f * scale), "按 ENTER 启动电梯", _bodyStyle);
        }

        private void DrawInteraction(float scale)
        {
            float size = 4f * scale;
            DrawTint(new Rect(Screen.width * 0.5f - size * 0.5f, Screen.height * 0.5f - size * 0.5f,
                size, size), _focusedControl == null ? new Color(0.7f, 0.82f, 0.8f, 0.62f) :
                new Color(0.2f, 1f, 0.78f, 0.9f));
            if (_focusedControl == null)
            {
                return;
            }

            string verb = _focusedControl.Control == SurvivalControl.Light ||
                _focusedControl.Control == SurvivalControl.Intercom ? "按 E" : "按住 E";
            string suffix = string.Empty;
            if ((_focusedControl.Control == SurvivalControl.FuseBox ||
                _focusedControl.Control == SurvivalControl.Brake) && _heldControl == _focusedControl.Control)
            {
                suffix = "  " + Mathf.RoundToInt(Mathf.Clamp01(_interactionHold /
                    _focusedControl.HoldDuration) * 100f) + "%";
            }
            Rect prompt = new Rect(Screen.width * 0.35f, Screen.height * 0.57f,
                Screen.width * 0.3f, 42f * scale);
            GUI.Label(prompt, verb + " · " + _focusedControl.Label + suffix, _centerStyle);
        }

        private void DrawThreatEdges()
        {
            float danger = Mathf.Max(_crawlerLevel, _mirrorLevel);
            if (danger <= 0.08f)
            {
                return;
            }
            Color color = _mirrorLevel >= _crawlerLevel ? new Color(0.05f, 0f, 0.08f, danger * 0.34f) :
                new Color(0.12f, 0.01f, 0f, danger * 0.3f);
            float edge = Screen.width * 0.055f;
            DrawTint(new Rect(0f, 0f, edge, Screen.height), color);
            DrawTint(new Rect(Screen.width - edge, 0f, edge, Screen.height), color);
            DrawTint(new Rect(0f, 0f, Screen.width, edge * 0.6f), color);
        }

        private void DrawEnding(float scale)
        {
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));
            Rect rect = new Rect(Screen.width * 0.2f, Screen.height * 0.22f,
                Screen.width * 0.6f, Screen.height * 0.52f);
            DrawPanel(rect, _phase == SurvivalPhase.Won ? new Color(0.1f, 0.8f, 0.7f) : new Color(1f, 0.08f, 0.03f));
            GUI.Label(new Rect(rect.x + 42f * scale, rect.y + 42f * scale,
                rect.width - 84f * scale, 72f * scale), _endingTitle, _headingStyle);
            GUI.Label(new Rect(rect.x + 42f * scale, rect.y + 132f * scale,
                rect.width - 84f * scale, 120f * scale), _endingBody, _bodyStyle);
            GUI.Label(new Rect(rect.x + 42f * scale, rect.yMax - 72f * scale,
                rect.width - 84f * scale, 36f * scale),
                "剩余电力 " + Mathf.CeilToInt(_power) + "  ·  按 R 重新启动", _smallStyle);
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
            if (_titleStyle != null)
            {
                return;
            }
            _titleStyle = NewStyle(FontStyle.Bold, TextAnchor.MiddleLeft, new Color(0.86f, 0.95f, 0.92f));
            _headingStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.18f, 0.9f, 0.82f));
            _bodyStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.82f, 0.88f, 0.86f));
            _bodyStyle.wordWrap = true;
            _smallStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.56f, 0.72f, 0.7f));
            _centerStyle = NewStyle(FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.78f, 0.9f, 0.87f));
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
            _titleStyle.fontSize = Mathf.RoundToInt(58f * scale);
            _headingStyle.fontSize = Mathf.RoundToInt(27f * scale);
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
