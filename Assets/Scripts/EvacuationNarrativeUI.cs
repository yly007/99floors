using System;
using System.Collections.Generic;
using UnityEngine;

namespace NinetyNine
{
    public sealed class EvacuationNarrativeUI : MonoBehaviour
    {
        private sealed class SubtitleLine
        {
            public string Speaker;
            public string Body;
            public float Duration;
            public Color Accent;
        }

        private struct PrologueBeat
        {
            public int ImageIndex;
            public string Heading;
            public string Body;
            public float Duration;

            public PrologueBeat(int imageIndex, string heading, string body, float duration)
            {
                ImageIndex = imageIndex;
                Heading = heading;
                Body = body;
                Duration = duration;
            }
        }

        private readonly Queue<SubtitleLine> _subtitleQueue = new Queue<SubtitleLine>();
        private Texture2D _storyAtlas;
        private Texture2D _titleHero;
        private Texture2D _titleWordmark;
        private Texture2D _panelSkin;
        private Texture2D _buttonSkin;
        private Font _font;
        private GUIStyle _eyebrowStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _taglineStyle;
        private GUIStyle _titleWarningStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _primaryButtonStyle;
        private GUIStyle _prologueHeadingStyle;
        private GUIStyle _prologueBodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _subtitleSpeakerStyle;
        private GUIStyle _subtitleBodyStyle;
        private PrologueBeat[] _beats;
        private Action _beginAction;
        private Action _settingsAction;
        private Action _quitAction;
        private Action _prologueComplete;
        private SubtitleLine _activeSubtitle;
        private float _subtitleStarted;
        private float _beatStarted;
        private int _beatIndex;
        private bool _titleVisible;
        private bool _prologueActive;

        public bool PrologueActive => _prologueActive;
        public bool GameplaySubtitleVisible => !_titleVisible && !_prologueActive && _activeSubtitle != null;
        public string GameplaySubtitleSpeaker => _activeSubtitle != null ? _activeSubtitle.Speaker : string.Empty;
        public string GameplaySubtitleBody => _activeSubtitle != null ? _activeSubtitle.Body : string.Empty;
        public Color GameplaySubtitleAccent => _activeSubtitle != null ? _activeSubtitle.Accent : Color.white;
        public float GameplaySubtitleAlpha
        {
            get
            {
                if (_activeSubtitle == null) return 0f;
                float elapsed = Time.unscaledTime - _subtitleStarted;
                return Mathf.Min(Mathf.Clamp01(elapsed / 0.18f),
                    Mathf.Clamp01((_activeSubtitle.Duration - elapsed) / 0.35f));
            }
        }

        public void Initialize(Texture2D storyAtlas, Texture2D titleHero, Texture2D titleWordmark,
            Texture2D panelSkin, Texture2D buttonSkin, Font font, Action beginAction, Action settingsAction,
            Action quitAction)
        {
            _storyAtlas = storyAtlas;
            _titleHero = titleHero;
            _titleWordmark = titleWordmark;
            _panelSkin = panelSkin;
            _buttonSkin = buttonSkin;
            _font = font;
            _beginAction = beginAction;
            _settingsAction = settingsAction;
            _quitAction = quitAction;
            _beats = new[]
            {
                new PrologueBeat(0, "02:17 · 顶层办公室",
                    "凌晨两点十七分，最后一份文件终于发了出去。\n整层楼已经空了，只有走廊尽头的电梯门还开着，像在等我。", 5.3f),
                new PrologueBeat(1, "出口失效",
                    "我本想走消防楼梯离开。推开安全门，门后却不是楼梯间，\n而是一段从没见过的走廊。回头时，来路也不见了。", 5.5f),
                new PrologueBeat(2, "午夜广播",
                    "广播通知：大楼将在二十分钟后进入封锁，所有出口停止服务。\n手机没有信号，窗外只有雾，和一片看不见底的黑。", 5.7f),
                new PrologueBeat(3, "唯一的路",
                    "大厅里只剩这部电梯还能启动，但它的电量不够直达一楼。\n去楼层里找电池，边下行，边找到离开的办法。", 5.6f)
            };
        }

        public void ShowTitle(bool visible)
        {
            _titleVisible = visible;
            if (visible) _prologueActive = false;
        }

        public void PlayPrologue(Action complete)
        {
            _prologueComplete = complete;
            _titleVisible = false;
            _prologueActive = true;
            _beatIndex = 0;
            _beatStarted = Time.unscaledTime;
            _activeSubtitle = null;
            _subtitleQueue.Clear();
        }

        public void SkipPrologue()
        {
            if (!_prologueActive) return;
            _prologueActive = false;
            Action complete = _prologueComplete;
            _prologueComplete = null;
            complete?.Invoke();
        }

        public void QueueThought(string body, float duration)
        {
            if (string.IsNullOrWhiteSpace(body)) return;
            if (_subtitleQueue.Count >= 3) _subtitleQueue.Dequeue();
            _subtitleQueue.Enqueue(new SubtitleLine
            {
                Speaker = "我",
                Body = body,
                Duration = Mathf.Max(1.2f, duration),
                Accent = new Color(0.95f, 0.76f, 0.42f)
            });
        }

        public void ShowThought(string body, float duration)
        {
            ShowImmediate("我", body, duration, new Color(0.95f, 0.76f, 0.42f));
        }

        public void ShowBuildingMessage(string body, float duration)
        {
            ShowImmediate("未知讯号", body, duration, new Color(0.9f, 0.12f, 0.08f));
        }

        public void ShowSystemMessage(string body, float duration)
        {
            ShowImmediate("电梯系统", body, duration, new Color(0.1f, 0.82f, 0.72f));
        }

        public void ShowNpcMessage(string speaker, string body, float duration)
        {
            ShowImmediate(string.IsNullOrWhiteSpace(speaker) ? "乘客" : speaker, body, duration,
                new Color(0.72f, 0.82f, 0.76f));
        }

        public void ClearGameplayNarrative()
        {
            _subtitleQueue.Clear();
            _activeSubtitle = null;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (_prologueActive)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    SkipPrologue();
                    return;
                }
                if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
                {
                    AdvanceBeat();
                    return;
                }
                if (now - _beatStarted >= _beats[_beatIndex].Duration) AdvanceBeat();
                return;
            }
            UpdateSubtitle(now);
        }

        private void AdvanceBeat()
        {
            _beatIndex++;
            if (_beatIndex >= _beats.Length)
            {
                SkipPrologue();
                return;
            }
            _beatStarted = Time.unscaledTime;
        }

        private void ShowImmediate(string speaker, string body, float duration, Color accent)
        {
            if (string.IsNullOrWhiteSpace(body) || _prologueActive) return;
            _subtitleQueue.Clear();
            StartSubtitle(new SubtitleLine
            {
                Speaker = speaker,
                Body = body,
                Duration = Mathf.Max(1.1f, duration),
                Accent = accent
            });
        }

        private void UpdateSubtitle(float now)
        {
            if (_activeSubtitle == null)
            {
                if (_subtitleQueue.Count > 0) StartSubtitle(_subtitleQueue.Dequeue());
                return;
            }
            if (now - _subtitleStarted >= _activeSubtitle.Duration)
            {
                _activeSubtitle = null;
            }
        }

        private void StartSubtitle(SubtitleLine line)
        {
            _activeSubtitle = line;
            _subtitleStarted = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (!_titleVisible && !_prologueActive) return;
            EnsureStyles();
            GUI.depth = -120;
            float scale = Mathf.Clamp(Screen.height / 1080f, 0.72f, 1.5f);
            ApplyStyleScale(scale);
            if (_titleVisible) DrawTitle(scale);
            else DrawPrologue(scale);
        }

        private void DrawTitle(float scale)
        {
            if (_titleHero != null)
            {
                DrawMidnightTitle(scale);
                return;
            }
            float zoom = 1.035f + Mathf.Sin(Time.unscaledTime * 0.12f) * 0.008f;
            if (_titleHero != null) GUI.DrawTexture(ZoomedScreenRect(zoom), _titleHero, ScaleMode.ScaleAndCrop);
            else DrawAtlasCell(2, ZoomedScreenRect(zoom), Color.white);
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.002f, 0.006f, 0.007f, 0.3f));
            DrawPanelPlate(new Rect(Screen.width * 0.046f, Screen.height * 0.11f,
                Screen.width * 0.44f, Screen.height * 0.78f),
                new Color(0.58f, 0.67f, 0.62f, 0.88f));
            DrawTint(new Rect(Screen.width * 0.062f, Screen.height * 0.17f,
                4f * scale, Screen.height * 0.66f), new Color(0.08f, 0.82f, 0.72f, 0.94f));

            GUI.Label(new Rect(Screen.width * 0.082f, Screen.height * 0.18f,
                Screen.width * 0.38f, 36f * scale), "午夜封锁协议 · 99", _eyebrowStyle);
            GUI.Label(new Rect(Screen.width * 0.08f, Screen.height * 0.25f,
                Screen.width * 0.43f, 105f * scale), "末 班 电 梯", _titleStyle);
            GUI.Label(new Rect(Screen.width * 0.083f, Screen.height * 0.39f,
                Screen.width * 0.4f, 70f * scale),
                "你已经加班太久，久到这栋大楼忘了让你离开。", _taglineStyle);

            if (GUI.Button(new Rect(Screen.width * 0.082f, Screen.height * 0.58f,
                330f * scale, 58f * scale), "开始撤离", _primaryButtonStyle))
                _beginAction?.Invoke();
            if (GUI.Button(new Rect(Screen.width * 0.082f, Screen.height * 0.67f,
                330f * scale, 54f * scale), "显示与操作设置", _buttonStyle))
                _settingsAction?.Invoke();
            if (GUI.Button(new Rect(Screen.width * 0.082f, Screen.height * 0.755f,
                330f * scale, 54f * scale), "退出游戏", _buttonStyle))
                _quitAction?.Invoke();
            GUI.Label(new Rect(Screen.width * 0.083f, Screen.height * 0.88f,
                480f * scale, 30f * scale), "ENTER 开始 · ESC 跳过序章", _hintStyle);
        }

        private void DrawMidnightTitle(float scale)
        {
            float zoom = 1.035f + Mathf.Sin(Time.unscaledTime * 0.12f) * 0.008f;
            GUI.DrawTexture(ZoomedScreenRect(zoom), _titleHero, ScaleMode.ScaleAndCrop);
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0.001f, 0.004f, 0.006f, 0.2f));
            DrawTint(new Rect(0f, 0f, Screen.width * 0.49f, Screen.height),
                new Color(0f, 0.002f, 0.004f, 0.46f));

            float left = Screen.width * 0.075f;
            float contentWidth = Mathf.Min(Screen.width * 0.35f, 560f * scale);
            DrawPanelPlate(new Rect(left - 30f * scale, Screen.height * 0.17f,
                contentWidth + 56f * scale, Screen.height * 0.64f),
                new Color(0.16f, 0.2f, 0.19f, 0.53f));
            DrawTint(new Rect(left - 14f * scale, Screen.height * 0.23f,
                3f * scale, Screen.height * 0.48f), new Color(0.69f, 0.12f, 0.09f, 0.88f));

            if (_titleWordmark != null)
            {
                GUI.DrawTextureWithTexCoords(new Rect(left - 4f * scale, Screen.height * 0.3f,
                    contentWidth + 46f * scale, 116f * scale), _titleWordmark,
                    new Rect(0f, 0.18f, 1f, 0.64f), true);
            }
            else
            {
                GUI.Label(new Rect(left - 2f * scale, Screen.height * 0.3f,
                    contentWidth + 40f * scale, 112f * scale), "逃离午夜大楼", _titleStyle);
            }
            GUI.Label(new Rect(left, Screen.height * 0.438f,
                contentWidth, 76f * scale),
                "你已经加班太久，久到这栋大楼忘了让你离开。", _taglineStyle);
            GUI.Label(new Rect(left, Screen.height * 0.525f,
                contentWidth, 26f * scale), "你能逃出生天吗？", _titleWarningStyle);

            if (GUI.Button(new Rect(left, Screen.height * 0.595f,
                330f * scale, 58f * scale), "进入电梯", _primaryButtonStyle))
                _beginAction?.Invoke();
            if (GUI.Button(new Rect(left, Screen.height * 0.685f,
                330f * scale, 54f * scale), "调整终端", _buttonStyle))
                _settingsAction?.Invoke();
            if (GUI.Button(new Rect(left, Screen.height * 0.77f,
                330f * scale, 54f * scale), "离开大楼", _buttonStyle))
                _quitAction?.Invoke();
            float flicker = Mathf.Clamp01((Mathf.Sin(Time.unscaledTime * 1.7f) - 0.955f) / 0.045f);
            if (flicker > 0f)
            {
                DrawTint(new Rect(0f, 0f, Screen.width, Screen.height),
                    new Color(0.18f, 0.01f, 0.005f, flicker * 0.1f));
            }
        }

        private void DrawPrologue(float scale)
        {
            PrologueBeat beat = _beats[_beatIndex];
            float elapsed = Time.unscaledTime - _beatStarted;
            float progress = Mathf.Clamp01(elapsed / beat.Duration);
            float zoom = 1.025f + progress * 0.045f;
            float alpha = Mathf.Min(Mathf.Clamp01(elapsed / 0.65f),
                Mathf.Clamp01((beat.Duration - elapsed) / 0.7f));
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height), Color.black);
            DrawAtlasCell(beat.ImageIndex, ZoomedScreenRect(zoom), new Color(1f, 1f, 1f, alpha));
            DrawTint(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(0f, 0f, 0f, 0.18f));
            DrawTint(new Rect(0f, Screen.height * 0.62f, Screen.width,
                Screen.height * 0.38f), new Color(0f, 0f, 0f, 0.82f));
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Label(new Rect(Screen.width * 0.08f, Screen.height * 0.68f,
                Screen.width * 0.72f, 38f * scale), beat.Heading, _prologueHeadingStyle);
            GUI.Label(new Rect(Screen.width * 0.08f, Screen.height * 0.74f,
                Screen.width * 0.75f, 120f * scale), beat.Body, _prologueBodyStyle);
            GUI.color = old;
            GUI.Label(new Rect(Screen.width * 0.68f, Screen.height * 0.93f,
                Screen.width * 0.26f, 26f * scale), "SPACE 继续 · ESC 跳过序章", _hintStyle);
        }

        private void DrawSubtitle(float scale)
        {
            float elapsed = Time.unscaledTime - _subtitleStarted;
            float alpha = Mathf.Min(Mathf.Clamp01(elapsed / 0.18f),
                Mathf.Clamp01((_activeSubtitle.Duration - elapsed) / 0.35f));
            Rect panel = new Rect(Screen.width * 0.21f, Screen.height * 0.79f,
                Screen.width * 0.58f, 92f * scale);
            DrawPanelPlate(panel, new Color(0.68f, 0.72f, 0.68f, 0.9f * alpha));
            DrawTint(new Rect(panel.x, panel.y, 4f * scale, panel.height),
                new Color(_activeSubtitle.Accent.r, _activeSubtitle.Accent.g,
                    _activeSubtitle.Accent.b, 0.94f * alpha));
            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            _subtitleSpeakerStyle.normal.textColor = _activeSubtitle.Accent;
            GUI.Label(new Rect(panel.x + 25f * scale, panel.y + 17f * scale,
                100f * scale, 34f * scale), _activeSubtitle.Speaker + "：", _subtitleSpeakerStyle);
            GUI.Label(new Rect(panel.x + 105f * scale, panel.y + 13f * scale,
                panel.width - 132f * scale, 62f * scale), _activeSubtitle.Body, _subtitleBodyStyle);
            GUI.color = old;
        }

        private void DrawAtlasCell(int index, Rect destination, Color color)
        {
            if (_storyAtlas == null)
            {
                DrawTint(destination, new Color(0.01f, 0.018f, 0.018f, color.a));
                return;
            }
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(destination, _storyAtlas, AtlasRect(index), true);
            GUI.color = old;
        }

        private static Rect ZoomedScreenRect(float scale)
        {
            float width = Screen.width * scale;
            float height = Screen.height * scale;
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f,
                width, height);
        }

        private static Rect AtlasRect(int index)
        {
            int column = index % 2;
            int rowFromTop = index / 2;
            return new Rect(column * 0.5f, 1f - (rowFromTop + 1) * 0.5f, 0.5f, 0.5f);
        }

        private void EnsureStyles()
        {
            if (_titleStyle != null) return;
            _eyebrowStyle = NewStyle(23, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(0.43f, 0.82f, 0.73f));
            _titleStyle = NewStyle(76, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(0.92f, 0.97f, 0.94f));
            _taglineStyle = NewStyle(25, FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(0.72f, 0.79f, 0.76f));
            _taglineStyle.wordWrap = true;
            _titleWarningStyle = NewStyle(19, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(0.88f, 0.23f, 0.16f));
            _prologueHeadingStyle = NewStyle(23, FontStyle.Bold, TextAnchor.UpperLeft,
                new Color(0.1f, 0.9f, 0.76f));
            _prologueBodyStyle = NewStyle(34, FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(0.9f, 0.94f, 0.92f));
            _prologueBodyStyle.wordWrap = true;
            _hintStyle = NewStyle(16, FontStyle.Normal, TextAnchor.MiddleLeft,
                new Color(0.44f, 0.59f, 0.55f));
            _subtitleSpeakerStyle = NewStyle(20, FontStyle.Bold, TextAnchor.UpperLeft, Color.white);
            _subtitleBodyStyle = NewStyle(24, FontStyle.Normal, TextAnchor.UpperLeft,
                new Color(0.9f, 0.94f, 0.92f));
            _subtitleBodyStyle.wordWrap = true;
            _buttonStyle = NewButtonStyle(new Color(0.035f, 0.06f, 0.06f, 0.9f));
            _primaryButtonStyle = NewButtonStyle(new Color(0.055f, 0.48f, 0.41f, 0.92f));
            _primaryButtonStyle.normal.textColor = new Color(1f, 0.62f, 0.27f);
        }

        private GUIStyle NewStyle(int size, FontStyle fontStyle, TextAnchor anchor, Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                font = _font,
                fontSize = size,
                fontStyle = fontStyle,
                alignment = anchor,
                normal = { textColor = color }
            };
            return style;
        }

        private GUIStyle NewButtonStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button)
            {
                font = _font,
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(28, 12, 0, 0)
            };
            Texture2D normal = _buttonSkin != null ? _buttonSkin : MakeTexture(color);
            Texture2D hover = _buttonSkin != null ? _buttonSkin : MakeTexture(new Color(
                color.r + 0.07f, color.g + 0.1f, color.b + 0.09f, color.a));
            Texture2D active = _buttonSkin != null ? _buttonSkin : MakeTexture(new Color(
                color.r, color.g + 0.16f, color.b + 0.12f, color.a));
            style.normal.background = normal;
            style.hover.background = hover;
            style.active.background = active;
            style.normal.textColor = color.g > 0.3f
                ? new Color(0.16f, 0.96f, 0.76f) : new Color(0.82f, 0.88f, 0.84f);
            style.hover.textColor = new Color(1f, 0.7f, 0.24f);
            style.active.textColor = Color.white;
            return style;
        }

        private void DrawPanelPlate(Rect rect, Color tint)
        {
            if (_panelSkin == null)
            {
                DrawTint(rect, new Color(0.002f, 0.006f, 0.006f, tint.a));
                return;
            }
            Color old = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, _panelSkin, ScaleMode.StretchToFill);
            GUI.color = old;
        }

        private static Texture2D MakeTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void DrawTint(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void ApplyStyleScale(float scale)
        {
            _eyebrowStyle.fontSize = Mathf.RoundToInt(23f * scale);
            _titleStyle.fontSize = Mathf.RoundToInt(76f * scale);
            _taglineStyle.fontSize = Mathf.RoundToInt(25f * scale);
            _titleWarningStyle.fontSize = Mathf.RoundToInt(19f * scale);
            _buttonStyle.fontSize = Mathf.RoundToInt(22f * scale);
            _primaryButtonStyle.fontSize = Mathf.RoundToInt(22f * scale);
            _prologueHeadingStyle.fontSize = Mathf.RoundToInt(23f * scale);
            _prologueBodyStyle.fontSize = Mathf.RoundToInt(34f * scale);
            _hintStyle.fontSize = Mathf.RoundToInt(16f * scale);
            _subtitleSpeakerStyle.fontSize = Mathf.RoundToInt(20f * scale);
            _subtitleBodyStyle.fontSize = Mathf.RoundToInt(24f * scale);
        }
    }
}
