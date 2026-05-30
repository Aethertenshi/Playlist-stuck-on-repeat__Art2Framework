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

namespace CoreGame
{
    public class Program
    {
        static void Main() => Engine.Run<MainGame>();
    }

    public partial class MainGame : IArt
    {
        // ─── Game Constants ──────────────────────────────────────────────────
        private const string SongsPath = @"playlists/";
        private const string SettingsFileName = "settings.json";

        // ─── Rendering & Core Visual Components ─────────────────────────────────
        private GaussianBlurEffect _blur = new();
        private TaikoPlayfield _taikofield;
        private ImageButton _logoUI = null!;
        private EffectFrame _blurBgUI = null!;
        private Frame _shockwaveHolder = null!;
        private ScrollingFrame _playlistScroll = null!;
        private ImageFrame _bgImageFrame = null!;

        // ─── Startup & Audio Preferences (Settings) ──────────────────────────
        private GameSettings _settings = new();
        private float _targetVolume = 0.5f;
        private float _effectsVolume = 0.5f;
        private float _audioOffset = -55.35f;

        // ─── Customizable Keyboard Bindings ─────────────────────────────────
        private Keys _keyToggleCover = Keys.Space;
        private Keys _keyStartGame = Keys.Tab;
        private Keys _keyExitGameplay = Keys.RightShift;
        private Keys _keyHitLeft = Keys.W;
        private Keys _keyToggleListenScore = Keys.LeftShift;
        private Keys _keyHitRight = Keys.Q;
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

        // ─── Screen Transition & Menu States ─────────────────────────────────
        private bool _isModifiersOpen = false;
        private bool _isSettingsOpen = false;
        private bool _isCoverView = false;
        private bool _isStarting = false;
        private Frame _scoreboardPanel = null!;
        private bool _isListenScoreMode = false;
        private float _startTimer = 0f;
        private int _startPhase = 0;
        private float _transitionDuration = 2f; // Customize transition speed here (seconds)
        private float _peekBg = 0f;

        // ─── Tweener Transition Pools ────────────────────────────────────────
        private Tweener _modifiersTweener = AddTween(new Tweener());
        private Tweener _bgTweener = AddTween(new Tweener());
        private Tweener _logoTweener = AddTween(new Tweener());
        private Tweener _settingsTweener = AddTween(new Tweener());
        private Tweener _startTransitionTweener = AddTween(new Tweener());
        private Tweener _startShrinkTweener = AddTween(new Tweener());
        private Tweener _listenScoreTweener = AddTween(new Tweener());
        private Tweener _peekBgTweener = AddTween(new Tweener());

        // ─── Intro Screen Welcome Lifecycle ──────────────────────────────────
        private bool _inIntro = true;
        private float _introAlpha = 0f;
        private float _introTransitionTimer = 0f;
       
        private bool _transitionFired = false;
        private GridTransitionRadial _welcomeTransition = null!;
        private Tweener _logoRotation = AddTween(new Tweener());

        // ─── Interactive Micro-Animation Lists ─────────────────────────────
        private readonly List<LogoShockwave> _shockwaves = new();
        private readonly List<MenuParticle> _menuParticles = new();

        // ─── Gameplay Modifier Preferences ─────────────────────────────────
        private float _actualMusicSpeed = 1.0f;
        private float _speedMultiplier = 1.0f;
        private bool _adjustPitch = false;
        private bool _modHidden = false;

        // ─── Real-Time Color Smoothing Variables ────────────────────────────
        private Color _currentCoverColor = Color.White;
        private Color _targetCoverColor = Color.White;
        private float _colorR = 255f, _colorG = 255f, _colorB = 255f; // Add these float trackers

        // Audio Transition Machine
        private enum FadeState { None, FadingOut, FadingIn }
        private string _currentAudioKey = "au_0";
        private int _audioCounter = 0;
        private Dictionary<string, Tweener> _audioTweeners = new();
    }
}