using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NinetyNine
{
    public struct EvacuationUiState
    {
        public bool GameplayVisible;
        public bool DialogueVisible;
        public bool SettingsVisible;
        public bool SettingsPaused;
        public bool EndingVisible;
        public bool Won;
        public bool WarningVisible;
        public bool WarningCritical;
        public bool ObjectiveVisible;
        public bool HealthVisible;
        public bool StaminaVisible;
        public bool FlashlightVisible;
        public bool CarryingVisible;
        public bool ScrapVisible;
        public bool InteractionVisible;
        public bool ControlsVisible;
        public bool PowerNoticeVisible;
        public bool PowerNoticePositive;
        public bool AdministratorOfferVisible;
        public bool SubtitleVisible;
        public string Floor;
        public string Power;
        public string Time;
        public string ElevatorStatus;
        public string PowerDelta;
        public string ObjectiveTitle;
        public string Objective;
        public string ObjectiveDetails;
        public string Warning;
        public string Health;
        public string Stamina;
        public string Flashlight;
        public string Carrying;
        public string Scrap;
        public string Interaction;
        public string Controls;
        public string DialogueTitle;
        public string DialogueBody;
        public string DialogueFirstChoice;
        public string DialogueTradeChoice;
        public string SettingsTitle;
        public string Resolution;
        public string Fullscreen;
        public string SubtitleSpeaker;
        public string SubtitleBody;
        public string EndingOutcome;
        public string EndingTitle;
        public string EndingBody;
        public string EndingStats;
        public string EndingPrompt;
        public string EndingRecord;
        public string EndingSeed;
        public float Power01;
        public float Health01;
        public float Stamina01;
        public float WarningPulse;
        public float EndingAge;
        public float Sensitivity;
        public float Volume;
        public float Brightness;
        public float SubtitleAlpha;
        public Color SubtitleAccent;
    }

    public sealed class EvacuationUiActions
    {
        public Action<int> ChooseDialogue;
        public Action CloseSettings;
        public Action RetrySeed;
        public Action NewSeed;
        public Action Quit;
        public Action PreviousResolution;
        public Action NextResolution;
        public Action ToggleFullscreen;
        public Action ApplyResolution;
        public Action<float> SetSensitivity;
        public Action<float> SetVolume;
        public Action<float> SetBrightness;
    }

    public sealed class EvacuationRuntimeUI : MonoBehaviour
    {
        private static readonly Color Teal = new Color(0.08f, 0.82f, 0.68f, 1f);
        private static readonly Color Amber = new Color(1f, 0.42f, 0.08f, 1f);
        private static readonly Color Red = new Color(0.92f, 0.045f, 0.025f, 1f);
        private static readonly Color TextPrimary = new Color(0.88f, 0.92f, 0.89f, 1f);
        private static readonly Color TextMuted = new Color(0.53f, 0.68f, 0.63f, 1f);

        private Font _font;
        private Sprite _solidSprite;
        private Sprite _panelSprite;
        private Sprite _buttonSprite;
        private EvacuationUiActions _actions;
        private GameObject _hudRoot;
        private GameObject _dialogueRoot;
        private GameObject _settingsRoot;
        private GameObject _endingRoot;
        private Text _floorText;
        private Text _powerText;
        private Text _timeText;
        private Text _powerDeltaText;
        private Text _statusText;
        private Image _statusPanel;
        private Image _powerFill;
        private GameObject _objectiveRoot;
        private Text _objectiveTitle;
        private Text _objectiveText;
        private Text _objectiveDetails;
        private GameObject _warningRoot;
        private Image _warningPanel;
        private Text _warningText;
        private GameObject _healthRoot;
        private Text _healthText;
        private Image _healthFill;
        private GameObject _staminaRoot;
        private Text _staminaText;
        private Image _staminaFill;
        private Text _flashlightText;
        private Text _carryingText;
        private Text _scrapText;
        private Image _reticle;
        private GameObject _interactionRoot;
        private Text _interactionText;
        private Text _controlsText;
        private GameObject _subtitleRoot;
        private CanvasGroup _subtitleGroup;
        private Image _subtitleMarker;
        private Text _subtitleSpeaker;
        private Text _subtitleBody;
        private Text _dialogueTitle;
        private Text _dialogueBody;
        private Button[] _dialogueButtons;
        private Text[] _dialogueButtonLabels;
        private Text _settingsTitle;
        private Text _resolutionText;
        private Text _fullscreenText;
        private Slider _sensitivitySlider;
        private Slider _volumeSlider;
        private Slider _brightnessSlider;
        private Button _retrySettingsButton;
        private Text _endingOutcome;
        private Text _endingTitle;
        private Text _endingBody;
        private Text _endingStats;
        private Text _endingPrompt;
        private Text _endingRecord;
        private Text _endingSeed;
        private CanvasGroup _endingGroup;
        private bool _settingsWasVisible;

        public void Initialize(Font font, Texture2D panelTexture, Texture2D buttonTexture,
            Texture2D endingTexture, EvacuationUiActions actions)
        {
            _font = font;
            _actions = actions;
            _solidSprite = CreateSprite(Texture2D.whiteTexture, 0f);
            _panelSprite = CreateSprite(panelTexture, 42f);
            _buttonSprite = CreateSprite(buttonTexture, 28f);

            GameObject canvasObject = new GameObject("Runtime UGUI", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 180;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            if (FindObjectOfType<EventSystem>() == null)
            {
                GameObject eventObject = new GameObject("UI Event System", typeof(EventSystem),
                    typeof(StandaloneInputModule));
                eventObject.transform.SetParent(transform, false);
            }

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            BuildHud(canvasRect);
            BuildDialogue(canvasRect);
            BuildSettings(canvasRect);
            BuildEnding(canvasRect, endingTexture);
        }

        public void Sync(EvacuationUiState state)
        {
            SetActive(_hudRoot, state.GameplayVisible && !state.DialogueVisible &&
                !state.SettingsVisible && !state.EndingVisible);
            SetActive(_dialogueRoot, state.DialogueVisible && !state.EndingVisible);
            SetActive(_settingsRoot, state.SettingsVisible && !state.EndingVisible);
            SetActive(_endingRoot, state.EndingVisible);

            _floorText.text = state.Floor;
            _powerText.text = state.Power;
            _timeText.text = state.Time;
            _powerFill.fillAmount = state.Power01;
            _powerFill.color = state.Power01 <= 0.27f ? Red : Teal;
            _powerDeltaText.gameObject.SetActive(state.PowerNoticeVisible);
            _powerDeltaText.text = state.PowerDelta;
            _powerDeltaText.color = state.PowerNoticePositive ? Teal : Amber;
            _statusText.text = state.ElevatorStatus;
            _statusPanel.color = state.ElevatorStatus == "门控系统故障"
                ? new Color(Red.r, Red.g, Red.b, 0.94f)
                : new Color(0.02f, 0.035f, 0.035f, 0.94f);

            SetActive(_objectiveRoot, state.ObjectiveVisible);
            _objectiveTitle.text = state.ObjectiveTitle;
            _objectiveText.text = state.Objective;
            _objectiveDetails.text = state.ObjectiveDetails;
            bool hasObjectiveDetails = !string.IsNullOrEmpty(state.ObjectiveDetails);
            _objectiveDetails.gameObject.SetActive(hasObjectiveDetails);
            RectTransform objectiveRect = _objectiveRoot.transform as RectTransform;
            float objectiveHeight = hasObjectiveDetails ? 270f : 132f;
            objectiveRect.sizeDelta = new Vector2(460f, objectiveHeight);
            objectiveRect.anchoredPosition = new Vector2(250f, -18f - objectiveHeight * 0.5f);
            SetBox(_objectiveText.rectTransform, new Vector2(0.5f, 1f),
                new Vector2(416f, hasObjectiveDetails ? 135f : 82f),
                new Vector2(0f, hasObjectiveDetails ? -105f : -76f));

            SetActive(_warningRoot, state.WarningVisible);
            _warningText.text = state.Warning;
            Color warningColor = state.WarningCritical ? Red : Amber;
            warningColor.a = state.WarningPulse;
            _warningPanel.color = new Color(0.015f, 0.012f, 0.01f, 0.92f);
            _warningText.color = warningColor;

            SetActive(_healthRoot, state.HealthVisible);
            _healthText.text = state.Health;
            _healthFill.fillAmount = state.Health01;
            SetActive(_staminaRoot, state.StaminaVisible);
            _staminaText.text = state.Stamina;
            _staminaFill.fillAmount = state.Stamina01;
            _flashlightText.gameObject.SetActive(state.FlashlightVisible);
            _flashlightText.text = state.Flashlight;
            _carryingText.gameObject.SetActive(state.CarryingVisible);
            _carryingText.text = state.Carrying;
            _scrapText.gameObject.SetActive(state.ScrapVisible);
            _scrapText.text = state.Scrap;
            _reticle.color = state.InteractionVisible ? Teal : new Color(0.68f, 0.76f, 0.72f, 0.58f);
            SetActive(_interactionRoot, state.InteractionVisible);
            _interactionText.text = state.Interaction;
            _controlsText.gameObject.SetActive(state.ControlsVisible);
            _controlsText.text = state.Controls;
            SetActive(_subtitleRoot, state.SubtitleVisible);
            if (state.SubtitleVisible)
            {
                _subtitleGroup.alpha = state.SubtitleAlpha;
                _subtitleMarker.color = state.SubtitleAccent;
                _subtitleSpeaker.color = state.SubtitleAccent;
                _subtitleSpeaker.text = state.SubtitleSpeaker + "：";
                _subtitleBody.text = state.SubtitleBody;
            }

            if (state.DialogueVisible)
            {
                _dialogueTitle.text = state.DialogueTitle;
                _dialogueBody.text = state.DialogueBody;
                _dialogueButtonLabels[0].text = state.DialogueFirstChoice;
                _dialogueButtonLabels[2].text = state.DialogueTradeChoice;
                _dialogueButtons[4].gameObject.SetActive(state.AdministratorOfferVisible);
            }

            SyncSettings(state);
            SyncEnding(state);
        }

        private void BuildHud(RectTransform parent)
        {
            _hudRoot = CreateRect("Gameplay HUD", parent, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero).gameObject;

            Image telemetry = CreateImage("Elevator Telemetry", _hudRoot.transform, _panelSprite,
                new Color(0.015f, 0.025f, 0.025f, 0.92f));
            SetBox(telemetry.rectTransform, new Vector2(0.5f, 1f), new Vector2(720f, 82f),
                new Vector2(0f, -54f));
            _floorText = CreateTelemetry(telemetry.transform, "楼层", new Vector2(-270f, 0f), 150f, Red);
            _powerText = CreateTelemetry(telemetry.transform, "电梯电量", Vector2.zero, 290f, Teal);
            _timeText = CreateTelemetry(telemetry.transform, "剩余时间", new Vector2(270f, 0f), 210f, Amber);
            _powerFill = CreateFill("Power Fill", telemetry.transform, new Vector2(0f, -31f),
                new Vector2(245f, 4f), Teal);
            _powerDeltaText = CreateText("Power Delta", telemetry.transform, string.Empty, 17,
                TextAnchor.MiddleRight, Amber);
            SetBox(_powerDeltaText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(70f, 24f),
                new Vector2(105f, 21f));

            _statusPanel = CreateImage("Elevator Status", _hudRoot.transform, _panelSprite,
                new Color(0.015f, 0.03f, 0.03f, 0.92f));
            SetBox(_statusPanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(390f, 34f),
                new Vector2(0f, -111f));
            _statusText = CreateText("Status", _statusPanel.transform, string.Empty, 17,
                TextAnchor.MiddleCenter, TextPrimary);
            Stretch(_statusText.rectTransform, 8f, 3f, 8f, 3f);

            Image objectivePanel = CreateImage("Objective", _hudRoot.transform, _panelSprite,
                new Color(0.018f, 0.026f, 0.024f, 0.9f));
            _objectiveRoot = objectivePanel.gameObject;
            SetBox(objectivePanel.rectTransform, new Vector2(0f, 1f), new Vector2(460f, 270f),
                new Vector2(250f, -162f));
            _objectiveTitle = CreateText("Objective Header", objectivePanel.transform, string.Empty, 19,
                TextAnchor.MiddleLeft, Amber);
            SetBox(_objectiveTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(416f, 30f),
                new Vector2(0f, -25f));
            _objectiveText = CreateText("Objective Body", objectivePanel.transform, string.Empty, 19,
                TextAnchor.UpperLeft, TextPrimary);
            SetBox(_objectiveText.rectTransform, new Vector2(0.5f, 1f), new Vector2(416f, 135f),
                new Vector2(0f, -105f));
            _objectiveDetails = CreateText("Objective Details", objectivePanel.transform, string.Empty, 16,
                TextAnchor.UpperLeft, TextMuted);
            SetBox(_objectiveDetails.rectTransform, new Vector2(0.5f, 0f), new Vector2(416f, 86f),
                new Vector2(0f, 51f));

            Image warning = CreateImage("Resource Warning", _hudRoot.transform, _panelSprite,
                new Color(0.02f, 0.015f, 0.012f, 0.92f));
            _warningRoot = warning.gameObject;
            _warningPanel = warning;
            SetBox(warning.rectTransform, new Vector2(0.5f, 1f), new Vector2(510f, 42f),
                new Vector2(0f, -158f));
            _warningText = CreateText("Warning Text", warning.transform, string.Empty, 17,
                TextAnchor.MiddleCenter, Amber);
            Stretch(_warningText.rectTransform, 10f, 2f, 10f, 2f);

            _healthRoot = BuildResourceBar(_hudRoot.transform, "生命", new Vector2(170f, 44f),
                Red, out _healthText, out _healthFill);
            _staminaRoot = BuildResourceBar(_hudRoot.transform, "体力", new Vector2(170f, 92f),
                Teal, out _staminaText, out _staminaFill);

            _flashlightText = CreateText("Flashlight", _hudRoot.transform, string.Empty, 15,
                TextAnchor.MiddleRight, TextMuted);
            SetBox(_flashlightText.rectTransform, new Vector2(1f, 0f), new Vector2(260f, 28f),
                new Vector2(-154f, 28f));
            _scrapText = CreateText("Scrap", _hudRoot.transform, string.Empty, 15,
                TextAnchor.MiddleRight, TextMuted);
            SetBox(_scrapText.rectTransform, new Vector2(1f, 0f), new Vector2(260f, 28f),
                new Vector2(-154f, 60f));
            _carryingText = CreateText("Carrying", _hudRoot.transform, string.Empty, 16,
                TextAnchor.MiddleRight, Amber);
            SetBox(_carryingText.rectTransform, new Vector2(1f, 1f), new Vector2(330f, 30f),
                new Vector2(-185f, -34f));

            _reticle = CreateImage("Interaction Reticle", _hudRoot.transform, null,
                new Color(0.68f, 0.76f, 0.72f, 0.58f));
            SetBox(_reticle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(6f, 6f), Vector2.zero);
            Image interaction = CreateImage("Interaction Prompt", _hudRoot.transform, _panelSprite,
                new Color(0.01f, 0.025f, 0.022f, 0.94f));
            _interactionRoot = interaction.gameObject;
            SetBox(interaction.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(410f, 46f),
                new Vector2(0f, -54f));
            _interactionText = CreateText("Interaction Text", interaction.transform, string.Empty, 18,
                TextAnchor.MiddleCenter, Teal);
            Stretch(_interactionText.rectTransform, 10f, 2f, 10f, 2f);
            _controlsText = CreateText("Controls", _hudRoot.transform, string.Empty, 15,
                TextAnchor.MiddleCenter, TextMuted);
            SetBox(_controlsText.rectTransform, new Vector2(0.5f, 0f), new Vector2(960f, 32f),
                new Vector2(0f, 22f));

            Image subtitle = CreateImage("Narrative Subtitle", _hudRoot.transform, _panelSprite,
                new Color(0.018f, 0.024f, 0.022f, 0.96f));
            _subtitleRoot = subtitle.gameObject;
            _subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>();
            SetBox(subtitle.rectTransform, new Vector2(0.5f, 0f), new Vector2(1120f, 94f),
                new Vector2(0f, 108f));
            _subtitleMarker = CreateImage("Subtitle Marker", subtitle.transform, null, Amber);
            SetBox(_subtitleMarker.rectTransform, new Vector2(0f, 0.5f), new Vector2(4f, 72f),
                new Vector2(10f, 0f));
            _subtitleSpeaker = CreateText("Subtitle Speaker", subtitle.transform, string.Empty, 20,
                TextAnchor.UpperLeft, Amber);
            SetBox(_subtitleSpeaker.rectTransform, new Vector2(0f, 0.5f), new Vector2(125f, 64f),
                new Vector2(92f, -3f));
            _subtitleBody = CreateText("Subtitle Body", subtitle.transform, string.Empty, 20,
                TextAnchor.UpperLeft, TextPrimary);
            SetBox(_subtitleBody.rectTransform, new Vector2(0f, 0.5f), new Vector2(900f, 64f),
                new Vector2(620f, -3f));
        }

        private void BuildDialogue(RectTransform parent)
        {
            RectTransform root = CreateRect("NPC Dialogue", parent, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            _dialogueRoot = root.gameObject;
            Image dim = CreateImage("Dialogue Dim", root, null, new Color(0f, 0f, 0f, 0.36f));
            Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);
            Image panel = CreateImage("Dialogue Panel", root, _panelSprite,
                new Color(0.055f, 0.045f, 0.028f, 0.97f));
            SetBox(panel.rectTransform, new Vector2(0.5f, 0f), new Vector2(1320f, 430f),
                new Vector2(0f, 245f));
            _dialogueTitle = CreateText("Dialogue Title", panel.transform, string.Empty, 29,
                TextAnchor.MiddleLeft, Amber);
            SetBox(_dialogueTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(1240f, 48f),
                new Vector2(0f, -46f));
            _dialogueBody = CreateText("Dialogue Body", panel.transform, string.Empty, 21,
                TextAnchor.UpperLeft, TextPrimary);
            SetBox(_dialogueBody.rectTransform, new Vector2(0.5f, 1f), new Vector2(1240f, 88f),
                new Vector2(0f, -116f));

            _dialogueButtons = new Button[5];
            _dialogueButtonLabels = new Text[5];
            string[] labels = { "1  同意同行", "2  询问大楼", "3  用零件交易电力", "4  拒绝 / 离开", "5  接受管理员协议（不可撤销）" };
            for (int i = 0; i < 4; i++)
            {
                int choice = i + 1;
                Vector2 position = new Vector2(i % 2 == 0 ? -310f : 310f, i < 2 ? -220f : -284f);
                _dialogueButtons[i] = CreateButton("Dialogue Choice " + choice, panel.transform,
                    labels[i], position, new Vector2(590f, 50f), () => _actions.ChooseDialogue(choice),
                    out _dialogueButtonLabels[i]);
            }
            _dialogueButtons[4] = CreateButton("Administrator Choice", panel.transform, labels[4],
                new Vector2(0f, -350f), new Vector2(1210f, 46f),
                () => _actions.ChooseDialogue(5), out _dialogueButtonLabels[4]);
        }

        private void BuildSettings(RectTransform parent)
        {
            RectTransform root = CreateRect("Settings", parent, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            _settingsRoot = root.gameObject;
            Image dim = CreateImage("Settings Dim", root, null, new Color(0f, 0f, 0f, 0.84f));
            Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);
            Image panel = CreateImage("Settings Panel", root, _panelSprite,
                new Color(0.045f, 0.04f, 0.03f, 0.98f));
            SetBox(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(760f, 650f), Vector2.zero);
            _settingsTitle = CreateText("Settings Title", panel.transform, string.Empty, 31,
                TextAnchor.MiddleLeft, Amber);
            SetBox(_settingsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(670f, 54f),
                new Vector2(0f, -48f));
            _sensitivitySlider = CreateSettingSlider(panel.transform, "鼠标灵敏度", -108f, 0.8f, 5f,
                _actions.SetSensitivity);
            _volumeSlider = CreateSettingSlider(panel.transform, "总音量", -178f, 0f, 1f,
                _actions.SetVolume);
            _brightnessSlider = CreateSettingSlider(panel.transform, "画面亮度", -248f, 0.72f, 1.35f,
                _actions.SetBrightness);

            CreateText("Resolution Label", panel.transform, "显示分辨率", 19,
                TextAnchor.MiddleLeft, TextPrimary, new Vector2(-250f, -322f), new Vector2(170f, 42f));
            CreateButton("Previous Resolution", panel.transform, "‹", new Vector2(-95f, -322f),
                new Vector2(48f, 42f), _actions.PreviousResolution, out _);
            _resolutionText = CreateText("Resolution", panel.transform, string.Empty, 18,
                TextAnchor.MiddleCenter, TextPrimary, new Vector2(35f, -322f), new Vector2(190f, 42f));
            CreateButton("Next Resolution", panel.transform, "›", new Vector2(165f, -322f),
                new Vector2(48f, 42f), _actions.NextResolution, out _);
            Button fullscreen = CreateButton("Fullscreen", panel.transform, string.Empty,
                new Vector2(275f, -322f), new Vector2(150f, 42f), _actions.ToggleFullscreen,
                out _fullscreenText);
            fullscreen.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            CreateButton("Apply Resolution", panel.transform, "应用显示设置", new Vector2(-180f, -392f),
                new Vector2(300f, 48f), _actions.ApplyResolution, out _);
            CreateButton("Close Settings", panel.transform, "继续 / 返回", new Vector2(180f, -392f),
                new Vector2(300f, 48f), _actions.CloseSettings, out _);
            _retrySettingsButton = CreateButton("Retry Seed", panel.transform, "重开当前种子",
                new Vector2(-180f, -464f), new Vector2(300f, 46f), _actions.RetrySeed, out _);
            CreateButton("Quit", panel.transform, "退出游戏", new Vector2(180f, -464f),
                new Vector2(300f, 46f), _actions.Quit, out _);
        }

        private void BuildEnding(RectTransform parent, Texture2D endingTexture)
        {
            RectTransform root = CreateRect("Ending", parent, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            _endingRoot = root.gameObject;
            _endingGroup = root.gameObject.AddComponent<CanvasGroup>();
            RawImage background = root.gameObject.AddComponent<RawImage>();
            background.texture = endingTexture;
            background.color = Color.white;
            Image dim = CreateImage("Ending Contrast", root, null, new Color(0f, 0f, 0f, 0.18f));
            Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);

            _endingOutcome = CreateText("Ending Outcome", root, string.Empty, 23,
                TextAnchor.MiddleLeft, Red, new Vector2(-520f, 382f), new Vector2(760f, 38f));
            _endingTitle = CreateText("Ending Title", root, string.Empty, 48,
                TextAnchor.MiddleLeft, TextPrimary, new Vector2(-520f, 318f), new Vector2(760f, 72f));
            _endingBody = CreateText("Ending Body", root, string.Empty, 24,
                TextAnchor.UpperLeft, new Color(0.76f, 0.8f, 0.76f, 1f),
                new Vector2(-520f, 210f), new Vector2(760f, 120f));
            _endingStats = CreateText("Ending Stats", root, string.Empty, 23,
                TextAnchor.UpperLeft, TextPrimary, new Vector2(-520f, 68f), new Vector2(760f, 118f));
            _endingRecord = CreateText("Ending Record", root, string.Empty, 19,
                TextAnchor.MiddleLeft, Amber, new Vector2(-520f, -38f), new Vector2(760f, 42f));
            _endingPrompt = CreateText("Ending Prompt", root, string.Empty, 19,
                TextAnchor.MiddleLeft, TextMuted, new Vector2(-520f, -86f), new Vector2(760f, 52f));
            _endingSeed = CreateText("Ending Seed", root, string.Empty, 14,
                TextAnchor.MiddleRight, TextMuted, new Vector2(505f, -438f), new Vector2(520f, 30f));

            CreateButton("New Seed", root, "换一栋，再下去", new Vector2(-515f, -374f),
                new Vector2(500f, 78f), _actions.NewSeed, out Text newSeedLabel);
            newSeedLabel.fontSize = 25;
            newSeedLabel.color = Teal;
            CreateButton("Retry Same Seed", root, "不服，重走这栋", new Vector2(85f, -374f),
                new Vector2(500f, 78f), _actions.RetrySeed, out Text retryLabel);
            retryLabel.fontSize = 23;
        }

        private void SyncSettings(EvacuationUiState state)
        {
            if (state.SettingsVisible && !_settingsWasVisible)
            {
                _sensitivitySlider.SetValueWithoutNotify(state.Sensitivity);
                _volumeSlider.SetValueWithoutNotify(state.Volume);
                _brightnessSlider.SetValueWithoutNotify(state.Brightness);
            }
            _settingsWasVisible = state.SettingsVisible;
            if (!state.SettingsVisible) return;
            _settingsTitle.text = state.SettingsTitle;
            _resolutionText.text = state.Resolution;
            _fullscreenText.text = state.Fullscreen;
            _retrySettingsButton.gameObject.SetActive(state.SettingsPaused);
        }

        private void SyncEnding(EvacuationUiState state)
        {
            if (!state.EndingVisible) return;
            float alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(state.EndingAge / 0.7f));
            _endingGroup.alpha = alpha;
            _endingGroup.blocksRaycasts = state.EndingAge >= 0.45f;
            Color outcomeColor = state.Won ? Teal : Red;
            _endingOutcome.color = outcomeColor;
            _endingOutcome.text = state.EndingOutcome;
            _endingTitle.text = state.EndingTitle;
            _endingBody.text = state.EndingBody;
            _endingStats.text = state.EndingStats;
            _endingPrompt.text = state.EndingPrompt;
            _endingRecord.text = state.EndingRecord;
            _endingSeed.text = state.EndingSeed;
        }

        private GameObject BuildResourceBar(Transform parent, string label, Vector2 position,
            Color color, out Text text, out Image fill)
        {
            RectTransform root = CreateRect(label, parent, new Vector2(0f, 0f), new Vector2(0f, 0f),
                position, new Vector2(290f, 40f));
            text = CreateText(label + " Text", root, label, 15, TextAnchor.MiddleLeft, TextMuted);
            SetBox(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(110f, 24f),
                new Vector2(55f, 7f));
            fill = CreateFill(label + " Fill", root, new Vector2(180f, 6f), new Vector2(260f, 7f), color);
            RectTransform fillBackground = fill.transform.parent as RectTransform;
            SetBox(fillBackground, new Vector2(0f, 0.5f), new Vector2(260f, 7f),
                new Vector2(145f, -8f));
            return root.gameObject;
        }

        private Text CreateTelemetry(Transform parent, string label, Vector2 position, float width, Color accent)
        {
            Text labelText = CreateText(label + " Label", parent, label, 14,
                TextAnchor.MiddleCenter, TextMuted, position + new Vector2(0f, 20f), new Vector2(width, 20f));
            Text value = CreateText(label + " Value", parent, string.Empty, 26,
                TextAnchor.MiddleCenter, TextPrimary, position + new Vector2(0f, -7f), new Vector2(width, 36f));
            Image marker = CreateImage(label + " Marker", parent, null, accent);
            SetBox(marker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(3f, 55f),
                position + new Vector2(-width * 0.5f, 0f));
            labelText.raycastTarget = false;
            return value;
        }

        private Slider CreateSettingSlider(Transform parent, string label, float y, float min, float max,
            Action<float> callback)
        {
            CreateText(label, parent, label, 19, TextAnchor.MiddleLeft, TextPrimary,
                new Vector2(-250f, y), new Vector2(180f, 38f));
            RectTransform sliderRoot = CreateRect(label + " Slider", parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), new Vector2(120f, y), new Vector2(360f, 36f));
            Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
            Image background = CreateImage("Background", sliderRoot, null, new Color(0f, 0f, 0f, 0.72f));
            SetBox(background.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(350f, 7f), Vector2.zero);
            Image fill = CreateImage("Fill", sliderRoot, null, Teal);
            Stretch(fill.rectTransform, 5f, 14f, 5f, 14f);
            Image handle = CreateImage("Handle", sliderRoot, _buttonSprite, Amber);
            SetBox(handle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(28f, 32f), Vector2.zero);
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(value => callback(value));
            return slider;
        }

        private Button CreateButton(string name, Transform parent, string label, Vector2 position,
            Vector2 size, Action callback, out Text text)
        {
            Image image = CreateImage(name, parent, _buttonSprite, new Color(0.34f, 0.31f, 0.25f, 1f));
            SetBox(image.rectTransform, new Vector2(0.5f, 0.5f), size, position);
            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.78f, 0.78f, 0.73f, 1f);
            colors.highlightedColor = new Color(0.92f, 1f, 0.92f, 1f);
            colors.pressedColor = new Color(0.72f, 0.52f, 0.32f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() => callback());
            text = CreateText("Label", image.transform, label, 18, TextAnchor.MiddleCenter, TextPrimary);
            Stretch(text.rectTransform, 12f, 5f, 12f, 5f);
            return button;
        }

        private Image CreateFill(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            Image background = CreateImage(name + " Background", parent, null, new Color(0f, 0f, 0f, 0.78f));
            SetBox(background.rectTransform, new Vector2(0.5f, 0.5f), size, position);
            Image fill = CreateImage(name, background.transform, null, color);
            Stretch(fill.rectTransform, 0f, 0f, 0f, 0f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            return fill;
        }

        private Image CreateImage(string name, Transform parent, Sprite sprite, Color color)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? sprite : _solidSprite;
            image.color = color;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            return image;
        }

        private Text CreateText(string name, Transform parent, string value, int size,
            TextAnchor alignment, Color color)
        {
            return CreateText(name, parent, value, size, alignment, color, Vector2.zero,
                new Vector2(100f, 30f));
        }

        private Text CreateText(string name, Transform parent, string value, int size,
            TextAnchor alignment, Color color, Vector2 position, Vector2 boxSize)
        {
            RectTransform rect = CreateRect(name, parent, new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f), position, boxSize);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.text = value;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            GameObject value = new GameObject(name, typeof(RectTransform));
            value.transform.SetParent(parent, false);
            RectTransform rect = value.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        private static void SetBox(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static Sprite CreateSprite(Texture2D texture, float border)
        {
            if (texture == null) return null;
            float safe = Mathf.Min(border, Mathf.Min(texture.width, texture.height) * 0.24f);
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(safe, safe, safe, safe));
        }

        private static void SetActive(GameObject target, bool value)
        {
            if (target.activeSelf != value) target.SetActive(value);
        }
    }
}
