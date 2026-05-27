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

    public class MainGame : IArt
    {
        // Game Constants
        private const string SongsPath = @"C:/Users/YOMARI/Documents/123123/";

        // Game Variables
        private GaussianBlurEffect _blur = new();
        private float _starRating = 0f;
        private float _settingsYOffset = 15f;
        private TaikoPlayfield _taikofield;
        private ImageFrame _logoUI = null!;
        private EffectFrame _blurBgUI = null!;
        private Frame _shockwaveHolder = null!;
        private ScrollingFrame _playlistScroll = null!;
        private ImageFrame _bgImageFrame = null!;
        private static readonly Dictionary<string, float> _starRatingCache = new();
        private const string CacheFileName = "songs_star_cache.json";
        private readonly Queue<Action> _loadQueue = new();
        private GameSettings _settings = new();
        private const string SettingsFileName = "settings.json";
        private float _effectsVolume = 0.5f;
        private float _audioOffset = -55.35f;

        // Rhythm State
        private readonly OsuParser _parser = new();
        private readonly OsuScanner scanner = new OsuScanner();
        private RhythmTracker _rhythmTracker = new RhythmTracker();
        private InterpolatingAudioClock _audioClock = new InterpolatingAudioClock();
        private RhythmIndexer? _rythmIndexer;
        private OsuBeatmap? _beatmap;

        // Transition States
        private Tweener _modifiersTweener = AddTween(new Tweener());
        private Tweener _bgTweener = AddTween(new Tweener());
        private Tweener _logoTweener = AddTween(new Tweener());
        private Tweener _settingsTweener = AddTween(new Tweener());
        private bool _isModifiersOpen = false;
        private bool _isSettingsOpen = false;
        private bool _isCoverView = false;
        private bool _isStarting = false;
        private float _startTimer = 0f;
        private int _startPhase = 0;

        // Intro Screen State
        private bool _inIntro = true;
        private float _introAlpha = 0f;
        private bool _transitionFired = false;
        private GridTransitionRadial _welcomeTransition = null!;
        private Tweener _logoRotation = AddTween(new Tweener());

        // Interactive Visual Suite States
        private readonly List<LogoShockwave> _shockwaves = new();
        private readonly List<MenuParticle> _menuParticles = new();

        // Phase 1: Cover centers, UI hides, BG darkens
        private Tweener _startTransitionTweener = AddTween(new Tweener());

        // Phase 2: Cover and text shrink/fade into the abyss
        private Tweener _startShrinkTweener = AddTween(new Tweener());

        // Mod States
        private float _actualMusicSpeed = 1.0f;
        private float _speedMultiplier = 1.0f;
        private bool _adjustPitch = false;
        private bool _modHidden = false;
        private float _modifiersYOffset = 15f;

        // Color Smoothing
        private Color _currentCoverColor = Color.White;
        private Color _targetCoverColor = Color.White;
        private float _colorR = 255f, _colorG = 255f, _colorB = 255f; // Add these float trackers

        // Audio Transition Machine
        private enum FadeState { None, FadingOut, FadingIn }
        private string _currentAudioKey = "au_0";
        private float _targetVolume = 0.5f;
        private int _audioCounter = 0;
        private Dictionary<string, Tweener> _audioTweeners = new();

        public void Setup()
        {
            // Load persistent star ratings cache
            LoadStarRatingCache();

            // Load persistent game settings
            LoadSettings();

            ConfigureWindow(width: 1920, height: 1080, fullscreen: false);
            SetInputFramerate(300);
            SetFrameRate(120);

            LoadSFX("normal", "sounds/hitsounds/normal-hitnormal.wav");
            LoadSFX("whistle", "sounds/hitsounds/normal-hitwhistle.wav");
            LoadSFX("finish", "sounds/hitsounds/normal-hitfinish.wav");
            LoadSFX("clap", "sounds/hitsounds/normal-hitclap.wav");

            LoadSFX("beat", "sounds/sfxs/logo-heartbeat.wav");
            LoadSFX("dwbeat", "sounds/sfxs/logo-downbeat.wav");
            LoadSFX("hover", "sounds/sfxs/default-hover.wav");
            LoadSFX("select", "sounds/sfxs/default-select.wav");
            LoadSFX("keypress1", "sounds/sfxs/key-press-1.mp3");
            LoadSFX("keypress2", "sounds/sfxs/key-press-2.mp3");
            LoadSFX("keypress3", "sounds/sfxs/key-press-3.mp3");
            LoadSFX("keypress4", "sounds/sfxs/key-press-4.mp3");
            LoadSFX("keydel", "sounds/sfxs/key-delete.mp3");
            LoadSFX("play-click", "sounds/sfxs/menu-play-click.wav");

            // Apply loaded SFX volumes
            SetSFXVolume("hover", _effectsVolume);
            SetSFXVolume("select", _effectsVolume);
            SetSFXVolume("beat", _effectsVolume);
            SetSFXVolume("dwbeat", _effectsVolume);

            LoadAtlasFont("gsans_bold", "fonts/googlesans_bold.json", "fonts/googlesans_bold.png");
            LoadAtlasFont("gsans", "fonts/googlesans.json", "fonts/googlesans.png");

            _bgTweener.SetValue(0f);

            // Intialize Gameplay
            _taikofield = new TaikoPlayfield(LoadImage("circle", "content/hitcircle.png"), "gsans_bold")
            {
                size = new UDim2(1f, 0, 0, 200f),
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                GlobalScale = 1.4f,
                alpha = 0f // Start hidden
            };
            _taikofield.OnPlayHitSound = (hitSoundMask) =>
            {
                PlaySFX("beat");
                if ((hitSoundMask & 2) > 0) PlaySFX("whistle");
                if ((hitSoundMask & 4) > 0) PlaySFX("finish");
                if ((hitSoundMask & 8) > 0) PlaySFX("clap");
            };

            _taikofield.OnExit = () =>
            {
                // 1. Wipe the playfield clean and hide it
                _taikofield.ResetState();

                // 2. Audio Transition: Jump back to menu preview and restore volume!
                SeekMusic(_currentAudioKey, _beatmap.PreviewTime / 1000f);
                if (_audioTweeners.ContainsKey(_currentAudioKey))
                    _audioTweeners[_currentAudioKey].Restart(1.6f, _targetVolume, Easing.Exponential, Direction.Out);

                // 3. Tell the cinematic timeline to run backward
                _isStarting = false;
                _startPhase = 0;

                SetInputFramerate(300);
                SetFrameRate(120);

                //_rythmIndexer = new RhythmIndexer(new InterpolatingAudioClock(), new RhythmTracker(), () => GetMusicTimePlayed(_currentAudioKey)) { Beatmap = _beatmap, MusicOffset = -55.35f };
                _startShrinkTweener.Restart(1.5f, 0f, Easing.Exponential, Direction.Out);
                _startTransitionTweener.Restart(1.5f, 0f, Easing.Exponential, Direction.Out);
            };

            // Bind the update loop directly to the Frame component
            _taikofield.onUpdate = (e, dt) =>
            {
                // ONLY run physics and hit detection if we are actively in the game scene
                if (_startPhase == 3 && _rythmIndexer != null)
                {
                    _taikofield.UpdatePlayfield(dt, _rythmIndexer.CurrentProgress);
                }
            };

            // 1. Scan and Pick Random Beatmap
            var scannedBeatmaps = scanner.ScanLazy(SongsPath).ToList();
            if (scannedBeatmaps.Count > 0)
            {
                var beatmapRand = new Random();
                _beatmap = scannedBeatmaps[beatmapRand.Next(scannedBeatmaps.Count)];
            }
            else
            {
                _beatmap = _parser.Parse("sounds/osu.osu");
            }

            LoadMusic(_currentAudioKey, Path.Combine(Path.GetDirectoryName(_beatmap.FilePath) ?? "", _beatmap.AudioFilename));
            
            Image initialBg = LoadImage(_beatmap.BeatmapSetId.ToString(), _beatmap.GetBackgroundFullPath());
            _targetCoverColor = GetAverageColor(initialBg, 25);
            _colorR = _targetCoverColor.R; _colorG = _targetCoverColor.G; _colorB = _targetCoverColor.B; // Snap floats
            _currentCoverColor = _targetCoverColor;

            // Initialize Grid Transition Radial
            _welcomeTransition = new GridTransitionRadial(Color.Black, fadeOut: true, reverseWave: false, tileSize: 70);
            _welcomeTransition.SetValue(0f); // Screen starts completely black/opaque

            // === L1 UI Elements ===
            Frame bgDrop = new Frame
            {
                size = new UDim2(1f, 1f),
                position = new UDim2(.5f, .5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                onUpdate = (e, dt) =>
                {
                    // 1. Calculate the standard menu color
                    Color menuColor = Color.LerpColor(
                        new Color((byte)(_currentCoverColor.R * 0.7f), (byte)(_currentCoverColor.G * 0.7f), (byte)(_currentCoverColor.B * 0.7f)),
                        _currentCoverColor,
                        _bgTweener.CurrentValue
                    );

                    // 2. Lerp to black when starting
                    e.color = Color.LerpColor(menuColor, Color.Black, _startTransitionTweener.CurrentValue);
                }
            };

            _blurBgUI = new EffectFrame
            {
                position = new UDim2(0.5f, 0.5f),
                size = new UDim2(1f, 1f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                Effect = _blur,
                onUpdate = (e, dt) =>
                {
                    if (_blur != null)
                    {
                        //_blur.BlurAmount = 2.5f * (1f - _bgTweener.CurrentValue);

                        // 1. Dynamically calculate what the "shrunk" target size should be.
                        // If settings is closed (0), the target is 500f. If settings is open (1), it smoothly shrinks to 450f.
                        //float currentTargetSize = ArtMathHelper.Lerp(500f, 450f, _settingsTweener.CurrentValue);

                        // 2. Run your master layout Lerp using the dynamic target size
                        e.size = UDim2.Lerp(UDim2.FromScale(1f, 1f), UDim2.FromOffset(500f, 500f), _bgTweener.CurrentValue);

                        //float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, _settingsTweener.CurrentValue);
                        float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                        float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
                        float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                        e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(currentTargetX, 0.5f), _bgTweener.CurrentValue);

                        //e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(0.38f, 0.5f), _bgTweener.CurrentValue);

                        _blur.BlurAmount = ArtMathHelper.Lerp(0f, 2.5f, 1f - _bgTweener.CurrentValue);
                        e.alpha = 1f - _startShrinkTweener.CurrentValue;
                        e.BypassEffect = _bgTweener.CurrentValue >= 0.99f;

                        // Note: For blurBg, apply this shrink to the size:
                        // e.size = UDim2.Lerp(UDim2.FromScale(1f, 1f), UDim2.FromOffset(500f, 500f), _bgTweener.CurrentValue) * (1f - _startShrinkTweener.CurrentValue);
                    }
                }
            };

            ImageFrame bg = new ImageFrame
            {
                texture = initialBg,
                fit = ObjectFit.Cover,
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(1f, 1f),     // Always 100% of the parent
                position = new UDim2(0f, 0f), // Locked to the top-left of the parent
                onUpdate = (e, dt) =>
                {
                    // 1. Smoothly interpolate Size
                    //e.size = UDim2.Lerp(UDim2.FromScale(1f, 1f), UDim2.FromOffset(500f, 500f), _bgTweener.CurrentValue);

                    // 2. Smoothly interpolate Position (Center -> 1/3 Left)
                    //UDim2 fullScreenPos = UDim2.FromScale(0.5f, 0.5f);
                    //UDim2 coverPos = UDim2.FromScale(0.38f, 0.5f);
                    //e.position = UDim2.Lerp(fullScreenPos, coverPos, _bgTweener.CurrentValue);

                    // 3. Smoothly interpolate Color (Dark gray -> Dynamic Cover Color)
                    e.color = Color.LerpColor(new Color(200, 200, 200), Color.White, _bgTweener.CurrentValue);
                    e.alpha = 1f - _startShrinkTweener.CurrentValue;

                    // Input Polling
                    if ((Keyboard.IsKeyPressed(Keys.Space) || (Mouse.LeftClicked() && !_isCoverView)) && !_isStarting && !_inIntro)
                    {
                        _isCoverView = !_isCoverView;
                        _bgTweener.Restart(duration: 0.7f, targetValue: _isCoverView ? 1.0f : 0f, Easing.Exponential, Direction.Out);
                    }
                }
            };
            _blurBgUI.children.Add(bg);
            _bgImageFrame = bg;

            _shockwaveHolder = new Frame
            {
                position = bg.position,
                anchorX = bg.anchorX,
                anchorY = bg.anchorY,
                alpha = 0,
                onUpdate = (frame, dt) =>
                {
                    frame.size = bg.size;
                }
            };

            // Initialize floating lens bokeh particles (Number 5)
            Random rand = new Random();
            for (int i = 0; i < 25; i++)
            {
                float size = (float)rand.NextDouble() * 15f + 8f; // size between 8px and 23px
                CircleFrame partNode = new CircleFrame
                {
                    color = new Color(255, 255, 255, (byte)rand.Next(75, 100)), // soft glowing alpha
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Center,
                    size = new UDim2(0f, 0f, size, size),
                    position = new UDim2((float)rand.NextDouble(), (float)rand.NextDouble())
                };
                
                _blurBgUI.children.Add(partNode);
                _menuParticles.Add(new MenuParticle
                {
                    VisualNode = partNode,
                    DriftSpeedX = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.45f,
                    DriftSpeedY = (float)(rand.NextDouble() * 2.0 - 1.0) * 0.45f,
                    BaseSize = size,
                    PulsePhase = (float)(rand.NextDouble() * Math.PI * 2)
                });
            }

            _logoUI = new ImageFrame
            {
                texture = LoadImage("logo", "content/logo_game.png"),
                color = new Color(255, 255, 255),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                fit = ObjectFit.Cover,
                onUpdate = (e, dt) =>
                {
                    if (_inIntro)
                    {
                        e.alpha = MathF.Max(0.3f, _introAlpha);
                        e.size = new UDim2(0.4f, 0.4f);
                        e.position = UDim2.FromScale(0.5f, 0.5f);
                        e.rotation = _logoRotation.CurrentValue;
                    }
                    else
                    {
                        // Calculate dynamic size
                        e.size = (new UDim2(0.4f, 0.4f) * MathF.Max(_logoTweener.CurrentValue, _startTransitionTweener.CurrentValue)) * MathF.Max((1f - _bgTweener.CurrentValue), 0.25f);
                        e.rotation = (_logoRotation.CurrentValue * (1f - _bgTweener.CurrentValue));

                        // Match the background's position logic perfectly so it stays centered inside the cover
                        float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                        float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
                        float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                        e.alpha = 1f - _startShrinkTweener.CurrentValue;
                        e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(currentTargetX, 0.5f), _bgTweener.CurrentValue);
                    }
                }
            };

            // === L1.5 Metadata Badges ===

            // 1. Star Rating (Top Left)
            bg.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Top,
                position = new UDim2(0f, 0f, 15f, 15f), // 15px inset from the cover's top-left
                scale = 1.25f,
                color = Color.White,
                backgroundColor = new Color(0, 0, 0), // Black badge
                backgroundAlpha = 0.6f,               // 60% transparency
                backgroundPadding = 6f,               // Gives the text breathing room
                onUpdate = (e, dt) =>
                {
                    e.text = $"{_starRating:F2}";
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue); // Fades in exactly as the cover shrinks
                }
            });

            // 2. BPM (Bottom Left)
            bg.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Bottom,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Bottom,
                position = new UDim2(0f, 1f, 15f, -15f), // 15px inset from the cover's bottom-left
                scale = 1.25f,
                color = Color.White,
                backgroundColor = new Color(0, 0, 0),
                backgroundAlpha = 0.6f,
                backgroundPadding = 6f,
                onUpdate = (e, dt) =>
                {
                    e.text = $"{_beatmap.GetBpmAt(0):F0} BPM";
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);
                }
            });

            // 3. AR & CS (Bottom Right)
            bg.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Bottom,
                textAnchorX = AnchorX.Right,
                textAnchorY = AnchorY.Bottom,
                position = new UDim2(1f, 1f, -15f, -15f), // 15px inset from the cover's bottom-right
                scale = 1.25f,
                color = Color.White,
                backgroundColor = new Color(0, 0, 0),
                backgroundAlpha = 0.6f,
                backgroundPadding = 6f,
                onUpdate = (e, dt) =>
                {
                    string ar = _beatmap.GetDifficulty("ApproachRate", "5.0");
                    string cs = _beatmap.GetDifficulty("CircleSize", "4.0");
                    e.text = $"AR {ar}  |  CS {cs}";
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);
                }
            });

            // 4. Active Mods (Top Right)
            bg.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Right,
                textAnchorY = AnchorY.Top,
                position = new UDim2(1f, 0f, -15f, 15f), // 15px inset from the cover's top-right
                scale = 1.25f,
                color = Color.White,
                backgroundColor = new Color(0, 0, 0),
                backgroundAlpha = 0.6f,
                backgroundPadding = 6f,
                onUpdate = (e, dt) =>
                {
                    string activeMods = "";

                    if (_modHidden) activeMods += "HD ";

                    // Check if speed has been altered
                    if (Math.Abs(_speedMultiplier - 1f) > 0.01f)
                    {
                        activeMods += _speedMultiplier > 1f ? "DT " : "HT ";
                        activeMods += $"({_speedMultiplier:F2}x) ";
                    }

                    if (_adjustPitch) activeMods += "NC "; // Nightcore/Pitch modifier

                    e.text = string.IsNullOrWhiteSpace(activeMods) ? "NM" : activeMods.TrimEnd();
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);
                }
            });

            // === L2 UI Elements ===

            // 1. Song Title
            ArtObject songTitle = new TextFrame
            {
                fontName = "gsans_bold",
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Top,
                scale = 2.4f,
                onUpdate = (e, dt) =>
                {
                    e.text = _beatmap.Title;
                    e.color = new Color((byte)(_currentCoverColor.R * MathF.Max(0.3f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.G * MathF.Max(0.3f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.B * MathF.Max(0.3f, _startTransitionTweener.CurrentValue)));

                    // Drop the opacity slightly when settings is open to declutter center space
                    e.alpha = _bgTweener.CurrentValue * (1f - _settingsTweener.CurrentValue * 0.4f);

                    // Calculate dynamic X layout coordinate
                    //float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, _settingsTweener.CurrentValue);
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
                    float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                    // Swap out the static 0.38f in the second UDim2 container for currentTargetX
                    e.position = UDim2.Lerp(new UDim2(0.38f, 0.5f, -250f, 320f), new UDim2(currentTargetX, 0.5f, -250f, 280f), _bgTweener.CurrentValue);

                    // Fade out normally if panels are open, but also fade out aggressively in Phase 2
                    e.alpha = _bgTweener.CurrentValue
                            * (1f - _settingsTweener.CurrentValue * 0.4f)
                            * (1f - _startShrinkTweener.CurrentValue);
                }
            };

            // 2. Artist Name
            ArtObject songArtist = new TextFrame
            {
                fontName = "gsans",
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Top,
                scale = 1.8f,
                onUpdate = (e, dt) =>
                {
                    e.text = _beatmap.Artist;
                    e.color = new Color((byte)(_currentCoverColor.R * MathF.Max(0.6f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.G * MathF.Max(0.6f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.B * MathF.Max(0.6f, _startTransitionTweener.CurrentValue)));

                    // Match layout opacity behaviors
                    e.alpha = _bgTweener.CurrentValue * (1f - _settingsTweener.CurrentValue * 0.4f);

                    // Calculate dynamic X layout coordinate
                    //float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, _settingsTweener.CurrentValue);
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
                    float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                    // Swap out the static 0.38f in the second UDim2 container for currentTargetX
                    e.position = UDim2.Lerp(new UDim2(0.38f, 0.5f, -250f, 350f), new UDim2(currentTargetX, 0.5f, -250f, 325f), _bgTweener.CurrentValue);

                    // Fade out normally if panels are open, but also fade out aggressively in Phase 2
                    e.alpha = _bgTweener.CurrentValue
                            * (1f - _settingsTweener.CurrentValue * 0.4f)
                            * (1f - _startShrinkTweener.CurrentValue);
                }
            };

            // 3. Progress Bar Track
            Frame progressBarTrack = new Frame
            {
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Top,
                color = new Color(80, 80, 80),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);

                    // Calculate dynamic X layout coordinate
                    //float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, _settingsTweener.CurrentValue);
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);

                    // Swap out the static 0.38f in the second UDim2 container for currentTargetX
                    e.position = UDim2.Lerp(new UDim2(0.38f, 0.5f, 0f, 410f), new UDim2(currentTargetX, 0.5f, 0f, 390f), _bgTweener.CurrentValue);
                    e.size = new UDim2(0f, 0f, 500f * _bgTweener.CurrentValue, 6f);
                }
            };

            // 4. Progress Bar Fill
            ArtObject progressBarFill = new Frame
            {
                position = new UDim2(0f, 0f, 0f, 0f), // Leaving this at 0,0 is correct since it draws from the parent's top-left!
                size = new UDim2(0f, 1f, 0f, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                color = Color.White,
                onUpdate = (e, dt) =>
                {
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);

                    float timePlayed = GetMusicTimePlayed(_currentAudioKey);
                    float totalLength = GetMusicLength(_currentAudioKey);
                    float progress = totalLength > 0 ? timePlayed / totalLength : 0f;

                    e.size = new UDim2(Math.Clamp(progress, 0f, 1f), 1f, 0f, 0f);
                }
            };
            progressBarTrack.children.Add(progressBarFill);
            Add(progressBarTrack);

            // 5. Progress Bar Dot / Handle
            ArtObject progressBarDot = new CircleFrame
            {
                // Anchor from the center of the dot so it sits perfectly over the end of the line
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                color = Color.White,
                onUpdate = (e, dt) =>
                {
                    // Match the general panel fade animation
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);

                    // 1. Calculate the active progress ratio
                    float timePlayed = GetMusicTimePlayed(_currentAudioKey);
                    float totalLength = GetMusicLength(_currentAudioKey);
                    float progress = totalLength > 0 ? Math.Clamp(timePlayed / totalLength, 0f, 1f) : 0f;

                    // 2. Position the dot relative to the track's width
                    // Since it's a child of progressBarTrack, Scale X goes from 0.0 (left) to 1.0 (right).
                    // Center it vertically on the track by setting Scale Y to 0.5f (50%).
                    e.position = new UDim2(progress, 0.5f, 0.5f, 0f);

                    // 3. Size the dot (14x14 pixels works perfectly for a clean look)
                    e.size = new UDim2(0f, 0f, 20f, 20f);
                }
            };
            progressBarTrack.children.Add(progressBarDot);

            // 6. Time Played Text
            ArtObject timePlayed = new TextFrame
            {
                fontName = "gsans",
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Top,
                scale = 1.35f,
                onUpdate = (e, dt) =>
                {
                    e.color = new Color((byte)(_currentCoverColor.R * 0.4f), (byte)(_currentCoverColor.G * 0.4f), (byte)(_currentCoverColor.B * 0.4f));
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);

                    // Calculate dynamic X layout coordinate
                    //float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, _settingsTweener.CurrentValue);
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);

                    // Swap out the static 0.38f in the second UDim2 container for currentTargetX
                    e.position = UDim2.Lerp(new UDim2(0.38f, 0.5f, -250f, 425f), new UDim2(currentTargetX, 0.5f, -250f, 405f), _bgTweener.CurrentValue);

                    float time = GetMusicTimePlayed(_currentAudioKey);
                    e.text = $"{(int)(time / 60)}:{(int)(time % 60):D2}";
                }
            };

            // 7. Time Remaining Text
            ArtObject timeRemaining = new TextFrame
            {
                fontName = "gsans",
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Right,
                textAnchorY = AnchorY.Top,
                scale = 1.35f,
                onUpdate = (e, dt) =>
                {
                    e.color = new Color((byte)(_currentCoverColor.R * 0.4f), (byte)(_currentCoverColor.G * 0.4f), (byte)(_currentCoverColor.B * 0.4f));
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);

                    // Calculate dynamic X layout coordinate
                    //float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, _settingsTweener.CurrentValue);
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float currentTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);

                    // Swap out the static 0.38f in the second UDim2 container for currentTargetX
                    e.position = UDim2.Lerp(new UDim2(0.38f, 0.5f, 250f, 425f), new UDim2(currentTargetX, 0.5f, 250f, 405f), _bgTweener.CurrentValue);

                    float timePlayed = GetMusicTimePlayed(_currentAudioKey);
                    float totalLength = GetMusicLength(_currentAudioKey);
                    float left = MathF.Max(0f, totalLength - timePlayed);
                    e.text = $"-{(int)(left / 60)}:{(int)(left % 60):D2}";
                }
            };

            // === TopBar ===
            Frame topBar = new Frame
            {
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(1f, 0f, 0f, 60f),
                modifiers = new List<IFrameModifier>
                {
                    new ListLayout{ direction = Axis.Horizontal, horizontalAlign = HAlign.Left, verticalAlign = VAlign.Center, spacing = 15f, controlCrossAxis = true }
                },
                onUpdate = (e, dt) =>
                {
                    e.color = new Color((byte)(_currentCoverColor.R * 0.85f), (byte)(_currentCoverColor.G * 0.85f), (byte)(_currentCoverColor.B * 0.85f), 100);
                    e.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);
                    e.position = UDim2.Lerp(new UDim2(0f, 0f, 0f, -60f), new UDim2(0f, 0f, 0f, 0f), _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue));
                }
            };

            // 1. Local variables to track the current scale of each button
            float backHoverScale = 1f;
            float settingsHoverScale = 1f;
            float modifiersHoverScale = 1f;

            // 2. The Account Button
            Button accountBtn = new Button
            {
                size = new UDim2(0f, 1f, 120f, 0f),
                onHoverEnter = (btn) =>
                {
                    PlaySFX("hover");
                },
                onUpdate = (btn) =>
                {
                    // The same lerp logic from your song list
                    float targetScale = btn.IsHovered ? 1.32f : 1f;
                    backHoverScale = ArtMathHelper.Lerp(backHoverScale, targetScale, 0.05f);

                    // Scale the width (Offset X)
                    btn.size = new UDim2(0f, 1f, 120f * backHoverScale, 0f);

                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);

                    // 3. Apply the colors dynamically
                    btn.color = new Color(r, g, b, 175);
                    btn.hoverColor = new Color(r, g, b, 235);
                    btn.pressedColor = new Color(r, g, b, 255);
                }
            };

            accountBtn.children.Add(new TextFrame
            {
                text = "Account",
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.5f,
                color = Color.White
            });
            topBar.children.Add(accountBtn);

            // 3. The Settings Button
            Button settingsBtn = new Button
            {
                size = new UDim2(0f, 1f, 120f, 0f),
                onHoverEnter = (btn) =>
                {
                    PlaySFX("hover");
                },
                onClick = (btn) =>
                {
                    PlaySFX("select");
                    _isSettingsOpen = !_isSettingsOpen;

                    // Hide Modifiers panel if it's open
                    if (_isSettingsOpen && _isModifiersOpen)
                    {
                        _isModifiersOpen = false;
                        _modifiersTweener.Restart(0.8f, 0f, Easing.Exponential, Direction.Out);
                    }

                    _settingsTweener.Restart(duration: 0.8f, targetValue: _isSettingsOpen ? 1.0f : 0f, Easing.Exponential, Direction.Out);
                },
                onUpdate = (btn) =>
                {
                    float targetScale = btn.IsHovered ? 1.32f : 1f;
                    settingsHoverScale = ArtMathHelper.Lerp(settingsHoverScale, targetScale, 0.05f);
                    btn.size = new UDim2(0f, 1f, 120f * settingsHoverScale, 0f);

                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);

                    // 3. Apply the colors dynamically
                    btn.color = new Color(r, g, b, 175);
                    btn.hoverColor = new Color(r, g, b, 235);
                    btn.pressedColor = new Color(r, g, b, 255);
                }
            };

            settingsBtn.children.Add(new TextFrame
            {
                text = "Settings",
                fontName = "gsans",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.5f,
                color = Color.White
            });
            topBar.children.Add(settingsBtn);

            // 4. The Modifiers Button
            Button modifiersBtn = new Button
            {
                size = new UDim2(0f, 1f, 120f, 0f),
                onHoverEnter = (btn) =>
                {
                    PlaySFX("hover");
                },
                onUpdate = (btn) =>
                {
                    float targetScale = btn.IsHovered ? 1.32f : 1f;
                    modifiersHoverScale = ArtMathHelper.Lerp(modifiersHoverScale, targetScale, 0.05f);
                    btn.size = new UDim2(0f, 1f, 120f * modifiersHoverScale, 0f);

                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);

                    // 3. Apply the colors dynamically
                    btn.color = new Color(r, g, b, 175);
                    btn.hoverColor = new Color(r, g, b, 235);
                    btn.pressedColor = new Color(r, g, b, 255);
                },
                onClick = (btn) =>
                {
                    PlaySFX("select");
                    _isModifiersOpen = !_isModifiersOpen;

                    // Hide Settings panel if it's open
                    if (_isModifiersOpen && _isSettingsOpen)
                    {
                        _isSettingsOpen = false;
                        _settingsTweener.Restart(0.8f, 0f, Easing.Exponential, Direction.Out);
                    }

                    _modifiersTweener.Restart(duration: 0.8f, targetValue: _isModifiersOpen ? 1.0f : 0f, Easing.Exponential, Direction.Out);
                },
            };

            modifiersBtn.children.Add(new TextFrame
            {
                text = "Modifiers",
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.5f,
                color = Color.White
            });
            topBar.children.Add(modifiersBtn);

            // === Playlist Scroll ===
            ScrollingFrame playlistScroll = new ScrollingFrame
            {
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,
                size = new UDim2(0f, 1f, 510f, -60f),
                scrollDirection = Axis.Vertical,
                showScrollbar = false,
                scrollbarColor = new Color(255, 255, 255, 100),
                smoothing = 8f,
                scrollSensitivity = 55,
                clipMode = ClipMode.Clip,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    e.position = UDim2.Lerp(new UDim2(1f, 0f, 510f, 60f), new UDim2(1f, 0f, 0f, 60f), _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue));
                }
            };

            _playlistScroll = playlistScroll;
            _starRating = GetRealStarRating(_beatmap);

            // === Modifiers Panel ===
            ScrollingFrame modifiersPanel = new ScrollingFrame
            {
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(0f, 1f, 480f, -60f),
                scrollDirection = Axis.Vertical,
                showScrollbar = false,
                smoothing = 18f,
                clipMode = ClipMode.None,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    // Smoothly interpolate positions
                    e.position = UDim2.Lerp(new UDim2(0f, 0, -480f, 60f), new UDim2(0f, 0f, 0f, 60f), MathF.Min(_modifiersTweener.CurrentValue, _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue)));
                    e.alpha = _modifiersTweener.CurrentValue;
                    e.color = new Color((byte)(_currentCoverColor.R * 0.85f), (byte)(_currentCoverColor.G * 0.85f), (byte)(_currentCoverColor.B * 0.85f), 100);
                }
            };

            // 0. Header
            Frame modifiersTitle = new Frame
            {
                position = new UDim2(0f, 0f, 0f, _settingsYOffset),
                size = new UDim2(1f, 0f, 0f, 45f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                onUpdate = (e, dt) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);

                    // 3. Apply the colors dynamically
                    e.color = new Color(r, g, b, 175);
                }
            };
            modifiersTitle.children.Add(new TextFrame
            {
                text = "Modifiers",
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f, 0, 0f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.8f,
                color = Color.White
            });
            modifiersPanel.children.Add(modifiersTitle);
            _modifiersYOffset += 50f;

            // 1. Double Time (Speed) Slider
            SliderFrame sliderSpeed = new SliderFrame
            {
                fontName = "gsans_bold",
                title = "Speed Multiplier",
                valueFormat = "0.00x",
                fontScale = 1.35f,
                position = new UDim2(0.5f, 0f, 0f, _modifiersYOffset),
                size = new UDim2(.9f, 0f, 0f, 75f),
                fillColor = new Color(230, 230, 230),
                resetBtnColor = new Color(230, 230, 230),
                resetBtnHoverColor = Color.White,
                handleColor = Color.White,
                handleWidth = 15f,
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Top,
                minValue = 0.5f,
                maxValue = 2.0f,
                defaultValue = 1.0f,
                currentValue = _speedMultiplier,
                onUpdate = (e, dt) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);
                    e.trackColor = new Color(r, g, b, 175);
                    e.resetBtnColor = new Color(r, g, b, 255);
                },
                onSlide = (e) =>
                {
                    _speedMultiplier = e.currentValue;
                },
                onValueChanges = (e) =>
                {
                    _speedMultiplier = e.currentValue;
                },
            };
            modifiersPanel.children.Add(sliderSpeed);
            _modifiersYOffset += 80f;

            // 2. Adjust Pitch Toggle
            modifiersPanel.children.Add(CreateModToggle("Adjust Pitch", _modifiersYOffset, () => _adjustPitch, (val) => { _adjustPitch = val; SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch); }));
            _modifiersYOffset += 60f;

            // 3. Hidden Toggle
            modifiersPanel.children.Add(CreateModToggle("Hidden", _modifiersYOffset, () => _modHidden, (val) => { _modHidden = val; SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch); }));
            _modifiersYOffset += 60f;

            // --- Settings Panel ---
            ScrollingFrame settingsPanel = new ScrollingFrame
            {
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(0f, 1f, 480f, -60f), // Match the exact footprint of your song list
                scrollDirection = Axis.Vertical,
                showScrollbar = false,
                smoothing = 18f,
                clipMode = ClipMode.None,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    // Smoothly interpolate positions from tucked away (-510px) to resting at the left edge (0px)
                    e.position = UDim2.Lerp(new UDim2(0f, 0, -480f, 60f), new UDim2(0f, 0f, 0f, 60f), MathF.Min(_settingsTweener.CurrentValue, _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue)));
                    e.alpha = _settingsTweener.CurrentValue;

                    // Pull dynamic color mutations matching your global album art tint machine
                    e.color = new Color((byte)(_currentCoverColor.R * 0.85f), (byte)(_currentCoverColor.G * 0.85f), (byte)(_currentCoverColor.B * 0.85f), 100);
                }
            };

            // --- Dummy Prototype Settings Rows ---
            string[] options = { "Volumes", "Audio Offset", "Key Bindings", "Graphics Config" };
            foreach (var optionName in options)
            {
                Frame optionRow = new Frame
                {
                    position = new UDim2(0f, 0f, 0f, _settingsYOffset),
                    size = new UDim2(1f, 0f, 0f, 45f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.85f);
                        byte g = (byte)(_currentCoverColor.G * 0.85f);
                        byte b = (byte)(_currentCoverColor.B * 0.85f);

                        // 3. Apply the colors dynamically
                        e.color = new Color(r, g, b, 175);
                    }
                };

                optionRow.children.Add(new TextFrame
                {
                    text = optionName,
                    fontName = "gsans_bold",
                    position = new UDim2(0.5f, 0.5f, 0, 0f),
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Center,
                    textAnchorY = AnchorY.Center,
                    scale = 1.8f,
                    color = Color.White
                });

                settingsPanel.children.Add(optionRow);
                _settingsYOffset += 50f; // Stack layout down cleanly
                AddSettingsMenu(settingsPanel, optionName);
            };

            // --- Testing ---
            //int lastText = 0;
            //TextBoxFrame nameBox = new TextBoxFrame
            //{
            //    position = new UDim2(0.5f, 0.5f, 0, 0),
            //    size = new UDim2(0, 0, 300, 50),
            //    anchorX = AnchorX.Center,
            //    anchorY = AnchorY.Center,
            //    placeholder = "Enter your name...",
            //    fontName = "gsans",
            //    fontScale = 1.25f,
            //    maxLength = 32,
            //    //onEnter = box => Console.WriteLine($"Submitted: {box.currentText}"),
            //    //onFocusLost = box => Console.WriteLine(box.currentText),
            //    onTextChanged = (box) =>
            //    {
            //        if (lastText > box.currentText.Length)
            //            PlaySFX("keydel");
            //        else
            //            PlaySFX($"keypress{_random.Next(3) + 1}");

            //        lastText = box.currentText.Length;
            //    },
            //};

            // --- Drawing Index ---
            Add(bgDrop);

            Add(songTitle);
            Add(songArtist);
            Add(playlistScroll);
            Add(topBar);
            Add(timeRemaining);
            Add(timePlayed);
            Add(progressBarTrack);

            Add(_blurBgUI);
            Add(_welcomeTransition); // Renders over the background but behind the logo
            Add(_shockwaveHolder);

            Add(_logoUI);

            Add(settingsPanel);
            Add(modifiersPanel);
            Add(_taikofield);

            // Populate Playlist
            float currentYOffset = 10f;
            int scannedCount = 0;
            foreach (OsuBeatmap bm in scanner.ScanLazy(SongsPath))
            {
                var button = CreateSongRow(bm, scannedCount, currentYOffset, bg);
                playlistScroll.children.Add(button);
                currentYOffset += 90f;
                scannedCount++;
            }

            // Initialize Rhythm Indexer early so it's not null when added to helperPool
            _rythmIndexer = new RhythmIndexer(_audioClock, _rhythmTracker, () => GetMusicTimePlayed(_currentAudioKey))
            {
                Beatmap = _beatmap,
                MusicOffset = _audioOffset
            };
            _rythmIndexer.OnBeat += (beatIndex) =>
            {
                if (_inIntro) return;
                
                if (!_isCoverView)
                {
                    PlaySFX(_rythmIndexer.IsDownbeat ? "dwbeat" : "beat");
                    
                    // Spawn a logo shockwave on downbeats (Number 4)
                    if (_rythmIndexer.IsDownbeat)
                    {
                        var waveNode = new ImageFrame
                        {
                            texture = LoadImage("logo"),
                            color = new Color(255, 255, 255), // Full glowing white
                            anchorX = AnchorX.Center,
                            anchorY = AnchorY.Center,
                            fit = ObjectFit.Cover,
                            alpha = 0.7f
                        };
                        _shockwaveHolder.children.Add(waveNode); // RENDER BEHIND LOGO AND INHERIT LENS BLUR!
                        _shockwaves.Add(new LogoShockwave { VisualNode = waveNode, Progress = 0f });
                    }
                }
                
                if (_rythmIndexer.IsDownbeat)
                    _logoTweener.SetValue(.92f);
                else
                    _logoTweener.SetValue(.97f);
                _logoTweener.Restart(1.8f, 1f, Easing.Fluid, Direction.Out);
            };

            AddHelper(_rythmIndexer);

            // Setup and Play Welcome intro audio
            LoadMusic("welcome", "sounds/sfxs/welcome.wav");
            PlayMusic("welcome");
            SetMusicVolume("welcome", _targetVolume);

            // Load and pause the selected beatmap preview audio
            SetMusicVolume(_currentAudioKey, 0f);
            StopMusic(_currentAudioKey);

            Tweener initialTweener = AddTween(new Tweener());
            initialTweener.SetValue(0f); // Starts at 0 volume
            _audioTweeners[_currentAudioKey] = initialTweener;

            // Initialize OS Drag-and-Drop Handler
            OszDropHandler.Initialize();
        }

        // --- Custom Game Loop for State Management ---
        public void Update(float dt)
        {
            // Drain the OS drag-and-drop file queue
            OszDropHandler.DrainQueue(ProcessDroppedFile);

            // Process at most one lazy load task per frame to prevent main-thread stuttering
            if (_loadQueue.Count > 0)
            {
                var loadAction = _loadQueue.Dequeue();
                try { loadAction(); } catch { }
            }

            if (_inIntro)
            {
                float played = GetMusicTimePlayed("welcome");
                float length = GetMusicLength("welcome");

                if (length > 0)
                {
                    float progress = played / length;
                    _introAlpha = Math.Clamp(progress * 1.01f, 0f, 1f); // Smooth logo fade-in

                    // Trigger GridTransitionRadial at 95% completion of welcome.wav
                    if (progress >= 0.915f && !_transitionFired)
                    {
                        _transitionFired = true;
                        _welcomeTransition.Play(1.5f, Easing.Cubic, Direction.Out);
                        _logoRotation.Start(2f, 0f, -3.7f, Easing.Exponential, Direction.Out);

                        // Play randomly selected beatmap music preview
                        PlayMusic(_currentAudioKey);
                        SeekMusic(_currentAudioKey, _beatmap.PreviewTime / 1000f);
                        if (_audioTweeners.ContainsKey(_currentAudioKey))
                            _audioTweeners[_currentAudioKey].Restart(3.5f, _targetVolume, Easing.Cubic, Direction.Out);
                    }

                    // Complete intro and play selected song
                    if (progress >= 0.97f || (_transitionFired && !_welcomeTransition.IsPlaying))
                    {
                        _inIntro = false;
                        StopMusic("welcome");
                        _logoTweener.SetValue(1.0f);
                    }
                }
            }

            // Smoothly approach the target color using floats to prevent byte-truncation getting stuck
            _colorR += (_targetCoverColor.R - _colorR) * (dt * 5f);
            _colorG += (_targetCoverColor.G - _colorG) * (dt * 5f);
            _colorB += (_targetCoverColor.B - _colorB) * (dt * 5f);

            _currentCoverColor = new Color((byte)Math.Clamp(_colorR, 0, 255), (byte)Math.Clamp(_colorG, 0, 255), (byte)Math.Clamp(_colorB, 0, 255));

            // Decay Background Beat Scale back to 1.0f

            // Update Bokeh Particles (Number 5)
            float particleSpeedMultiplier = _isCoverView ? 0f : (1f - _startTransitionTweener.CurrentValue);
            
            foreach (var part in _menuParticles)
            {
                float xOffset = part.DriftSpeedX * dt * 45f * particleSpeedMultiplier;
                float yOffset = part.DriftSpeedY * dt * 45f * particleSpeedMultiplier;
                
                float newScaleX = part.VisualNode.position.ScaleX + (xOffset / 1920f);
                float newScaleY = part.VisualNode.position.ScaleY + (yOffset / 1080f);
                
                if (newScaleX < -0.05f) newScaleX = 1.05f;
                if (newScaleX > 1.05f) newScaleX = -0.05f;
                if (newScaleY < -0.05f) newScaleY = 1.05f;
                if (newScaleY > 1.05f) newScaleY = -0.05f;
                
                part.VisualNode.position = UDim2.FromScale(newScaleX, newScaleY);
                part.PulsePhase += dt * 2.0f;
                
                float dynamicSize = part.BaseSize * (1.0f + MathF.Sin(part.PulsePhase) * 0.12f);
                part.VisualNode.size = new UDim2(0f, 0f, dynamicSize, dynamicSize);
                
                float introFactor = _inIntro ? _introAlpha : 1f;
                float viewFactor = _isCoverView ? 0f : (1f - _startTransitionTweener.CurrentValue);
                part.VisualNode.alpha = introFactor * viewFactor * 0.7f; // keep them soft
            }

            // Update Logo Shockwaves (Number 4)
            for (int i = _shockwaves.Count - 1; i >= 0; i--)
            {
                var wave = _shockwaves[i];
                wave.Progress += dt * 2.0f; // completes in ~500ms
                if (wave.Progress >= 1f)
                {
                    _shockwaveHolder.children.Remove(wave.VisualNode); // REMOVE FROM BLURBG
                    _shockwaves.RemoveAt(i);
                }
                else
                {
                    // Scale it much larger so it extends far beyond the logo's boundaries!
                    float scaleMultiplier = 1f + wave.Progress * 0.125f; // expands to 1.125x scale!
                    float baseSizeScale = 0.4f * MathF.Max(_logoTweener.CurrentValue, _startTransitionTweener.CurrentValue);
                    wave.VisualNode.size = new UDim2(baseSizeScale * scaleMultiplier, baseSizeScale * scaleMultiplier);
                    
                    // Center inside blurBg (which aligns perfectly with logo center)
                    wave.VisualNode.position = UDim2.FromScale(0.5f, 0.5f);
                    wave.VisualNode.rotation = _logoRotation.CurrentValue;
                    wave.VisualNode.alpha = (1f - wave.Progress) * 0.45f;
                }
            }

            // --- Dynamic Audio Speed ---
            if (Math.Abs(_actualMusicSpeed - _speedMultiplier) > 0.0001f)
            {
                // Exponential decay smoothing (feels natural for audio)
                _actualMusicSpeed += (_speedMultiplier - _actualMusicSpeed) * (dt * 8f);

                // Snap to target if it gets extremely close to save CPU calls
                if (Math.Abs(_actualMusicSpeed - _speedMultiplier) <= 0.001f)
                {
                    _actualMusicSpeed = _speedMultiplier;
                }

                // Push the smoothed value to BASS
                SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch);
            }

            // --- Game Start Sequence (Press TAB) ---
            if (!_isStarting && Keyboard.IsKeyPressed(Keys.Tab) && _isCoverView)
            {
                SetInputFramerate(900);
                SetFrameRate(500);

                PlaySFX("play-click"); // Optional feedback
                _isStarting = true;
                _startPhase = 1;
                _startTimer = 0f;

                // 1. Force close any open side panels
                _settingsTweener.Restart(0.5f, 0f, Easing.Exponential, Direction.Out);
                _modifiersTweener.Restart(0.5f, 0f, Easing.Exponential, Direction.Out);

                // 2. Trigger Phase 1 (UI Fades out, Cover slides to center, bgDrop darkens)
                _startTransitionTweener.Restart(1.5f, 1.0f, Easing.Exponential, Direction.Out);

                // 3. Fade out the music smoothly
                if (_audioTweeners.ContainsKey(_currentAudioKey))
                    _audioTweeners[_currentAudioKey].Restart(1.5f, 0f, Easing.Exponential, Direction.Out);
            }

            if (_isStarting)
            {
                _startTimer += dt;

                // Wait 1.5 seconds, then trigger Phase 2 (The Shrink)
                if (_startPhase == 1 && _startTimer >= 1.5f)
                {
                    _startPhase = 2;
                    _startShrinkTweener.Restart(1.2f, 1.0f, Easing.Exponential, Direction.In);
                }
                // Wait another 1.5 seconds, then load the game
                else if (_startPhase == 2 && _startTimer >= 3.0f)
                {
                    _startPhase = 3;

                    // TODO: ENTER GAMEPLAY SCENE
                    //Console.WriteLine("/// TRANSITION FINISHED: LOAD GAMEPLAY STATE ///");

                    if (_audioTweeners.ContainsKey(_currentAudioKey))
                    {
                        StopMusic(_currentAudioKey);
                        _audioTweeners[_currentAudioKey].Restart(0.5f, _targetVolume, Easing.Exponential, Direction.Out);
                        SeekMusic(_currentAudioKey, 0f);
                        PlayMusic(_currentAudioKey);
                    }

                    // 1. Recycle the existing rhythm indexer and tell it to wait for 0.0s!
                    _rythmIndexer.Beatmap = _beatmap;
                    _rythmIndexer.MusicOffset = -55.35f;
                    _rythmIndexer.Reset(0f); // Uses your InterpolatingAudioClock's built in Reset[cite: 15]

                    // 2. Wipe any old state and load the new notes
                    _taikofield.alpha = 0f;
                    _taikofield.ResetState();
                    _taikofield.LoadBeatmap(_beatmap);
                }
            }

            // --- Dynamic Audio Crossfading ---
            var keys = _audioTweeners.Keys.ToList();
            foreach (var key in keys)
            {
                var tweener = _audioTweeners[key];

                // Always apply volume if the tweener is actively calculating
                if (tweener.IsPlaying)
                {
                    SetMusicVolume(key, tweener.CurrentValue);
                }
                // Cleanup finished fade-outs to save memory and audio channels
                else if (tweener.CurrentValue <= 0f && key != _currentAudioKey)
                {
                    _audioTweeners.Remove(key);
                    TweenHelper.Remove(tweener);
                    StopMusic(key);

                    // NOTE: If your AudioHelper has a StopMusic(key) or UnloadMusic(key) method, 
                    // call it right here to completely free the audio stream!
                }
            }
        }

        // --- Helper Functions ---
        private void AddSettingsMenu(ScrollingFrame settingsPanel, string currentPage = "")
        {
            if (currentPage != "" && currentPage == "Volumes")
            {
                // --- Main Volume ---
                SliderFrame sliderMainVolume = new SliderFrame
                {
                    fontName = "gsans_bold",
                    title = "Main Volume",
                    fontScale = 1.35f,
                    position = new UDim2(0.5f, 0f, 0f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 75f),
                    fillColor = new Color(230, 230, 230),
                    resetBtnColor = new Color(230, 230, 230),
                    resetBtnHoverColor = Color.White,
                    handleColor = Color.White,
                    handleWidth = 15f,
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Top,
                    currentValue = _targetVolume,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.85f);
                        byte g = (byte)(_currentCoverColor.G * 0.85f);
                        byte b = (byte)(_currentCoverColor.B * 0.85f);

                        // 3. Apply the colors dynamically
                        e.trackColor = new Color(r, g, b, 175);
                        e.resetBtnColor = new Color(r, g, b, 255);
                    },
                    onValueChanges = (e) =>
                    {
                        _targetVolume = e.currentValue;
                        _audioTweeners[_currentAudioKey].Restart(0.5f, _targetVolume, Easing.Cubic, Direction.Out);
                        SaveSettings();
                    }
                };
                settingsPanel.children.Add(sliderMainVolume);
                _settingsYOffset += 80f;

                // --- Effects Volume ---
                SliderFrame sliderEffectVolume = new SliderFrame
                {
                    fontName = "gsans_bold",
                    title = "Effects Volume",
                    fontScale = 1.35f,
                    position = new UDim2(0.5f, 0f, 0f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 75f),
                    fillColor = new Color(230, 230, 230),
                    resetBtnColor = new Color(230, 230, 230),
                    resetBtnHoverColor = Color.White,
                    handleColor = Color.White,
                    handleWidth = 15f,
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Top,
                    currentValue = _effectsVolume,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.85f);
                        byte g = (byte)(_currentCoverColor.G * 0.85f);
                        byte b = (byte)(_currentCoverColor.B * 0.85f);

                        // 3. Apply the colors dynamically
                        e.trackColor = new Color(r, g, b, 175);
                        e.resetBtnColor = new Color(r, g, b, 255);
                    },
                    onValueChanges = (e) =>
                    {
                        _effectsVolume = e.currentValue;
                        SetSFXVolume("hover", e.currentValue);
                        SetSFXVolume("select", e.currentValue);
                        SetSFXVolume("beat", e.currentValue);
                        SetSFXVolume("dwbeat", e.currentValue);
                        SaveSettings();
                    }
                };
                settingsPanel.children.Add(sliderEffectVolume);
                _settingsYOffset += 80f;
            }
            
            if (currentPage != "" && currentPage == "Audio Offset")
            {
                // --- Audio Offset ---
                SliderFrame sliderAudioOffset = new SliderFrame
                {
                    fontName = "gsans_bold",
                    title = "Audio Offset",
                    fontScale = 1.35f,
                    position = new UDim2(0.5f, 0f, 0f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 75f),
                    fillColor = new Color(230, 230, 230),
                    resetBtnColor = new Color(230, 230, 230),
                    resetBtnHoverColor = Color.White,
                    handleColor = Color.White,
                    handleWidth = 15f,
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Top,
                    minValue = -80f,
                    maxValue = 80f,
                    defaultValue = 0,
                    currentValue = _audioOffset,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.85f);
                        byte g = (byte)(_currentCoverColor.G * 0.85f);
                        byte b = (byte)(_currentCoverColor.B * 0.85f);

                        // 3. Apply the colors dynamically
                        e.trackColor = new Color(r, g, b, 175);
                        e.resetBtnColor = new Color(r, g, b, 255);
                    },
                    onValueChanges = (e) =>
                    {
                        _audioOffset = e.currentValue;
                        if (_rythmIndexer != null)
                        {
                            _rythmIndexer.MusicOffset = e.currentValue;
                        }
                        SaveSettings();
                    }
                };
                settingsPanel.children.Add(sliderAudioOffset);
                _settingsYOffset += 80f;
            }
        }

        private Button CreateModToggle(string title, float yOffset, Func<bool> getState, Action<bool> setState)
        {
            Button toggleBtn = new Button
            {
                position = new UDim2(0.5f, 0f, 0f, yOffset),
                size = new UDim2(.9f, 0f, 0f, 45f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Top,
                onClick = (b) =>
                {
                    PlaySFX("select");
                    setState(!getState()); // Flip the boolean
                },
                onHoverEnter = (b) => PlaySFX("hover"),
                onUpdate = (e) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);

                    bool isActive = getState();

                    // Highlight solid white if active, otherwise dim dynamic tint
                    e.color = isActive ? new Color(255, 255, 255, 200) : new Color(r, g, b, 175);
                    e.hoverColor = isActive ? new Color(255, 255, 255, 255) : new Color(r, g, b, 235);
                    e.pressedColor = new Color(255, 255, 255, 255);
                }
            };

            toggleBtn.children.Add(new TextFrame
            {
                text = title,
                fontName = "gsans_bold",
                position = new UDim2(0.05f, 0.5f, 0, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Center,
                scale = 1.35f,
                onUpdate = (t, dt) =>
                {
                    // Dark text if the button is highlighted, white if dim
                    t.color = getState() ? Color.Black : Color.White;
                }
            });

            return toggleBtn;
        }

        private void ProcessDroppedFile(string path)
        {
            Console.WriteLine($"[MainGame] Dropped file detected: {path}");
            if (string.IsNullOrEmpty(path)) return;

            OsuBeatmap? newBeatmap = null;

            try
            {
                // 1. If it's a .osz file
                if (path.EndsWith(".osz", StringComparison.OrdinalIgnoreCase))
                {
                    string? extractedFolder = OszImporter.Import(path, SongsPath);
                    if (extractedFolder != null && Directory.Exists(extractedFolder))
                    {
                        var osuFiles = Directory.GetFiles(extractedFolder, "*.osu", SearchOption.AllDirectories);
                        if (osuFiles.Length > 0)
                        {
                            newBeatmap = _parser.Parse(osuFiles[0]);
                        }
                        else
                        {
                            Console.WriteLine($"[MainGame] No .osu files found in extracted .osz folder: {extractedFolder}");
                        }
                    }
                }
                // 2. If it's a .osu file directly
                else if (path.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                {
                    newBeatmap = _parser.Parse(path);
                }
                else
                {
                    Console.WriteLine($"[MainGame] Unsupported dropped file type: {path}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error processing dropped file: {ex.Message}");
            }

            if (newBeatmap != null)
            {
                Console.WriteLine($"[MainGame] Successfully processed dropped beatmap: {newBeatmap.Title}");

                // Load background image texture
                string bgPath = newBeatmap.GetBackgroundFullPath();
                LoadImage(newBeatmap.BeatmapSetId.ToString(), bgPath);

                // Add song button to scrolling playlist UI
                float yOffset = 10f + _playlistScroll.children.Count * 90f;
                var button = CreateSongRow(newBeatmap, _playlistScroll.children.Count, yOffset, _bgImageFrame);
                _playlistScroll.children.Add(button);

                // Instantly select, load, play the new song and update star rating!
                ChangeSong(newBeatmap, _bgImageFrame);
                _starRating = GetRealStarRating(newBeatmap);
                PlaySFX("select");
            }
        }

        private void ChangeSong(OsuBeatmap targetMap, ImageFrame? bg = null)
        {
            if (targetMap == _beatmap) return; // Prevent clicking the song that is already playing

            // 1. Fade out the CURRENT song (if it exists)
            if (_audioTweeners.ContainsKey(_currentAudioKey))
            {
                // Retarget the existing tweener to 0. 
                // Because of your Tweener.Restart logic, it smoothly fades down from its CURRENT volume! No snapping!
                _audioTweeners[_currentAudioKey].Restart(0.5f, 0f, Easing.Cubic, Direction.Out);
            }

            // 2. Setup the New Song Identity
            _audioCounter++;
            _currentAudioKey = $"au_{_audioCounter}";
            _beatmap = targetMap;

            // 3. Swap UI & Visuals Immediately (Feels incredibly snappy)
            Image newBg = LoadImage(_beatmap.BeatmapSetId.ToString(), _beatmap.GetBackgroundFullPath());
            if (bg != null) bg.texture = newBg;
            _targetCoverColor = GetAverageColor(newBg, 15);

            if (_rythmIndexer != null) _rythmIndexer.Beatmap = _beatmap;

            // 4. Load & Play New Audio
            LoadMusic(_currentAudioKey, Path.Combine(Path.GetDirectoryName(_beatmap.FilePath) ?? "", _beatmap.AudioFilename));
            SetMusicVolume(_currentAudioKey, 0f); // Force start at 0 volume
            SetMusicSpeed(_currentAudioKey, _speedMultiplier, _adjustPitch);
            PlayMusic(_currentAudioKey);
            SeekMusic(_currentAudioKey, _beatmap.PreviewTime / 1000f);

            // 5. Create and start the Fade-In Tweener
            var fadeInTweener = AddTween(new Tweener());
            fadeInTweener.SetValue(0f); // Snap tweener state to 0
            fadeInTweener.Restart(0.5f, _targetVolume, Easing.Cubic, Direction.Out);

            // Track it in our dictionary
            _audioTweeners[_currentAudioKey] = fadeInTweener;
        }

        private Button CreateSongRow(OsuBeatmap bm, int index, float yOffset, ImageFrame bg)
        {
            float currentHoverScale = 1f;
            float starRating = GetRealStarRating(bm);

            var rowButton = new Button
            {
                position = new UDim2(1f, 0f, -10f, yOffset),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,

                // Trigger the song change!
                onClick = (b) => 
                {
                    _starRating = starRating;
                    ChangeSong(bm, bg);
                    PlaySFX("select");
                },
                onHoverEnter = (b) =>
                {
                    PlaySFX("hover");
                }
            };

            rowButton.onUpdate = (btn) =>
            {
                float targetScale = btn.IsHovered ? 1.08f : 1f;
                // Smoothly interpolate the scale factor manually over delta time
                currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                // Dynamically scale the card width and give it a slight pop outward to the left
                rowButton.size = new UDim2(0f, 0f, 440f * currentHoverScale, 80f);

                byte r = (byte)(_currentCoverColor.R * 0.85f);
                byte g = (byte)(_currentCoverColor.G * 0.85f);
                byte b = (byte)(_currentCoverColor.B * 0.85f);

                // 3. Apply the colors dynamically
                btn.color = new Color(r, g, b, 175);
                btn.hoverColor = new Color(r, g, b, 235);
                btn.pressedColor = new Color(r, g, b, 255);
            };

            var thumbFrame = new ImageFrame
            {
                texture = LoadImage("logo"),
                position = new UDim2(0f, 0.5f, 10f, 0f),
                size = new UDim2(0f, 0f, 60f, 60f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                fit = ObjectFit.Cover
            };
            rowButton.children.Add(thumbFrame);

            // Queue background texture loading lazily to prevent synchronous boot freeze
            string id = bm.BeatmapSetId.ToString();
            string bgPath = bm.GetBackgroundFullPath();
            _loadQueue.Enqueue(() =>
            {
                try
                {
                    Image loadedTexture = LoadImage(id, bgPath);
                    thumbFrame.texture = loadedTexture;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MainGame] Error lazy loading background for {id}: {ex.Message}");
                }
            });
            
            rowButton.children.Add(new TextFrame { text = bm.Title, fontName = "gsans_bold", position = new UDim2(0f, 0f, 85f, 18f), anchorX = AnchorX.Left, anchorY = AnchorY.Top, textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top, scale = 1.3f, color = Color.White });
            
            rowButton.children.Add(new TextFrame { text = $"{bm.Artist} // {bm.Version}", fontName = "gsans", position = new UDim2(0f, 0f, 85f, 45f), anchorX = AnchorX.Left, anchorY = AnchorY.Top, textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top, scale = 1.0f, color = Color.White });
            
            rowButton.children.Add(new Frame { position = new UDim2(1f, 0.5f, -10f, 0f), size = new UDim2(0f, 0f, 4f, 40f), anchorX = AnchorX.Right, anchorY = AnchorY.Center, color = GetDifficultyColor(bm) });

            return rowButton;
        }

        private void LoadStarRatingCache()
        {
            try
            {
                if (File.Exists(CacheFileName))
                {
                    string json = File.ReadAllText(CacheFileName);
                    var deserialized = JsonSerializer.Deserialize<Dictionary<string, float>>(json);
                    if (deserialized != null)
                    {
                        foreach (var kvp in deserialized)
                        {
                            _starRatingCache[kvp.Key] = kvp.Value;
                        }
                        Console.WriteLine($"[MainGame] Star rating cache loaded successfully. Found {_starRatingCache.Count} entries.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error loading star rating cache: {ex.Message}");
            }
        }

        private void SaveStarRatingCache()
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_starRatingCache, options);
                File.WriteAllText(CacheFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error saving star rating cache: {ex.Message}");
            }
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFileName))
                {
                    string json = File.ReadAllText(SettingsFileName);
                    var deserialized = JsonSerializer.Deserialize<GameSettings>(json);
                    if (deserialized != null)
                    {
                        _settings = deserialized;
                        _targetVolume = _settings.MainVolume;
                        _effectsVolume = _settings.EffectsVolume;
                        _audioOffset = _settings.AudioOffset;
                        Console.WriteLine($"[MainGame] Settings loaded successfully. Main={_targetVolume}, SFX={_effectsVolume}, Offset={_audioOffset}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error loading settings: {ex.Message}");
            }
        }

        private void SaveSettings()
        {
            try
            {
                _settings.MainVolume = _targetVolume;
                _settings.EffectsVolume = _effectsVolume;
                _settings.AudioOffset = _audioOffset;

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error saving settings: {ex.Message}");
            }
        }

        private float GetRealStarRating(OsuBeatmap bm)
        {
            // Safety check
            if (string.IsNullOrEmpty(bm.FilePath) || !File.Exists(bm.FilePath))
                return 0f;

            // Check cache first
            if (_starRatingCache.TryGetValue(bm.FilePath, out float cachedRating))
                return cachedRating;

            try
            {
                // 1. Open a StreamReader directly to your local .osu file
                using (var reader = new StreamReader(bm.FilePath))
                {
                    // 2. Let OppaiSharp parse the file for physics calculations
                    var oppaiBeatmap = OppaiSharp.Beatmap.Read(reader);

                    // 3. Calculate the difficulty (using NoMod for the base Star Rating)
                    var diff = new DiffCalc().Calc(oppaiBeatmap, Mods.NoMod);

                    float rating = (float)diff.Total;

                    // Cache and save
                    _starRatingCache[bm.FilePath] = rating;
                    SaveStarRatingCache();

                    return rating;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error calculating star rating for {bm.Title}: {ex.Message}");
                return 0f;
            }
        }

        private Color GetDifficultyColor(OsuBeatmap bm)
        {
            if (!float.TryParse(bm.GetDifficulty("ApproachRate"), System.Globalization.CultureInfo.InvariantCulture, out float ar)) ar = 5f;
            if (ar < 4.0f) return new Color(118, 186, 255);
            if (ar < 6.0f) return new Color(136, 224, 118);
            if (ar < 8.0f) return new Color(255, 230, 118);
            if (ar < 9.5f) return new Color(255, 118, 118);
            return new Color(200, 118, 255);
        }

        // --- Custom Menu Visual Suite Structs ---
        private class LogoShockwave
        {
            public ImageFrame VisualNode { get; set; } = null!;
            public float Progress { get; set; } = 0f;
        }

        private class MenuParticle
        {
            public CircleFrame VisualNode { get; set; } = null!;
            public float DriftSpeedX { get; set; }
            public float DriftSpeedY { get; set; }
            public float BaseSize { get; set; }
            public float PulsePhase { get; set; }
        }

        private class GameSettings
        {
            public float MainVolume { get; set; } = 0.5f;
            public float EffectsVolume { get; set; } = 0.5f;
            public float AudioOffset { get; set; } = -55.35f;
        }
    }
}