using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.Effects;
using ArtFrame.RythmModule;
using ArtFrame.UIModifier;
using ArtFrame.UserInterface;
using ArtFrame.FileProcessing;
using OsuLib;

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
    public partial class MainGame : IArt
    {
        public void Setup()
        {
            // Ensure the playlists directory exists in the output folder
            if (!Directory.Exists(SongsPath))
            {
                Directory.CreateDirectory(SongsPath);
            }

            // Load persistent star ratings cache
            StarRatingCache.Load();

            // Load persistent game settings
            LoadSettings();

            ConfigureWindow(width: 1920, height: 1080, fullscreen: false);
            SetInputFramerate(300);
            SetFrameRate(120);

            LoadSFX("normal", "sounds/hitsounds/taiko-soft-hitnormal.wav");
            LoadSFX("whistle", "sounds/hitsounds/taiko-soft-hitwhistle.wav");
            LoadSFX("finish", "sounds/hitsounds/taiko-soft-hitfinish.wav");
            LoadSFX("clap", "sounds/hitsounds/taiko-soft-hitclap.wav");

            LoadSFX("beat", "sounds/sfxs/heartbeat.mp3");
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

            // Initialize Gameplay
            _taikofield = new TaikoPlayfield(LoadImage("circle", "content/hitcircle.png"), "gsans_bold")
            {
                size = new UDim2(1f, 0, 0, 200f),
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                GlobalScale = 1.4f,
                ExitKey = _keyExitGameplay,
                HitKeys = new Keys[] { _keyHitLeft, _keyHitRight },
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
                // Save player's score to ScoreManager if they actually played
                if (_taikofield.Score > 0 || _taikofield.HitsPerfect > 0 || _taikofield.HitsGood > 0 || _taikofield.HitsOk > 0 || _taikofield.HitsMiss > 0)
                {
                    string activeMods = "";
                    if (_modHidden) activeMods += "HD ";
                    if (Math.Abs(_speedMultiplier - 1f) > 0.01f)
                    {
                        activeMods += _speedMultiplier > 1f ? "DT " : "HT ";
                    }
                    if (_adjustPitch) activeMods += "NC ";
                    activeMods = string.IsNullOrWhiteSpace(activeMods) ? "NM" : activeMods.TrimEnd();

                    ScoreManager.AddScore(
                        _beatmap?.FilePath ?? "", 
                        "You", 
                        _taikofield.Score, 
                        _taikofield.GetAccuracy(), 
                        _taikofield.MaxComboReached, 
                        activeMods
                    );
                    
                    RefreshScoreboard();
                }

                // 1. Wipe the playfield clean and hide it
                _taikofield.ResetState();

                // 2. Audio Transition: Jump back to menu preview and restore volume!
                SeekMusic(_currentAudioKey, _beatmap?.PreviewTime / 1000f ?? 0f);
                if (_audioTweeners.ContainsKey(_currentAudioKey))
                    _audioTweeners[_currentAudioKey].Restart(1.6f, _targetVolume, Easing.Exponential, Direction.Out);

                // 3. Tell the cinematic timeline to run backward
                _isStarting = false;
                _startPhase = 0;

                SetInputFramerate(300);
                SetFrameRate(120);

                //_rythmIndexer = new RhythmIndexer(new InterpolatingAudioClock(), new RhythmTracker(), () => GetMusicTimePlayed(_currentAudioKey)) { Beatmap = _beatmap, MusicOffset = -55.35f };
                _startShrinkTweener.Restart(1f, 0f, Easing.Exponential, Direction.Out);
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
            var scannedBeatmaps = _scanner.ScanLazy(SongsPath).ToList();
            if (scannedBeatmaps.Count > 0)
            {
                var beatmapRand = new Random();
                _beatmap = scannedBeatmaps[beatmapRand.Next(scannedBeatmaps.Count)];
            }

            // 1.5. Group Beatmaps by Set ID for grouped playlist
            var groups = scannedBeatmaps
                .GroupBy(bm => bm.BeatmapSetId > 0 ? bm.BeatmapSetId.ToString() : $"{bm.Title}_{bm.Artist}")
                .Select(g => new BeatmapGroup
                {
                    Key = g.Key,
                    Representative = g.First(),
                    Difficulties = g.OrderBy(bm => GetRealStarRating(bm)).ToList()
                })
                .ToList();
            _beatmapGroups.Clear();
            _beatmapGroups.AddRange(groups);

            if (_beatmap != null)
            {
                var activeGroup = _beatmapGroups.FirstOrDefault(g => g.Difficulties.Contains(_beatmap));
                if (activeGroup != null)
                {
                    activeGroup.IsExpanded = true;
                }
            }


            LoadMusic(_currentAudioKey, Path.Combine(Path.GetDirectoryName(_beatmap?.FilePath) ?? "", _beatmap?.AudioFilename ?? ""));
            
            Image initialBg = LoadImage(_beatmap?.BeatmapSetId.ToString() ?? "default_bg", _beatmap?.GetBackgroundFullPath() ?? "content/default_bg.png");
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
                        float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                        float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

                        // 1. Sizing: Shrunk cover in Listen/Score mode
                        float coverSize = ArtMathHelper.Lerp(500f, 360f, effectiveListenScore);
                        e.size = UDim2.Lerp(UDim2.FromScale(1f, 1f), UDim2.FromOffset(coverSize, coverSize), _bgTweener.CurrentValue);

                        // 2. Position: Shift cover to top-left in Listen/Score mode
                        float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue); // Cleaned nested lerp
                        float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                        float targetX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, effectiveListenScore);
                        float targetY = ArtMathHelper.Lerp(0.5f, 0.32f, effectiveListenScore);

                        e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(targetX, targetY), _bgTweener.CurrentValue);

                        _blur.BlurAmount = ArtMathHelper.Lerp(0f, 2.5f, 1f - _bgTweener.CurrentValue);
                        e.alpha = 1f - _startShrinkTweener.CurrentValue;
                        e.BypassEffect = _bgTweener.CurrentValue >= 0.99f;
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
                    if ((Keyboard.IsKeyPressed(_keyToggleCover) || (Mouse.LeftClicked() && !_isCoverView)) && !_isStarting && !_inIntro && !_isListeningForKey)
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

            _logoUI = new ImageButton
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
                        e.alpha = _introAlpha;
                        e.size = new UDim2(0.4f, 0.4f);
                        e.position = UDim2.FromScale(0.5f, 0.5f);
                        e.rotation = _logoRotation.CurrentValue;
                    }
                    else
                    {
                        float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                        float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

                        // Calculate dynamic size matching the cover shrink factor (shrinks to 72% in Listen/Score view)
                        float logoScaleFactor = ArtMathHelper.Lerp(0.25f, 0.25f * 0.72f, effectiveListenScore);
                        e.size = (new UDim2(0.4f, 0.4f) * MathF.Max(_logoTweener.CurrentValue, _startTransitionTweener.CurrentValue)) * MathF.Max((1f - _bgTweener.CurrentValue), logoScaleFactor);
                        e.rotation = (_logoRotation.CurrentValue * (1f - _bgTweener.CurrentValue));

                        // FIXED: Cleaned up the nested layout calculation to perfectly align with the title logic
                        float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
                        float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                        // Dynamic targets for Listen/Score mode
                        float targetX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, effectiveListenScore);
                        float targetY = ArtMathHelper.Lerp(0.5f, 0.32f, effectiveListenScore);

                        e.alpha = 1f - _startShrinkTweener.CurrentValue;

                        // The position retains its exact original behavior but tracks on the corrected curve
                        e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(targetX, targetY), _bgTweener.CurrentValue);
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
                    e.text = $"{_beatmap?.GetBpmAt(0):F0} BPM";
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
                    string ar = _beatmap?.GetDifficulty("ApproachRate", "5.0") ?? "5.0";
                    string cs = _beatmap?.GetDifficulty("CircleSize", "4.0") ?? "4.0";
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
                    e.text = _beatmap?.Title ?? "";
                    e.color = new Color((byte)(_currentCoverColor.R * MathF.Max(0.3f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.G * MathF.Max(0.3f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.B * MathF.Max(0.3f, _startTransitionTweener.CurrentValue)));

                    // 1. REPLICATE THE EXACT COVER POSITION LOGIC
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

                    float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
                    float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                    float coverTargetX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, effectiveListenScore);
                    float coverTargetY = ArtMathHelper.Lerp(0.5f, 0.32f, effectiveListenScore); // Reverted back to match true cover logic targets

                    // This matches the Cover's exact spatial center point at any given frame
                    UDim2 currentCoverCenter = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(coverTargetX, coverTargetY), _bgTweener.CurrentValue);

                    // 2. POSITION THE TITLE RELATIVE TO THAT CENTER
                    // FIXED: When effectiveListenScore is 0 (Cover View), this math now evaluates 
                    // EXACTLY to: ScaleX = currentTargetX, ScaleY = 0.5f, OffsetX = -250f, OffsetY = 320f.
                    // It will stay perfectly locked to your original static positioning.
                    float targetScaleX = ArtMathHelper.Lerp(currentTargetX, coverTargetX, _bgTweener.CurrentValue);
                    float targetScaleY = ArtMathHelper.Lerp(0.5f, coverTargetY, _bgTweener.CurrentValue);

                    float xOffset = ArtMathHelper.Lerp(-250f, 210f, effectiveListenScore);
                    float yOffset = ArtMathHelper.Lerp(320f, -60f, effectiveListenScore);

                    // We dynamically blend the scale baseline based on effectiveListenScore so it remains unmoving in Cover View
                    e.position = new UDim2(
                        ArtMathHelper.Lerp(currentTargetX, targetScaleX, effectiveListenScore),
                        ArtMathHelper.Lerp(0.47f, targetScaleY, effectiveListenScore),
                        xOffset,
                        yOffset
                    );

                    // 3. ALPHA SINK
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
                    e.text = _beatmap?.Artist ?? "";
                    e.color = new Color((byte)(_currentCoverColor.R * MathF.Max(0.6f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.G * MathF.Max(0.6f, _startTransitionTweener.CurrentValue)), (byte)(_currentCoverColor.B * MathF.Max(0.6f, _startTransitionTweener.CurrentValue)));

                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
                    float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                    float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);
                    float targetScaleX = currentTargetX - 0.08f;
                    float targetScaleY = 0.32f;

                    UDim2 originalPos = UDim2.Lerp(new UDim2(0.38f, 0.5f, -250f, 350f), new UDim2(currentTargetX, 0.5f, -250f, 325f), _bgTweener.CurrentValue);
                    UDim2 listenPos = new UDim2(targetScaleX, targetScaleY, 210f, -20f);

                    e.position = UDim2.Lerp(originalPos, listenPos, effectiveListenScore);

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

                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float baseTargetX = ArtMathHelper.Lerp(ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue), 0.5f, _startTransitionTweener.CurrentValue);
                    float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                    float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);
                    float targetScaleX = currentTargetX - 0.08f;
                    float targetScaleY = 0.32f;

                    UDim2 originalPos = UDim2.Lerp(new UDim2(0.38f, 0.5f, 0f, 410f), new UDim2(currentTargetX, 0.5f, 0f, 390f), _bgTweener.CurrentValue);
                    UDim2 listenPos = new UDim2(targetScaleX, targetScaleY, 415f, 40f);

                    e.position = UDim2.Lerp(originalPos, listenPos, effectiveListenScore);
                    
                    float trackWidth = ArtMathHelper.Lerp(500f * _bgTweener.CurrentValue, 410f * _bgTweener.CurrentValue, effectiveListenScore);
                    e.size = new UDim2(0f, 0f, trackWidth, 6f);
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

                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float baseTargetX = ArtMathHelper.Lerp(ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue), 0.5f, _startTransitionTweener.CurrentValue);
                    float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                    float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);
                    float targetScaleX = currentTargetX - 0.08f;
                    float targetScaleY = 0.32f;

                    UDim2 originalPos = UDim2.Lerp(new UDim2(0.38f, 0.5f, -250f, 425f), new UDim2(currentTargetX, 0.5f, -250f, 405f), _bgTweener.CurrentValue);
                    UDim2 listenPos = new UDim2(targetScaleX, targetScaleY, 210f, 60f);

                    e.position = UDim2.Lerp(originalPos, listenPos, effectiveListenScore);

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

                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float baseTargetX = ArtMathHelper.Lerp(ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue), 0.5f, _startTransitionTweener.CurrentValue);
                    float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);

                    float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);
                    float targetScaleX = currentTargetX - 0.08f;
                    float targetScaleY = 0.32f;

                    UDim2 originalPos = UDim2.Lerp(new UDim2(0.38f, 0.5f, 250f, 425f), new UDim2(currentTargetX, 0.5f, 250f, 405f), _bgTweener.CurrentValue);
                    UDim2 listenPos = new UDim2(targetScaleX, targetScaleY, 620f, 60f);

                    e.position = UDim2.Lerp(originalPos, listenPos, effectiveListenScore);

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
                        _modifiersTweener.Restart(0.9f, 0f, Easing.Fluid, Direction.Out);
                    }

                    _settingsTweener.Restart(duration: 0.9f, targetValue: _isSettingsOpen ? 1.0f : 0f, Easing.Fluid, Direction.Out);
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
                        _settingsTweener.Restart(0.9f, 0f, Easing.Fluid, Direction.Out);
                    }

                    _modifiersTweener.Restart(duration: 0.9f, targetValue: _isModifiersOpen ? 1.0f : 0f, Easing.Fluid, Direction.Out);
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
                scrollSensitivity = 68f,
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
                clipMode = ClipMode.Clip,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    // Smoothly interpolate positions from tucked away (-510px) to resting at the left edge (0px)
                    e.position = UDim2.Lerp(new UDim2(0f, 0, -480f, 60f), new UDim2(0f, 0f, 0f, 60f), MathF.Min(_settingsTweener.CurrentValue, _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue)));

                    // Pull dynamic color mutations matching your global album art tint machine
                    //e.color = new Color((byte)(_currentCoverColor.R * 0.85f), (byte)(_currentCoverColor.G * 0.85f), (byte)(_currentCoverColor.B * 0.85f), 100);
                }
            };

            // --- Dummy Prototype Settings Rows ---
            string[] options = { "Volumes", "Audio Offset", "Key Bindings", "Graphics Config" };
            foreach (var optionName in options)
            {
                Frame optionRow = new Frame
                {
                    position = new UDim2(0f, 0f, 10f, _settingsYOffset),
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
                    scale = 1.5f,
                    color = Color.White
                });

                settingsPanel.children.Add(optionRow);
                _settingsYOffset += 50f; // Stack layout down cleanly
                AddSettingsMenu(settingsPanel, optionName);
            };

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

            // === Scoreboard Panel ===
            _scoreboardPanel = new Frame
            {
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                size = new UDim2(0f, 0f, 450f, 360f),
                onUpdate = (e, dt) =>
                {
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float coverState = _bgTweener.CurrentValue * (1f - activePanelValue) * (1f - _startTransitionTweener.CurrentValue);
                    float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

                    // X coordinate is always centered relative to current cover/info center
                    float baseTargetX = ArtMathHelper.Lerp(ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue), 0.5f, _startTransitionTweener.CurrentValue);
                    float x = baseTargetX;
                    
                    // Y axis only: slides up from offscreen bottom (1.3f) to its position (0.70f)
                    float y = ArtMathHelper.Lerp(1.3f, 0.70f, effectiveListenScore * coverState);

                    e.position = new UDim2(x, y, 0f, 20f);

                    byte r = (byte)(_currentCoverColor.R * 0.12f);
                    byte g = (byte)(_currentCoverColor.G * 0.12f);
                    byte b = (byte)(_currentCoverColor.B * 0.12f);

                    // Only shown when effectiveListenScore is active
                    float totalState = effectiveListenScore * coverState;
                    e.color = new Color(r, g, b, (byte)(160 * totalState));
                    e.alpha = totalState;
                }
            };
            Add(_scoreboardPanel);
            RefreshScoreboard();

            Add(settingsPanel);
            Add(modifiersPanel);
            Add(_taikofield);

            // Populate Playlist
            RepopulatePlaylist();

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

                    if (_logoUI.IsHovered)
                    {
                        PlaySFX(_rythmIndexer.IsDownbeat ? "dwbeat" : "beat");
                    }
                    
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

        private void RefreshScoreboard()
        {
            if (_scoreboardPanel == null) return;
            
            _scoreboardPanel.children.Clear();

            if (_beatmap == null) return;

            // Spotify style header
            Frame header = new Frame
            {
                position = new UDim2(0f, 0f, 0f, 15f),
                size = new UDim2(1f, 0f, 0f, 35f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                color = new Color(0, 0, 0, 0)
            };

            header.children.Add(new TextFrame
            {
                text = "Listen Scores",
                fontName = "gsans_bold",
                position = new UDim2(0.05f, 0.5f, 0f, 0f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                color = Color.White
            });

            _scoreboardPanel.children.Add(header);

            // Fetch leaderboard from ScoreManager
            string mapKey = _beatmap.FilePath;
            var scores = ScoreManager.GetLeaderboard(mapKey, _beatmap.Title, _beatmap.Version, 5);

            float yOffset = 50f;
            for (int i = 0; i < scores.Count; i++)
            {
                var scoreEntry = scores[i];
                int rank = i + 1;

                float currentHoverScale = 1f;
                int rankIndex = rank; // local copy for lambda
                
                var rowBtn = new Button
                {
                    position = new UDim2(0f, 0f, 10f, yOffset),
                    size = new UDim2(0.95f, 0f, 0f, 55f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onClick = (b) => {
                        PlaySFX("select");
                    },
                    onHoverEnter = (b) => {
                        PlaySFX("hover");
                    }
                };

                rowBtn.onUpdate = (btn) =>
                {
                    float hoveredScale = btn.IsHovered ? 1.03f : 1f;
                    float targetScale = btn.IsPressed ? hoveredScale + 0.02f : hoveredScale;
                    currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.1f);
                    
                    rowBtn.size = new UDim2(0.95f, 0f, 0f, 55f * currentHoverScale);
                    
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);

                    btn.color = new Color(r, g, b, 140);
                    btn.hoverColor = new Color((byte)Math.Clamp(r + 20, 0, 255), (byte)Math.Clamp(g + 20, 0, 255), (byte)Math.Clamp(b + 20, 0, 255), 190);
                    btn.pressedColor = new Color((byte)Math.Clamp(r + 40, 0, 255), (byte)Math.Clamp(g + 40, 0, 255), (byte)Math.Clamp(b + 40, 0, 255), 220);
                };

                // Rank badge (Left)
                Color rankColor = Color.White;
                if (rankIndex == 1) rankColor = new Color(255, 215, 0);       // Gold
                else if (rankIndex == 2) rankColor = new Color(192, 192, 192); // Silver
                else if (rankIndex == 3) rankColor = new Color(205, 127, 50);  // Bronze

                var rankBadge = new TextFrame
                {
                    text = $"#{rankIndex}",
                    fontName = "gsans_bold",
                    position = new UDim2(0.04f, 0.5f, 0f, 0f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Left,
                    textAnchorY = AnchorY.Center,
                    scale = 1.05f,
                    color = rankColor
                };
                rowBtn.children.Add(rankBadge);

                // Player name + Mods capsule
                string displayName = scoreEntry.PlayerName;
                var nameText = new TextFrame
                {
                    text = displayName,
                    fontName = "gsans_bold",
                    position = new UDim2(0.16f, 0.5f, 0f, -10f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Left,
                    textAnchorY = AnchorY.Center,
                    scale = 0.95f,
                    color = Color.White
                };
                rowBtn.children.Add(nameText);

                // Mods badge capsule if any mods are active (e.g. DT, HD)
                if (!string.IsNullOrEmpty(scoreEntry.Mods) && scoreEntry.Mods != "NM")
                {
                    var modCapsule = new TextFrame
                    {
                        text = scoreEntry.Mods,
                        fontName = "gsans_bold",
                        position = new UDim2(0.52f, 0.5f, 0f, -10f),
                        anchorX = AnchorX.Left,
                        anchorY = AnchorY.Center,
                        textAnchorX = AnchorX.Left,
                        textAnchorY = AnchorY.Center,
                        scale = 0.75f,
                        color = new Color(30, 215, 96),
                        backgroundColor = new Color(0, 0, 0),
                        backgroundAlpha = 0.6f,
                        backgroundPadding = 4f
                    };
                    rowBtn.children.Add(modCapsule);
                }

                // Stats (Bottom Left: Accuracy and Max Combo)
                string stats = $"{scoreEntry.Accuracy:F2}%  //  {scoreEntry.MaxCombo}x";
                var statsText = new TextFrame
                {
                    text = stats,
                    fontName = "gsans",
                    position = new UDim2(0.16f, 0.5f, 0f, 10f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Left,
                    textAnchorY = AnchorY.Center,
                    scale = 0.75f,
                    color = new Color(175, 175, 175)
                };
                rowBtn.children.Add(statsText);

                // Score (Right)
                var scoreText = new TextFrame
                {
                    text = $"{scoreEntry.Score:N0}",
                    fontName = "gsans_bold",
                    position = new UDim2(0.92f, 0.5f, -12f, 0f),
                    anchorX = AnchorX.Right,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Right,
                    textAnchorY = AnchorY.Center,
                    scale = 1.0f,
                    color = Color.White
                };
                rowBtn.children.Add(scoreText);

                // Right-Edge Color Stripe (Vertical color bar matching ranking colors)
                var edgeStripe = new Frame
                {
                    position = new UDim2(1f, 0.5f, -4f, 0f),
                    size = new UDim2(0f, 0f, 4f, 30f),
                    anchorX = AnchorX.Right,
                    anchorY = AnchorY.Center,
                    color = rankColor
                };
                rowBtn.children.Add(edgeStripe);

                _scoreboardPanel.children.Add(rowBtn);
                yOffset += 65f;
            }
        }
    }
}
