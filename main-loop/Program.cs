using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.Effects;
using ArtFrame.RythmModule;
using ArtFrame.UIModifier;
using ArtFrame.UserInterface;
using OppaiSharp;
using OsuLib;
using ArtFrame.FileProcessing;
using System.Text.Json;
using System.Collections.Generic;

using static ArtFrame.AudioHelper;
using static ArtFrame.EffectsHelper;
using static ArtFrame.FontHelper;
using static ArtFrame.GraphicsHelper;
using static ArtFrame.InputHelper;
using static ArtFrame.RythmHelper;
using static ArtFrame.SpriteHelper;
using static ArtFrame.TextureHelper;
using static ArtFrame.TweenHelper;
using System.Diagnostics;

namespace CoreGame
{
    public class Program
    {
        static void Main() => Engine.Run<MainGame>();
    }

    public enum GameplayMode
    {
        Taiko,
        Stack
    }

    public partial class MainGame : IArt
    {
        // ─── Game Constants ──────────────────────────────────────────────────
        private const string SongsPath = @"playlists/";
        private const string SettingsFileName = "settings.json";

        // ─── Rendering & Core Visual Components ─────────────────────────────────
        private GaussianBlurEffect _blur = new();
        private TaikoPlayfield? _taikofield;
        private StackPlayfield? _stackfield;
        private ResultScreen? _resultscreen;
        private bool _inResultScreen = false;
        private float _gameplayFinishTimer = 0f;
        private GameplayMode _activeGameplayMode = GameplayMode.Taiko;
        private OnlineManager _online = new("http://192.168.1.49:3000");
        private ImageButton _logoUI = null!;
        private EffectFrame _blurBgUI = null!;
        private Frame _shockwaveHolder = null!;
        private ScrollingFrame _playlistScroll = null!;
        private ImageFrame _bgImageFrame = null!;
        private Frame _playerControlFrame = null!;
        private Frame _bgDrop = null!;
        private VideoFrame? _bgVideoFrame;

        // ─── Startup & Audio Preferences (Settings) ──────────────────────────
        private GameSettings _settings = new();
        private float _targetVolume = 0.5f;
        private float _effectsVolume = 0.5f;
        private float _audioOffset = -55.35f;

        // ─── Customizable Keyboard Bindings ─────────────────────────────────
        private Keys _keyExitGame = Keys.Escape;
        private float _exitHoldTimer = 0f;
        private Keys _keyToggleCover = Keys.Space;
        private Keys _keyStartGame = Keys.Tab;
        private Keys _keyExitGameplay = Keys.RightShift;
        private Keys _keyHitLeft1 = Keys.Q;
        private Keys _keyHitLeft2 = Keys.W;
        private Keys _keyHitRight1 = Keys.O;
        private Keys _keyHitRight2 = Keys.P;
        private bool _isListeningForKey = false;
        private string _listeningActionName = "";

        // ─── Rhythm & Beatmap Parser State ───────────────────────────────────
        private readonly OsuParser _parser = new();
        private readonly OsuScanner _scanner = new OsuScanner();
        private RhythmTracker _rhythmTracker = new RhythmTracker();
        private InterpolatingAudioClock _audioClock = new InterpolatingAudioClock();
        private RhythmIndexer? _rythmIndexer;
        private OsuBeatmap? _beatmap;

        // ─── Star Rating & Performance Caching ──────────────────────────────
        private float _starRating = 0f;

        // ─── Grouped Accordion Playlist State ───────────────────────────────
        private readonly List<BeatmapGroup> _beatmapGroups = new();
        private readonly Queue<Action> _loadQueue = new();

        // ─── Settings & Mod UI Layout Variables ──────────────────────────────
        private float _settingsYOffset = 15f;
        private float _modifiersYOffset = 15f;
        private float _currentDt = 0f;

        // ─── Screen Transition & Menu States ─────────────────────────────────
        private bool _isModifiersOpen = false;
        private bool _isSettingsOpen = false;
        private bool _isAccountOpen = false;
        private bool _isCoverView = false;
        private bool _isStarting = false;
        private float _startTimer = 0f;
        private int _startPhase = 0;
        private bool _isLowPassEnabled = false;
        private float _peekBg = 0f;
        private bool _isDraggingProgressBar = false;
        private float _dragProgress = 0f;
        private string _bgVideoFilename = "";
        private float _videoSeekCooldown = 0f;

        // ─── Tweener Transition Pools ────────────────────────────────────────
        private Tweener _modifiersTweener = AddTween(new Tweener());
        private Tweener _bgTweener = AddTween(new Tweener());
        private Tweener _logoTweener = AddTween(new Tweener());
        private Tweener _settingsTweener = AddTween(new Tweener());
        private Tweener _accountTweener = AddTween(new Tweener());
        private Tweener _startTransitionTweener = AddTween(new Tweener());
        private Tweener _startShrinkTweener = AddTween(new Tweener());
        private Tweener _peekBgTweener = AddTween(new Tweener());
        private Tweener _lowPassTweener = AddTween(new Tweener());

        // ─── Warning Screen Lifecycle ────────────────────────────────────────
        private bool _showWarningScreen = false; // Toggle this to false to skip the warning screen and go straight to the intro
        private bool _inWarningScreen = true;
        private float _warningParentAlpha = 1.0f;
        private float _warningDoneTimer = 0f;
        private int _currentFadeWordIndex = 0;
        private List<WordController> _allWords = new();
        private Frame _warningScreenFrame = null!;

        // ─── Intro Screen Welcome Lifecycle ──────────────────────────────────
        private bool _inIntro = true;
        private float _introAlpha = 0f;
        private float _introTransitionTimer = 0f;
        private bool _transitionFired = false;
        private GridTransitionRadial _welcomeTransition = null!;
        private Tweener _logoRotation = AddTween(new Tweener());

        // ─── Interactive Micro-Animation Lists ─────────────────────────────
        private readonly List<LogoShockwave> _shockwaves = new();

        // ─── Gameplay Modifier Preferences ─────────────────────────────────
        private float _actualMusicSpeed = 1.0f;
        private float _speedMultiplier = 1.0f;
        private bool _adjustPitch = false;
        private bool _modHidden = false;
        private bool _modAutoplay = false;
        private bool _modSingleMode = true;

        // ─── Real-Time Color Smoothing Variables ────────────────────────────
        private Color _currentCoverColor = Color.White;
        private Color _targetCoverColor = Color.White;
        private float _colorR = 255f, _colorG = 255f, _colorB = 255f; // Add these float trackers

        // Audio Transition Machine
        private enum FadeState { None, FadingOut, FadingIn }
        private string _currentAudioKey = "au_0";
        private int _audioCounter = 0;
        private Dictionary<string, Tweener> _audioTweeners = new();

        public void ManualDraw(float dt)
        {
            if (_exitHoldTimer > 0f)
            {
                float progress = Math.Clamp(_exitHoldTimer / 0.5f, 0f, 1f);

                // 1. Full-screen Cinematic Dim
                DrawRectangle(0f, 0f, ScreenWidth, ScreenHeight, new Color(0, 0, 0, (byte)(progress * 180f)));

                // 2. Draw Text: "Holding [Key] to Exit..."
                string text = $"Holding {_keyExitGame} to Exit...";
                ArtFrame.ArtTypes.Vector2 textSize = MeasureText("gsans_bold", text, 20f);
                float textX = (ScreenWidth - textSize.X) / 2f;
                float textY = (ScreenHeight / 2f) - 40f;

                FontHelper.DrawTextPro(
                    "gsans_bold",
                    text,
                    new ArtFrame.ArtTypes.Vector2(textX, textY),
                    new ArtFrame.ArtTypes.Vector2(0f, 0f),
                    0f,
                    20.0f, // scale
                    new Color(255, 255, 255, 230)
                );

                // 3. Draw Progress Bar Background
                float barWidth = 400f;
                float barHeight = 8f;
                float barX = (ScreenWidth - barWidth) / 2f;
                float barY = (ScreenHeight / 2f) + 10f;

                DrawRectangle(barX, barY, barWidth, barHeight, new Color(50, 50, 50, 150));

                // 4. Draw Progress Bar Fill (with beautiful smooth theme color!)
                float fillWidth = barWidth * progress;
                DrawRectangle(barX, barY, fillWidth, barHeight, new Color(_currentCoverColor.R, _currentCoverColor.G, _currentCoverColor.B, 255));
            }
        }

        private float GetActivePlayfieldAlpha()
        {
            if (_activeGameplayMode == GameplayMode.Taiko)
                return _taikofield?.alpha ?? 0f;
            else
                return _stackfield?.alpha ?? 0f;
        }

        private void RestartGameplay()
        {
            _inResultScreen = false;
            _gameplayFinishTimer = 0f;
            _resultscreen?.Hide();

            SetPerformanceMode(_settings.GameplayFps);
            Engine.HighPrecisionLimiter.SetMaxFps(_settings.GameplayFps);

            _startPhase = 3;
            _isStarting = true;
            _startTimer = 0f;
            SetMusicLowPass(_currentAudioKey, false);

            StopMusic(_currentAudioKey);
            SeekMusic(_currentAudioKey, 0f);

            _rythmIndexer?.Reset(0f);

            if (_beatmap != null)
            {
                _beatmap = _parser.Parse(_beatmap.FilePath, metadataOnly: false);
            }

            Action onSplitFinished = () =>
            {
                if (_audioTweeners.ContainsKey(_currentAudioKey))
                {
                    _audioTweeners[_currentAudioKey].Restart(0.5f, _targetVolume, Easing.Fluid, Direction.Out);
                    PlayMusic(_currentAudioKey);
                }
            };

            if (_activeGameplayMode == GameplayMode.Taiko)
            {
                _taikofield?.alpha = 1f; // Show immediately on restart
                _taikofield?.IsAutoplay = _modAutoplay;
                _taikofield?.SingleMode = _modSingleMode;
                _taikofield?.GlobalScale = _settings.GlobalScale;
                _taikofield?.ResetState();
                _taikofield?.LoadBeatmap(_beatmap != null ? _beatmap : null);
                _taikofield?.OnSplitFinished = onSplitFinished;

                _stackfield?.ResetState();
                _stackfield?.alpha = 0f;
                _stackfield?.LoadBeatmap(null);
            }
            else
            {
                _stackfield?.alpha = 1f; // Show immediately on restart
                _stackfield?.IsAutoplay = _modAutoplay;
                _stackfield?.GlobalScale = _settings.GlobalScale;
                _stackfield?.ResetState();
                _stackfield?.LoadBeatmap(_beatmap);
                _stackfield?.OnSplitFinished = onSplitFinished;

                _taikofield?.ResetState();
                _taikofield?.alpha = 0f;
                _taikofield?.LoadBeatmap(null);
            }

            onSplitFinished();
        }
    }
}