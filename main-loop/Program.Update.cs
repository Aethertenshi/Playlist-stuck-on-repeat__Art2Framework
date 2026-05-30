using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UserInterface;
using ArtFrame.FileProcessing;
using OsuLib;
using System.Text.Json;

using static ArtFrame.AudioHelper;
using static ArtFrame.GraphicsHelper;
using static ArtFrame.InputHelper;
using static ArtFrame.SpriteHelper;
using static ArtFrame.TextureHelper;
using static ArtFrame.TweenHelper;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
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
                    _introAlpha = Math.Clamp(progress * 1.05f, 0f, 1f); // Smooth logo fade-in

                    if (_transitionFired)
                    {
                        _introTransitionTimer += dt * 1.25f;
                    }

                    // Trigger GridTransitionRadial at 95% completion of welcome.wav
                    if (progress >= 0.87f && !_transitionFired)
                    {
                        _transitionFired = true;
                        _welcomeTransition.Play(2.7f, Easing.Exponential, Direction.Out);
                        _logoRotation.Start(2.7f, 0f, -3.8f, Easing.Fluid, Direction.Out); // Smooth elegant rotation alignment

                        // Spawn a single, extremely soft pure white ripple ring (calm ripple effect)
                        var waveNode = new ImageFrame
                        {
                            texture = LoadImage("logo"),
                            color = Color.White,
                            anchorX = AnchorX.Center,
                            anchorY = AnchorY.Center,
                            fit = ObjectFit.Cover,
                            alpha = 0.4f
                        };
                        _shockwaveHolder.children.Add(waveNode);
                        _shockwaves.Add(new LogoShockwave { VisualNode = waveNode, Progress = 0f });

                        // Play randomly selected beatmap music preview
                        PlayMusic(_currentAudioKey);
                        SeekMusic(_currentAudioKey, _beatmap?.PreviewTime / 1000f ?? 0f);
                        if (_audioTweeners.ContainsKey(_currentAudioKey))
                            _audioTweeners[_currentAudioKey].Restart(3.5f, _targetVolume, Easing.Exponential, Direction.Out);
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
                
                // Inherit the cover color dynamically with a premium brightness boost
                float brightnessScale = 1.4f;
                byte r = (byte)Math.Clamp(_currentCoverColor.R * brightnessScale, 55, 255);
                byte g = (byte)Math.Clamp(_currentCoverColor.G * brightnessScale, 55, 255);
                byte b = (byte)Math.Clamp(_currentCoverColor.B * brightnessScale, 55, 255);
                part.VisualNode.color = new Color(r, g, b, part.Alpha);

                float introFactor = _inIntro ? _introAlpha : 1f;
                float viewFactor = _isCoverView ? 0f : (1f - _startTransitionTweener.CurrentValue);
                part.VisualNode.alpha = introFactor * viewFactor * 0.95f; // Boosted from 0.7f for beautiful background visibility
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
                    if (wave.Progress < 0f)
                    {
                        wave.VisualNode.alpha = 0f;
                    }
                    else
                    {
                        // Scale it much larger so it extends far beyond the logo's boundaries!
                        float scaleMultiplier = 1f + wave.Progress * 0.35f; // expands to 1.35x scale!
                        float baseSizeScale = 0.4f * MathF.Max(_logoTweener.CurrentValue, _startTransitionTweener.CurrentValue);
                        wave.VisualNode.size = new UDim2(baseSizeScale * scaleMultiplier, baseSizeScale * scaleMultiplier);
                        
                        // Center inside blurBg (which aligns perfectly with logo center)
                        wave.VisualNode.position = UDim2.FromScale(0.5f, 0.5f);
                        wave.VisualNode.rotation = _logoRotation.CurrentValue;
                        wave.VisualNode.alpha = ((1f - wave.Progress) * 0.65f) * (1f - _peekBg);
                    }
                }
            }

            // --- Custom Audio Speed ---
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

            // --- Background Peek ---
            if (!_isCoverView && Mouse.RightDown())
            {
                _peekBg = ArtMathHelper.Lerp(_peekBg, 1f, dt * 12f);
            }
            if (!_isCoverView && Mouse.RightReleased())
            {
                _peekBg = ArtMathHelper.Lerp(_peekBg, 0f, dt * 10f);
            }

            // --- Toggle Listen Score View ---
            if (!_isStarting && Keyboard.IsKeyPressed(_keyToggleListenScore) && _isCoverView && !_isListeningForKey && !Mouse.RightDown())
            {
                _isListenScoreMode = !_isListenScoreMode;
                _listenScoreTweener.Restart(duration: 0.7f, targetValue: _isListenScoreMode ? 1.0f : 0f, Easing.Fluid, Direction.Out);
                PlaySFX("select");
            }

            // --- Game Start Sequence (Press TAB) ---
            if (!_isStarting && Keyboard.IsKeyPressed(_keyStartGame) && _isCoverView && !_isListeningForKey && !Mouse.RightDown())
            {
                SetInputFramerate(_settings.GameplayPollingRate);
                SetFrameRate(_settings.GameplayFps);

                PlaySFX("play-click"); // Optional feedback
                _isStarting = true;
                _startTimer = 0f;

                // 1. Force close any open side panels and Listen/Score mode
                _settingsTweener.Restart(0.5f, 0f, Easing.Exponential, Direction.Out);
                _modifiersTweener.Restart(0.5f, 0f, Easing.Exponential, Direction.Out);
                
                bool hadListenScore = _isListenScoreMode;
                _isListenScoreMode = false;
                _listenScoreTweener.Restart(0.5f, 0f, Easing.Exponential, Direction.Out);

                if (hadListenScore)
                {
                    _startPhase = 0; // Wait 0.5 seconds for centering first
                }
                else
                {
                    _startPhase = 1;
                    // Trigger Phase 1 immediately
                    _startTransitionTweener.Restart(1.1f, 1.0f, Easing.Exponential, Direction.Out);

                    // Fade out the music smoothly
                    if (_audioTweeners.ContainsKey(_currentAudioKey))
                        _audioTweeners[_currentAudioKey].Restart(1.1f, 0f, Easing.Exponential, Direction.Out);
                }
            }

            if (_isStarting)
            {
                _startTimer += dt;

                // Wait 0.5 seconds in Phase 0 (for centering) then trigger Phase 1
                if (_startPhase == 0 && _startTimer >= 0.5f)
                {
                    _startPhase = 1;
                    _startTimer = 0f; // Reset timer so Phase 1 starts at 0

                    // Trigger Phase 1 (UI Fades out, Cover slides to center, bgDrop darkens)
                    _startTransitionTweener.Restart(1.6f, 1.0f, Easing.Fluid, Direction.InOut);

                    // Fade out the music smoothly
                    if (_audioTweeners.ContainsKey(_currentAudioKey))
                        _audioTweeners[_currentAudioKey].Restart(1.5f, 0f, Easing.Fluid, Direction.Out);
                }
                // Wait 1.5 seconds, then trigger Phase 2 (The Shrink)
                else if (_startPhase == 1 && _startTimer >= 1.3f)
                {
                    _startPhase = 2;
                    _startShrinkTweener.Restart(2.1f, 1.0f, Easing.Fluid, Direction.InOut);
                }
                // Wait another 1.5 seconds, then load the game
                else if (_startPhase == 2 && _startTimer >= 3.5f)
                {
                    _startPhase = 3;

                    // TODO: ENTER GAMEPLAY SCENE
                    //Console.WriteLine("/// TRANSITION FINISHED: LOAD GAMEPLAY STATE ///");

                    if (_audioTweeners.ContainsKey(_currentAudioKey))
                    {
                        StopMusic(_currentAudioKey);
                        _audioTweeners[_currentAudioKey].Restart(0.5f, _targetVolume, Easing.Fluid, Direction.Out);
                        SeekMusic(_currentAudioKey, 0f);
                        PlayMusic(_currentAudioKey);
                    }

                    // 1. Recycle the existing rhythm indexer and tell it to wait for 0.0s!
                    _rythmIndexer?.Beatmap = _beatmap;
                    _rythmIndexer?.MusicOffset = -55.35f;
                    _rythmIndexer?.Reset(0f); // Uses your InterpolatingAudioClock's built in Reset

                    // 2. Wipe any old state and load the new notes
                    _taikofield.alpha = 0f;
                    _taikofield.ResetState();
                    _taikofield.LoadBeatmap(_beatmap);
                }
            }

            // --- Keys Binding ---
            if (_isListeningForKey)
            {
                foreach (Keys key in Enum.GetValues<Keys>())
                {
                    if (key != Keys.None && Keyboard.IsKeyPressed(key))
                    {
                        if (_listeningActionName == "ToggleCover") _keyToggleCover = key;
                        else if (_listeningActionName == "StartGame") _keyStartGame = key;
                        else if (_listeningActionName == "ExitGameplay") { _keyExitGameplay = key; if (_taikofield != null) _taikofield.ExitKey = key; }
                        else if (_listeningActionName == "HitLeft") { _keyHitLeft = key; if (_taikofield != null) _taikofield.HitKeys = new Keys[] { _keyHitLeft, _keyHitRight }; }
                        else if (_listeningActionName == "HitRight") { _keyHitRight = key; if (_taikofield != null) _taikofield.HitKeys = new Keys[] { _keyHitLeft, _keyHitRight }; }
                        else if (_listeningActionName == "ListenScore") _keyToggleListenScore = key;

                        _isListeningForKey = false;
                        _listeningActionName = "";
                        SaveSettings();
                        PlaySFX("select");
                        break;
                    }
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

        private void ProcessDroppedFile(string path)
        {
            Console.WriteLine($"[MainGame] Dropped file detected: {path}");
            if (string.IsNullOrEmpty(path)) return;

            // The first successfully parsed beatmap (used to select/play after import)
            OsuBeatmap? firstBeatmap = null;

            try
            {
                // 1. If it's a .osz file — parse ALL .osu files inside it
                if (path.EndsWith(".osz", StringComparison.OrdinalIgnoreCase))
                {
                    string? extractedFolder = OszImporter.Import(path, SongsPath);
                    if (extractedFolder != null && Directory.Exists(extractedFolder))
                    {
                        var osuFiles = _scanner.FindOsuFiles(extractedFolder).ToArray();
                        if (osuFiles.Length == 0)
                        {
                            Console.WriteLine($"[MainGame] No .osu files found in extracted .osz folder: {extractedFolder}");
                            return;
                        }

                        foreach (var osuFile in osuFiles)
                        {
                            try
                            {
                                var beatmap = _parser.Parse(osuFile);
                                AddBeatmapToGroups(beatmap);
                                firstBeatmap ??= beatmap;
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[MainGame] Failed to parse {osuFile}: {ex.Message}");
                            }
                        }
                    }
                }
                // 2. If it's a .osu file directly
                else if (path.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                {
                    var beatmap = _parser.Parse(path);
                    AddBeatmapToGroups(beatmap);
                    firstBeatmap = beatmap;
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

            if (firstBeatmap != null)
            {
                // Load background image texture for the selected beatmap
                string bgPath = firstBeatmap.GetBackgroundFullPath();
                LoadImage(firstBeatmap.BeatmapSetId.ToString(), bgPath);

                // Refresh layout once after all difficulties are added
                RepopulatePlaylist();

                // Select, load, and play the first imported difficulty
                ChangeSong(firstBeatmap, _bgImageFrame);
                _starRating = GetRealStarRating(firstBeatmap);
                PlaySFX("select");
            }
        }

        private void AddBeatmapToGroups(OsuBeatmap beatmap)
        {
            Console.WriteLine($"[MainGame] Adding difficulty: {beatmap.Title} [{beatmap.Version}]");

            string setKey = beatmap.BeatmapSetId > 0
                ? beatmap.BeatmapSetId.ToString()
                : $"{beatmap.Title}_{beatmap.Artist}";

            var existingGroup = _beatmapGroups.FirstOrDefault(g => g.Key == setKey);
            if (existingGroup != null)
            {
                if (!existingGroup.Difficulties.Any(d => d.FilePath == beatmap.FilePath))
                {
                    existingGroup.Difficulties.Add(beatmap);
                    existingGroup.Difficulties = existingGroup.Difficulties
                        .OrderBy(bm => GetRealStarRating(bm))
                        .ToList();
                }
                foreach (var g in _beatmapGroups) g.IsExpanded = false;
                existingGroup.IsExpanded = true;
            }
            else
            {
                var newGroup = new BeatmapGroup
                {
                    Key = setKey,
                    Representative = beatmap,
                    Difficulties = new List<OsuBeatmap> { beatmap },
                    IsExpanded = true
                };
                foreach (var g in _beatmapGroups) g.IsExpanded = false;
                _beatmapGroups.Add(newGroup);
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
            _targetCoverColor = GetAverageColor(newBg, 28);

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

            RefreshScoreboard();
        }

        private void RepopulatePlaylist()
        {
            if (_playlistScroll == null) return;
            _playlistScroll.children.Clear();

            float currentYOffset = 10f;
            int index = 0;

            foreach (var group in _beatmapGroups)
            {
                // 1. Create and add Header card
                var header = CreateHeaderRow(group, index++, currentYOffset);
                _playlistScroll.children.Add(header);
                currentYOffset += 90f; // 80px + 10px spacing

                // 2. Always show nested difficulties (no expansion checks!)
                foreach (var diff in group.Difficulties)
                {
                    var diffRow = CreateDifficultyRow(diff, index++, currentYOffset);
                    _playlistScroll.children.Add(diffRow);
                    currentYOffset += 50f; // 40px + 10px spacing
                }
            }
        }

        private Button CreateHeaderRow(BeatmapGroup group, int index, float yOffset)
        {
            float currentHoverScale = 1f;
            var bm = group.Representative;

            var rowButton = new Button
            {
                position = new UDim2(1f, 0f, -10f, yOffset),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,
                onClick = (b) =>
                {
                    // Select the easiest difficulty from the set directly!
                    if (group.Difficulties.Count > 0)
                    {
                        var easiestDiff = group.Difficulties[0];
                        _starRating = GetRealStarRating(easiestDiff);
                        ChangeSong(easiestDiff, _bgImageFrame);
                    }

                    PlaySFX("select");
                },
                onHoverEnter = (b) =>
                {
                    PlaySFX("hover");
                }
            };

            rowButton.onUpdate = (btn) =>
            {
                float hoveredScale = btn.IsHovered ? 1.06f : 1f;
                float targetScale = btn.IsPressed ? hoveredScale + 0.045f : hoveredScale;
                currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                rowButton.size = new UDim2(0f, 0f, 440f * currentHoverScale, 80f);

                byte r = (byte)(_currentCoverColor.R * 0.85f);
                byte g = (byte)(_currentCoverColor.G * 0.85f);
                byte b = (byte)(_currentCoverColor.B * 0.85f);

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

            // Lazy load background
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
                    Console.WriteLine($"[MainGame] Error lazy loading background for header {id}: {ex.Message}");
                }
            });

            // Title & Artist
            rowButton.children.Add(new TextFrame { text = bm.Title, fontName = "gsans_bold", position = new UDim2(0f, 0f, 85f, 18f), anchorX = AnchorX.Left, anchorY = AnchorY.Top, textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top, scale = 1.3f, color = Color.White });
            
            // Subtext with Diffs Count
            rowButton.children.Add(new TextFrame { text = $"{bm.Artist}  //  [ {group.Difficulties.Count} difficulties ]", fontName = "gsans", position = new UDim2(0f, 0f, 85f, 45f), anchorX = AnchorX.Left, anchorY = AnchorY.Top, textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top, scale = 1.0f, color = new Color(220, 220, 255) });

            return rowButton;
        }

        private Button CreateDifficultyRow(OsuBeatmap bm, int index, float yOffset)
        {
            float currentHoverScale = 1f;
            float starRating = GetRealStarRating(bm);

            var rowButton = new Button
            {
                position = new UDim2(1f, 0f, -10f, yOffset), // indent it slightly to look nested
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,

                onClick = (b) =>
                {
                    _starRating = starRating;
                    ChangeSong(bm, _bgImageFrame);
                    PlaySFX("select");
                },
                onHoverEnter = (b) =>
                {
                    PlaySFX("hover");
                }
            };

            rowButton.onUpdate = (btn) =>
            {
                float hoveredScale = btn.IsHovered ? 1.04f : 1f;
                float pressedScale = btn.IsPressed ? hoveredScale + 0.03f : hoveredScale;
                float targetScale = _beatmap == bm ? hoveredScale + 0.05f : hoveredScale;
                currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                rowButton.size = new UDim2(0f, 0f, 420f * currentHoverScale, 40f);

                byte r = (byte)(_currentCoverColor.R * 0.7f);
                byte g = (byte)(_currentCoverColor.G * 0.7f);
                byte b = (byte)(_currentCoverColor.B * 0.7f);

                btn.color = new Color(r, g, b, 175);
                btn.hoverColor = new Color(r, g, b, 235);
                btn.pressedColor = new Color(r, g, b, 255);
            };

            // Spotify style "+" icon prefix
            var plusIcon = new TextFrame
            {
                text = "+",
                fontName = "gsans_bold",
                position = new UDim2(0f, 0.5f, 15f, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.1f,
                color = new Color(200, 255, 200)
            };
            rowButton.children.Add(plusIcon);

            // Difficulty Name & Star Rating
            string difficultyText = $"{bm.Version}  (★ {starRating:F2})";
            var label = new TextFrame
            {
                text = difficultyText,
                fontName = "gsans_bold",
                position = new UDim2(0f, 0.5f, 35f, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Center,
                scale = 1.0f,
                color = Color.White
            };
            rowButton.children.Add(label);

            // Vertically aligned difficulty color block on the right
            var colorBar = new Frame
            {
                position = new UDim2(1f, 0.5f, -8f, 0f),
                size = new UDim2(0f, 0f, 4f, 24f),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Center,
                color = GetDifficultyColor(bm)
            };
            rowButton.children.Add(colorBar);

            return rowButton;
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
                float hoveredScale = btn.IsHovered ? 1.06f : 1f;
                float targetScale = btn.IsPressed ? hoveredScale + 0.045f : hoveredScale;
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

                        // Restore keybindings
                        if (Enum.TryParse<Keys>(_settings.KeyToggleCover, out var k1)) _keyToggleCover = k1;
                        if (Enum.TryParse<Keys>(_settings.KeyStartGame, out var k2)) _keyStartGame = k2;
                        if (Enum.TryParse<Keys>(_settings.KeyExitGameplay, out var k3)) _keyExitGameplay = k3;
                        if (Enum.TryParse<Keys>(_settings.KeyHitLeft, out var k4)) _keyHitLeft = k4;
                        if (Enum.TryParse<Keys>(_settings.KeyHitRight, out var k5)) _keyHitRight = k5;
                        if (Enum.TryParse<Keys>(_settings.KeyToggleListenScore, out var k6)) _keyToggleListenScore = k6;

                        if (_taikofield != null)
                        {
                            _taikofield.ScrollSpeed = _settings.ScrollSpeed;
                            _taikofield.GlobalScale = _settings.GlobalScale;
                        }

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

                if (_taikofield != null)
                {
                    _settings.ScrollSpeed = _taikofield.ScrollSpeed;
                    _settings.GlobalScale = _taikofield.GlobalScale;
                }

                // Save keybindings
                _settings.KeyToggleCover = _keyToggleCover.ToString();
                _settings.KeyStartGame = _keyStartGame.ToString();
                _settings.KeyExitGameplay = _keyExitGameplay.ToString();
                _settings.KeyHitLeft = _keyHitLeft.ToString();
                _settings.KeyHitRight = _keyHitRight.ToString();
                _settings.KeyToggleListenScore = _keyToggleListenScore.ToString();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error saving settings: {ex.Message}");
            }
        }

        private float GetRealStarRating(OsuBeatmap bm) => StarRatingCache.GetRealStarRating(bm);

        private Color GetDifficultyColor(OsuBeatmap bm)
        {
            float sr = GetRealStarRating(bm);
            if (sr < 2.0f) return new Color(78, 186, 255);   // Easy — sky blue
            if (sr < 2.7f) return new Color(136, 224, 118);  // Normal — green
            if (sr < 4.0f) return new Color(255, 230, 118);  // Hard — yellow
            if (sr < 5.3f) return new Color(255, 118, 118);  // Insane — red
            if (sr < 6.5f) return new Color(200, 118, 255);  // Expert — purple
            return new Color(101, 99, 222);                   // Expert+ — dark indigo
        }
    }
}
