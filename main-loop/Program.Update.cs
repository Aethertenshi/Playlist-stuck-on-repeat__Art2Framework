using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UserInterface;
using ArtFrame.FileProcessing;
using OsuLib;
using System.Text.Json;

using static ArtFrame.AudioHelper;
using static ArtFrame.FontHelper;
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
            _currentDt = dt;
            if (_bgVideoFrame != null && !_bgVideoFrame.skipDraw && _bgVideoFrame.PlaybackState == VideoPlaybackState.Playing)
            {
                _bgDrop.alpha = MathF.Max(0f, _bgDrop.alpha - dt * 2.0f);
            }
            if (_resultscreen != null)
            {
                _resultscreen.CoverColor = _currentCoverColor;
            }
            if (Keyboard.IsKeyPressed(Keys.D0))
            {
                ShowPerformanceTelemetry = !ShowPerformanceTelemetry;
            }

            // Drain the OS drag-and-drop file queue
            OszDropHandler.DrainQueue(ProcessDroppedFile);

            // Process at most one lazy load task per frame to prevent main-thread stuttering
            if (_loadQueue.Count > 0)
            {
                var loadAction = _loadQueue.Dequeue();
                try { loadAction(); } catch { }
            }

            if (_inWarningScreen)
            {
                UpdateWarningScreen(dt);
                return;
            }

            // if (_inResultScreen)
            // {
            //     // Update audio crossfades
            //     var crossKeys = _audioTweeners.Keys.ToList();
            //     foreach (var key in crossKeys)
            //     {
            //         var tweener = _audioTweeners[key];
            //         if (tweener.IsPlaying)
            //             SetMusicVolume(key, tweener.CurrentValue);
            //         else if (tweener.CurrentValue <= 0f && key != _currentAudioKey)
            //         {
            //             _audioTweeners.Remove(key);
            //             Remove(tweener);
            //             StopMusic(key);
            //             UnloadMusic(key);
            //         }
            //     }
            //     return;
            // }

            // --- Customizable Hold-to-Exit Logic (like osu!) ---
            if (!_inIntro && !_isStarting && !_isListeningForKey)
            {
                if (Keyboard.IsKeyDown(_keyExitGame))
                {
                    _exitHoldTimer += dt;
                    if (_exitHoldTimer >= 0.5f)
                    {
                        StarRatingCache.Save();
                        Engine.Exit();
                    }
                }
                else
                {
                    _exitHoldTimer = 0f;
                }
            }
            else
            {
                _exitHoldTimer = 0f;
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
                        _welcomeTransition.Play(2.7f, Easing.Fluid, Direction.Out);
                        _logoRotation.Start(1.5f, 0f, -3.7f, Easing.Exponential, Direction.Out); // Smooth elegant rotation alignment

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

                        // Seamlessly transition from smooth VSync to player's custom menu performance settings!
                        SetPerformanceMode(_settings.MenuFps);
                        Engine.HighPrecisionLimiter.SetMaxFps(_settings.MenuFps);
                    }
                }
            }

            // Smoothly approach the target color using floats to prevent byte-truncation getting stuck
            _colorR += (_targetCoverColor.R - _colorR) * (dt * 2.5f);
            _colorG += (_targetCoverColor.G - _colorG) * (dt * 2.5f);
            _colorB += (_targetCoverColor.B - _colorB) * (dt * 2.5f);

            _currentCoverColor = new Color((byte)Math.Clamp(_colorR, 0, 255), (byte)Math.Clamp(_colorG, 0, 255), (byte)Math.Clamp(_colorB, 0, 255));

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
                        float scaleMultiplier = 1f + wave.Progress * 0.15f; // expands to 1.15x scale!
                        float baseSizeScale = 0.4f * MathF.Max(_logoTweener.CurrentValue, _startTransitionTweener.CurrentValue);
                        wave.VisualNode.size = new UDim2(baseSizeScale * scaleMultiplier, baseSizeScale * scaleMultiplier);
                        
                        // Center inside blurBg (which aligns perfectly with logo center)
                        wave.VisualNode.position = _logoUI.position;
                        wave.VisualNode.rotation = _logoRotation.CurrentValue;
                        wave.VisualNode.alpha = (1f - wave.Progress) * 0.65f * (1f - _peekBg);
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


            // --- Game Start Sequence (Press TAB) ---
            if (!_isStarting && Keyboard.IsKeyPressed(_keyStartGame) && _isCoverView && !_isListeningForKey && !Mouse.RightDown())
            {
                SetPerformanceMode(_settings.GameplayFps);
                Engine.HighPrecisionLimiter.SetMaxFps(_settings.GameplayFps);

                PlaySFX("play-click"); // Optional feedback
                _isStarting = true;
                _isLowPassEnabled = true;
                _startTimer = 0f;

                // 1. Force close any open side panels
                _settingsTweener.Restart(0.5f, 0f, Easing.Exponential, Direction.Out);
                _modifiersTweener.Restart(0.5f, 0f, Easing.Exponential, Direction.Out);
                
                _startPhase = 1;
                // Trigger Phase 1 immediately
                _startTransitionTweener.Restart(1.1f, 1.0f, Easing.Fluid, Direction.Out);
                _lowPassTweener.Restart(1.1f, 1.0f, Easing.Fluid, Direction.Out);

                // Under-water effect: turn down volume a bit and muffle music
                if (_audioTweeners.ContainsKey(_currentAudioKey))
                    _audioTweeners[_currentAudioKey].Restart(1.1f, _targetVolume * 0.6f, Easing.Exponential, Direction.Out);
                SetMusicLowPass(_currentAudioKey, _isLowPassEnabled, 20000f);
            }

            // --- Starting Gameplay ---
            if (Keyboard.IsKeyPressed(_keyExitGameplay) && _startPhase < 3)
            {
                _isStarting = false;
                _startTimer = 0f;
                _startShrinkTweener.Restart(1f, 0f, Easing.Fluid, Direction.Out);
                _startTransitionTweener.Restart(1.5f, 0f, Easing.Fluid, Direction.Out);
                _lowPassTweener.Restart(1f, 0f, Easing.Exponential, Direction.In);

                if (_audioTweeners.ContainsKey(_currentAudioKey))
                    _audioTweeners[_currentAudioKey].Restart(0.5f, _targetVolume, Easing.Exponential, Direction.Out);

                SetPerformanceMode(_settings.MenuFps);
                Engine.HighPrecisionLimiter.SetMaxFps(_settings.MenuFps);
            }
            
            // Low Pass Update
            if (!_isStarting && _lowPassTweener.IsPlaying && _isLowPassEnabled)
            {
                float cutoff = ArtMathHelper.Lerp(20000f, 300f, _lowPassTweener.CurrentValue);
                SetMusicLowPass(_currentAudioKey, _isLowPassEnabled, cutoff);
            }
            if (_isLowPassEnabled && !_lowPassTweener.IsPlaying && !_isStarting)
            {
                _isLowPassEnabled = false;
                SetMusicLowPass(_currentAudioKey, _isLowPassEnabled);

                Console.WriteLine("LowPass Disabled");
            }

            // --- Background Video Sync & Playback ---
            if (_bgVideoFrame != null)
            {
                if (_videoSeekCooldown > 0f)
                {
                    _videoSeekCooldown -= dt;
                }

                if ((_startPhase == 2 || _startPhase == 3) && _settings.EnableCanvasMovie && _beatmap != null && !string.IsNullOrEmpty(_bgVideoFilename))
                {
                    bool musicPlaying = IsMusicPlaying(_currentAudioKey) && _startPhase == 3;
                    float musicTime = _startPhase == 3 ? GetMusicTimePlayed(_currentAudioKey) : 0f;
                    double videoOffsetMs = _beatmap.GetVideoOffsetMs();
                    double targetVidMs = (musicTime * 1000.0) - videoOffsetMs;

                    if (targetVidMs >= 0)
                    {
                        if (_bgVideoFrame.skipDraw && _startPhase == 3)
                        {
                            _bgVideoFrame.skipDraw = false;
                            _bgVideoFrame.Volume = 0f;
                        }

                        if (_bgVideoFrame.PlaybackState == VideoPlaybackState.Stopped)
                        {
                            _bgVideoFrame.Play(_bgVideoFilename);
                            _bgVideoFrame.PositionMs = (long)targetVidMs;
                            _videoSeekCooldown = 1.5f; // Prevent seeking during initial player buffering
                        }

                        if (musicPlaying)
                        {
                            if (_bgVideoFrame.PlaybackState != VideoPlaybackState.Playing)
                            {
                                _bgVideoFrame.Resume();
                            }

                            // Sync playback rate to music speed
                            if (Math.Abs(_bgVideoFrame.PlaybackRate - _actualMusicSpeed) > 0.01f)
                            {
                                _bgVideoFrame.PlaybackRate = _actualMusicSpeed;
                            }

                            // Sync position if it drifts by more than 500ms and seek cooldown has expired
                            if (_videoSeekCooldown <= 0f)
                            {
                                long actualVidMs = _bgVideoFrame.PositionMs;
                                if (Math.Abs(actualVidMs - targetVidMs) > 500.0)
                                {
                                    _bgVideoFrame.PositionMs = (long)targetVidMs;
                                    _videoSeekCooldown = 1.5f; // Set cooldown after a hard seek
                                }
                            }
                        }
                        else
                        {
                            // Music is paused, not started yet, or we are in phase 2 pre-load
                            if (_bgVideoFrame.PlaybackState == VideoPlaybackState.Playing)
                            {
                                _bgVideoFrame.Pause();
                            }

                            // Sync initial/pause position only if drift is significant to avoid continuous seeks
                            if (_videoSeekCooldown <= 0f)
                            {
                                long actualVidMs = _bgVideoFrame.PositionMs;
                                if (Math.Abs(actualVidMs - targetVidMs) > 200.0)
                                {
                                    _bgVideoFrame.PositionMs = (long)targetVidMs;
                                    _videoSeekCooldown = 1.0f;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Video hasn't reached start time yet
                        if (!_bgVideoFrame.skipDraw)
                        {
                            _bgVideoFrame.Stop();
                            _bgVideoFrame.skipDraw = true;
                        }
                    }
                }
                else
                {
                    // Video is disabled or not in gameplay phase
                    if (!_bgVideoFrame.skipDraw)
                    {
                        _bgVideoFrame.Stop();
                        _bgVideoFrame.skipDraw = true;
                    }
                }
            }

            if (_isStarting)
            {
                _startTimer += dt;

                // During Phase 1, sweep low-pass cutoff down smoothly
                if (_startPhase == 1)
                {
                    float cutoff = ArtMathHelper.Lerp(20000f, 300f, _lowPassTweener.CurrentValue);
                    SetMusicLowPass(_currentAudioKey, true, cutoff);
                }
                // During Phase 2 (The Shrink), sweep low-pass down and fade volume to 0 following the shrink tweener
                else if (_startPhase == 2)
                {
                    float t = _startShrinkTweener.CurrentValue;
                    float vol = (_targetVolume * 0.45f) * (1f - t);
                    SetMusicVolume(_currentAudioKey, vol);
                }

                // Wait 0.5 seconds in Phase 0 (for centering) then trigger Phase 1
                if (_startPhase == 0 && _startTimer >= 0.5f)
                {
                    _startPhase = 1;
                    _startTimer = 0f; // Reset timer so Phase 1 starts at 0

                    // Trigger Phase 1 (UI Fades out, Cover slides to center, bgDrop darkens)
                    _startTransitionTweener.Restart(1.6f, 1.0f, Easing.Fluid, Direction.InOut);
                    _lowPassTweener.Restart(1.6f, 1.0f, Easing.Exponential, Direction.Out);

                    // Under-water effect: turn down volume a bit and muffle music
                    if (_audioTweeners.ContainsKey(_currentAudioKey))
                        _audioTweeners[_currentAudioKey].Restart(1.5f, _targetVolume * 0.5f, Easing.Exponential, Direction.Out);
                    _isLowPassEnabled = true;
                    SetMusicLowPass(_currentAudioKey, _isLowPassEnabled, 20000f);
                }
                // Wait 1.5 seconds, then trigger Phase 2 (The Shrink)
                else if (_startPhase == 1 && _startTimer >= 1.3f)
                {
                    _bgVideoFilename = "";
                    if (_settings.EnableCanvasMovie && _beatmap != null && _bgVideoFrame != null)
                    {
                        string rawVid = _beatmap.GetVideoFullPath();
                        if (!string.IsNullOrEmpty(rawVid))
                        {
                            string resolvedVid = "";
                            if (File.Exists(rawVid))
                            {
                                resolvedVid = rawVid;
                            }
                            else
                            {
                                // Fallback extensions if raw extension isn't found on disk
                                string[] fallbacks = { ".mp4", ".avi", ".ogv", ".ogg", ".flv" };
                                foreach (var ext in fallbacks)
                                {
                                    string testPath = Path.ChangeExtension(rawVid, ext);
                                    if (File.Exists(testPath))
                                    {
                                        resolvedVid = testPath;
                                        break;
                                    }
                                }
                            }

                            if (!string.IsNullOrEmpty(resolvedVid))
                            {
                                _bgVideoFilename = resolvedVid;
                            }
                        }
                    }

                    _startPhase = 2;
                    _startShrinkTweener.Restart(2.1f, 1.0f, Easing.Fluid, Direction.InOut);
                }
                // Wait another 1.5 seconds, then load the game
                else if (_startPhase == 2 && _startTimer >= 3.5f)
                {
                    _startPhase = 3;
                    _isLowPassEnabled = false;
                    SetMusicLowPass(_currentAudioKey, _isLowPassEnabled); // Disable LowPass filter for clean gameplay audio!

                    // TODO: ENTER GAMEPLAY SCENE

                    StopMusic(_currentAudioKey);
                    SeekMusic(_currentAudioKey, 0f);

                    // 1. Recycle the existing rhythm indexer and tell it to wait for 0.0s!
                    _rythmIndexer?.MusicOffset = _settings.AudioOffset;
                    _rythmIndexer?.Beatmap = _beatmap;
                    _rythmIndexer?.Reset(0f); // Uses your InterpolatingAudioClock's built in Reset

                    if (_beatmap != null)
                    {
                        // Re-parse fully (with hit-objects) right before active gameplay starts
                        _beatmap = _parser.Parse(_beatmap.FilePath, metadataOnly: false);
                    }

                    // 2. Wipe any old state and load the new notes
                    Action onSplitFinished = () =>
                    {
                        Console.WriteLine("Split Finished");
                        if (_audioTweeners.ContainsKey(_currentAudioKey))
                        {
                            _audioTweeners[_currentAudioKey].Restart(0.5f, _targetVolume, Easing.Fluid, Direction.Out);
                            PlayMusic(_currentAudioKey);
                        }
                    };

                    if (_activeGameplayMode == GameplayMode.Taiko)
                    {
                        _taikofield?.alpha = 0f;
                        _taikofield?.IsAutoplay = _modAutoplay;
                        _taikofield?.SingleMode = _modSingleMode;
                        _taikofield?.GlobalScale = _settings.GlobalScale;
                        _taikofield?.ResetState();
                        _taikofield?.LoadBeatmap(_beatmap);
                        _taikofield?.OnSplitFinished = onSplitFinished;

                        _stackfield?.ResetState();
                        _stackfield?.alpha = 0f;
                        _stackfield?.LoadBeatmap(null);
                    }
                    else
                    {
                        _stackfield?.alpha = 0f;
                        _stackfield?.IsAutoplay = _modAutoplay;
                        _stackfield?.GlobalScale = _settings.GlobalScale;
                        _stackfield?.ResetState();
                        _stackfield?.LoadBeatmap(_beatmap);
                        _stackfield?.OnSplitFinished = onSplitFinished;

                        _taikofield?.ResetState();
                        _taikofield?.alpha = 0f;
                        _taikofield?.LoadBeatmap(null);
                    }
                }
                else if (_startPhase == 3)
                {
                    bool finished = false;
                    if (_activeGameplayMode == GameplayMode.Taiko)
                        finished = _taikofield != null ? _taikofield.IsGameplayFinished : true;
                    else
                        finished = _stackfield != null ? _stackfield.IsGameplayFinished : true;

                    if (GetMusicTimePlayed(_currentAudioKey) >= GetMusicLength(_currentAudioKey) - 1f)
                    {
                        Console.WriteLine("Is Finished");
                        finished = true;
                    }

                    if (finished)
                    {
                        _gameplayFinishTimer += dt;
                        if (_gameplayFinishTimer >= 0.25f && !_inResultScreen)
                        {
                            Console.WriteLine("Show result screen");

                            _gameplayFinishTimer = 0f;
                            _inResultScreen = true;

                            if (_bgVideoFrame != null)
                            {
                                _bgVideoFrame.Stop();
                                _bgVideoFrame.skipDraw = true;
                            }
                            _bgDrop.alpha = 1f;

                            if (_activeGameplayMode == GameplayMode.Taiko)
                            {
                                _resultscreen?.Show(_beatmap, _taikofield != null ? _taikofield.Score : 0, _taikofield != null ? _taikofield.MaxComboReached : 0, _taikofield != null ? _taikofield.HitsPerfect : 0, _taikofield != null ? _taikofield.HitsGood : 0, _taikofield != null ? _taikofield.HitsOk : 0, _taikofield != null ? _taikofield.HitsMiss : 0, _modAutoplay);
                                if (!_modAutoplay && _taikofield != null)
                                    _online.SubmitScore(_beatmap?.BeatmapSetId ?? 0, _taikofield.Score, _taikofield.MaxComboReached, _taikofield.HitsPerfect, _taikofield.HitsGood, _taikofield.HitsOk, _taikofield.HitsMiss, _activeGameplayMode, _beatmap?.Version ?? "Unknown");
                            }
                            else
                            {
                                _resultscreen.Show(_beatmap, _stackfield != null ? _stackfield.Score : 0, _stackfield != null ? _stackfield.MaxComboReached : 0, _stackfield != null ? _stackfield.HitsPerfect : 0, _stackfield != null ? _stackfield.HitsGood : 0, _stackfield != null ? _stackfield.HitsOk : 0, _stackfield != null ? _stackfield.HitsMiss : 0, _modAutoplay);
                                if (!_modAutoplay && _stackfield != null)
                                    _online.SubmitScore(_beatmap?.BeatmapSetId ?? 0, _stackfield.Score, _stackfield.MaxComboReached, _stackfield.HitsPerfect, _stackfield.HitsGood, _stackfield.HitsOk, _stackfield.HitsMiss, _activeGameplayMode, _beatmap?.Version ?? "Unknown");
                            }
                        }
                    }
                    else
                    {
                        _gameplayFinishTimer = 0f;
                    }
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
                        else if (_listeningActionName == "ExitGameplay") { _keyExitGameplay = key; if (_taikofield != null) _taikofield.ExitKey = key; if (_stackfield != null) _stackfield.ExitKey = key; }
                        else if (_listeningActionName == "HitLeft1") { _keyHitLeft1 = key; RealTimeInputEngine.ConfigureKeys(new int[4] { (int)_keyHitLeft1, (int)_keyHitLeft2, (int)_keyHitRight1, (int)_keyHitRight2 }); var hk = new Keys[] { _keyHitLeft1, _keyHitLeft2, _keyHitRight1, _keyHitRight2 }; if (_taikofield != null) _taikofield.HitKeys = hk; if (_stackfield != null) _stackfield.HitKeys = hk; }
                        else if (_listeningActionName == "HitLeft2") { _keyHitLeft2 = key; RealTimeInputEngine.ConfigureKeys(new int[4] { (int)_keyHitLeft1, (int)_keyHitLeft2, (int)_keyHitRight1, (int)_keyHitRight2 }); var hk = new Keys[] { _keyHitLeft1, _keyHitLeft2, _keyHitRight1, _keyHitRight2 }; if (_taikofield != null) _taikofield.HitKeys = hk; if (_stackfield != null) _stackfield.HitKeys = hk; }
                        else if (_listeningActionName == "HitRight1") { _keyHitRight1 = key; RealTimeInputEngine.ConfigureKeys(new int[4] { (int)_keyHitLeft1, (int)_keyHitLeft2, (int)_keyHitRight1, (int)_keyHitRight2 }); var hk = new Keys[] { _keyHitLeft1, _keyHitLeft2, _keyHitRight1, _keyHitRight2 }; if (_taikofield != null) _taikofield.HitKeys = hk; if (_stackfield != null) _stackfield.HitKeys = hk; }
                        else if (_listeningActionName == "HitRight2") { _keyHitRight2 = key; RealTimeInputEngine.ConfigureKeys(new int[4] { (int)_keyHitLeft1, (int)_keyHitLeft2, (int)_keyHitRight1, (int)_keyHitRight2 }); var hk = new Keys[] { _keyHitLeft1, _keyHitLeft2, _keyHitRight1, _keyHitRight2 }; if (_taikofield != null) _taikofield.HitKeys = hk; if (_stackfield != null) _stackfield.HitKeys = hk; }
                        else if (_listeningActionName == "ExitGame") _keyExitGame = key;

                        _isListeningForKey = false;
                        _listeningActionName = "";
                        SaveSettings();
                        PlaySFX("select");
                        break;
                    }
                }
            }

            // --- Replaying ---
            if (GetMusicTimePlayed(_currentAudioKey) >= GetMusicLength(_currentAudioKey) - 0.025f && !_inResultScreen)
            {
                StopMusic(_currentAudioKey);
                PlayMusic(_currentAudioKey);
                SeekMusic(_currentAudioKey, _beatmap?.PreviewTime / 1000f ?? 0f);
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
                    Remove(tweener);
                    StopMusic(key);
                    UnloadMusic(key);
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
                                var beatmap = _parser.Parse(osuFile, metadataOnly: true);
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
                    var beatmap = _parser.Parse(path, metadataOnly: true);
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

            string newAudioPath = Path.Combine(Path.GetDirectoryName(targetMap.FilePath) ?? "", targetMap.AudioFilename);
            string oldAudioPath = _beatmap != null ? Path.Combine(Path.GetDirectoryName(_beatmap.FilePath) ?? "", _beatmap.AudioFilename) : "";

            if (newAudioPath == oldAudioPath && !string.IsNullOrEmpty(oldAudioPath))
            {
                // Same audio track/beatmap set, just a different difficulty!
                _beatmap = targetMap;
                if (_rythmIndexer != null) _rythmIndexer.Beatmap = _beatmap;
                return;
            }

            // 1. Fade out the CURRENT song (if it exists)
            if (_audioTweeners.ContainsKey(_currentAudioKey))
            {
                // Retarget the existing tweener to 0. 
                // Because of your Tweener.Restart logic, it smoothly fades down from its CURRENT volume! No snapping!
                _audioTweeners[_currentAudioKey].Restart(0.5f, 0f, Easing.Exponential, Direction.Out);
            }
            string newBgKey = targetMap.BeatmapSetId.ToString();
            string oldBgKey = _beatmap?.BeatmapSetId.ToString() ?? "";

            // 2. Setup the New Song Identity
            _audioCounter++;
            string loadingAudioKey = $"au_{_audioCounter}";
            _currentAudioKey = loadingAudioKey;
            _beatmap = targetMap;

            if (_rythmIndexer != null) _rythmIndexer.Beatmap = _beatmap;

            // 3. Load assets asynchronously in the background
            string bgPath = targetMap.GetBackgroundFullPath();
            string audioPath = Path.Combine(Path.GetDirectoryName(targetMap.FilePath) ?? "", targetMap.AudioFilename);
            float previewTime = targetMap.PreviewTime / 1000f;

            Task.Run(() =>
            {
                try
                {
                    // A. Load new background image (thread-safe lock in GraphicsHelper)
                    Image newBg = LoadImage(newBgKey, bgPath);

                    // B. Compute average color (blocks background thread, not main update loop)
                    Color averageColor = GetAverageColor(newBg, 28);

                    // C. Load new audio file (thread-safe lock in AudioHelper)
                    LoadMusic(loadingAudioKey, audioPath);

                    // D. Dispatch visual update and music playback back to the main thread
                    _loadQueue.Enqueue(() =>
                    {
                        // Check if this request is still the active/latest one (ignores stale requests from fast-clicking)
                        if (_currentAudioKey == loadingAudioKey)
                        {
                            if (bg != null) bg.texture = newBg;
                            _targetCoverColor = averageColor;

                            if (newBgKey != oldBgKey && !string.IsNullOrEmpty(oldBgKey))
                            {
                                Console.WriteLine($"Unloading image... {oldBgKey} Before Loading New Image {newBgKey}");
                                UnloadImage(oldBgKey);
                            }

                            SetMusicVolume(loadingAudioKey, 0f); // Force start at 0 volume
                            SetMusicSpeed(loadingAudioKey, _speedMultiplier, _adjustPitch);
                            PlayMusic(loadingAudioKey);
                            SeekMusic(loadingAudioKey, previewTime);

                            // Create and start the Fade-In Tweener
                            var fadeInTweener = AddTween(new Tweener());
                            fadeInTweener.SetValue(0f); // Snap tweener state to 0
                            fadeInTweener.Restart(0.5f, _targetVolume, Easing.Exponential, Direction.In);

                            _audioTweeners[loadingAudioKey] = fadeInTweener;
                        }
                        else
                        {
                            // If a newer song has already been selected, clean up this audio stream
                            UnloadMusic(loadingAudioKey);
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ChangeSong] Async loading error: {ex.Message}");
                }
            });
        }

        private void RepopulatePlaylist()
        {
            _loadQueue.Clear();
            if (_playlistScroll == null) return;
            _playlistScroll.children.Clear();

            int layoutOrderCounter = 0;
            int songNumber = 1;

            foreach (var group in _beatmapGroups)
            {
                // 1. Create and add Header card
                var header = CreateHeaderRow(group, songNumber);
                header.LayoutOrder = layoutOrderCounter++;
                _playlistScroll.children.Add(header);

                // 2. Add mini difficulty header
                var diffHeader = CreateDifficultyHeaderRow(group);
                diffHeader.LayoutOrder = layoutOrderCounter++;
                _playlistScroll.children.Add(diffHeader);

                // 3. Add nested difficulties
                int diffIndex = 1;
                foreach (var diff in group.Difficulties)
                {
                    var diffRow = CreateDifficultyRow(diff, group, $"{songNumber}.{diffIndex}");
                    diffRow.LayoutOrder = layoutOrderCounter++;
                    _playlistScroll.children.Add(diffRow);
                    diffIndex++;
                }

                songNumber++;
            }
        }

        private void DeleteSong(BeatmapGroup group)
        {
            var bm = group.Representative;
            if (bm == null) return;

            string folderPath = Path.GetDirectoryName(bm.FilePath) ?? "";
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return;

            try
            {
                // 1. If we are currently playing this song, change target or stop it.
                if (_beatmap != null && Path.GetDirectoryName(_beatmap.FilePath) == folderPath)
                {
                    // Find another beatmap that is NOT in this folder
                    var otherGroup = _beatmapGroups.FirstOrDefault(g => g != group);
                    if (otherGroup != null && otherGroup.Difficulties.Count > 0)
                    {
                        var nextBm = otherGroup.Difficulties[0];
                        _starRating = GetRealStarRating(nextBm);
                        
                        // Stop current music immediately to release file lock before changing song
                        StopMusic(_currentAudioKey);
                        UnloadMusic(_currentAudioKey);
                        if (_audioTweeners.ContainsKey(_currentAudioKey))
                        {
                            var tw = _audioTweeners[_currentAudioKey];
                            _audioTweeners.Remove(_currentAudioKey);
                            Remove(tw);
                        }

                        // Also unload current cover image to release file lock
                        string currentBgKey = _beatmap.BeatmapSetId.ToString();
                        UnloadImage(currentBgKey);

                        // Switch to the next song
                        ChangeSong(nextBm, _bgImageFrame);
                    }
                    else
                    {
                        // No other songs left!
                        _beatmap = null;
                        StopMusic(_currentAudioKey);
                        UnloadMusic(_currentAudioKey);
                        if (_audioTweeners.ContainsKey(_currentAudioKey))
                        {
                            var tw = _audioTweeners[_currentAudioKey];
                            _audioTweeners.Remove(_currentAudioKey);
                            Remove(tw);
                        }
                    }
                }

                // Unload all thumbnails of difficulties in this group to release locks
                string thumbId = bm.BeatmapSetId.ToString() + "_thumb";
                UnloadImage(thumbId);

                // Give the system a brief moment (e.g. 50ms) to ensure file handles are released by the audio/graphics library
                System.Threading.Thread.Sleep(50);

                // Delete the folder
                Directory.Delete(folderPath, true);
                Console.WriteLine($"[MainGame] Deleted beatmap folder: {folderPath}");

                // Reload scanned beatmaps and repopulate the playlist
                var scannedBeatmaps = _scanner.ScanLazy(SongsPath, metadataOnly: true).ToList();
                var groups = scannedBeatmaps
                    .GroupBy(g => g.BeatmapSetId > 0 ? g.BeatmapSetId.ToString() : $"{g.Title}_{g.Artist}")
                    .Select(g => new BeatmapGroup
                    {
                        Key = g.Key,
                        Representative = g.First(),
                        Difficulties = g.OrderBy(dbm => GetRealStarRating(dbm)).ToList()
                    })
                    .ToList();

                _beatmapGroups.Clear();
                _beatmapGroups.AddRange(groups);

                // If _beatmap was null and we have scanned beatmaps left, select one randomly
                if (_beatmap == null && scannedBeatmaps.Count > 0)
                {
                    var beatmapRand = new Random();
                    _beatmap = scannedBeatmaps[beatmapRand.Next(scannedBeatmaps.Count)];
                    _starRating = GetRealStarRating(_beatmap);
                    ChangeSong(_beatmap, _bgImageFrame);
                }

                RepopulatePlaylist();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error deleting song: {ex.Message}");
            }
        }

        private Frame CreateHeaderRow(BeatmapGroup group, int songNumber)
        {
            float currentOffsetX = -20f;
            var bm = group.Representative;

            Frame headerFrame = new Frame
            {
                position = new UDim2(1f, 0f, -20f, 0f),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,
                size = new UDim2(1f, 0f, -40f, 80f),
                color = new Color(0, 0, 0, 0) // transparent container
            };

            var rowButton = new Button
            {
                position = new UDim2(0f, 0f, 0f, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(1f, 0f, -45f, 80f), // Width is 100% of container minus 45px (for delete button)
                onClick = (b) =>
                {
                    // Select the easiest difficulty from the set directly!
                    if (group.Difficulties.Count > 0)
                    {
                        var easiestDiff = group.Difficulties[0];
                        _starRating = GetRealStarRating(easiestDiff);
                        ChangeSong(easiestDiff, _bgImageFrame);
                    }

                    // Toggle expansion state, collapse others
                    bool nextState = !group.IsExpanded;
                    foreach (var g in _beatmapGroups)
                    {
                        g.IsExpanded = (g == group) ? nextState : false;
                    }

                    PlaySFX("select");
                },
                onHoverEnter = (b) =>
                {
                    PlaySFX("hover");
                }
            };
            headerFrame.children.Add(rowButton);

            Button deleteBtn = new Button
            {
                position = new UDim2(1f, 0f, 0f, 0f),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,
                size = new UDim2(0f, 0f, 40f, 80f), // Width: 40px, Height: 80px
                color = new Color(180, 50, 50, 100),
                hoverColor = new Color(240, 70, 70, 200),
                pressedColor = new Color(255, 30, 30, 245),
                onHoverEnter = (_) => PlaySFX("hover"),
                onClick = (_) =>
                {
                    PlaySFX("select");
                    _loadQueue.Enqueue(() => DeleteSong(group));
                }
            };
            headerFrame.children.Add(deleteBtn);

            // Add a cross close/delete symbol inside deleteBtn
            deleteBtn.children.Add(new TextFrame
            {
                text = "✕",
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.3f,
                color = Color.White
            });

            headerFrame.onUpdate = (e, dt) =>
            {
                float targetOffsetX = rowButton.IsHovered || deleteBtn.IsHovered ? -28f : -20f;
                currentOffsetX = ArtMathHelper.Lerp(currentOffsetX, targetOffsetX, 0.1f);
                headerFrame.position = new UDim2(1f, 0f, currentOffsetX, headerFrame.position.OffsetY);
            };

            rowButton.onUpdate = (btn) =>
            {
                byte r = (byte)(_currentCoverColor.R * 0.85f);
                byte g = (byte)(_currentCoverColor.G * 0.85f);
                byte b = (byte)(_currentCoverColor.B * 0.85f);

                btn.color = new Color(r, g, b, 175);
                btn.hoverColor = new Color(r, g, b, 235);
                btn.pressedColor = new Color(r, g, b, 255);
            };

            // Song Index Number label (placed at X = 15px)
            rowButton.children.Add(new TextFrame
            {
                text = songNumber.ToString(),
                fontName = "gsans_bold",
                position = new UDim2(0f, 0.5f, 15f, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.2f,
                color = Color.White
            });

            var thumbFrame = new ImageFrame
            {
                texture = LoadImage("logo"),
                position = new UDim2(0f, 0.5f, 45f, 0f), // shifted right to 45px
                size = new UDim2(0f, 0f, 60f, 60f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                fit = ObjectFit.Cover
            };
            rowButton.children.Add(thumbFrame);

            // Lazy load background
            string id = bm.BeatmapSetId.ToString() + "_thumb";
            string bgPath = bm.GetBackgroundFullPath();
            _loadQueue.Enqueue(() =>
            {
                try
                {
                    Image loadedTexture = LoadImageResized(id, bgPath, 320, 180);
                    thumbFrame.texture = loadedTexture;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MainGame] Error lazy loading background for header {id}: {ex.Message}");
                }
            });

            // Title & Artist (shifted right to 120px)
            rowButton.children.Add(new TextFrame { text = bm.Title, fontName = "gsans_bold", position = new UDim2(0f, 0f, 120f, 18f), anchorX = AnchorX.Left, anchorY = AnchorY.Top, textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top, scale = 1.3f, color = Color.White });
            
            // Subtext with Diffs Count (shifted right to 120px)
            rowButton.children.Add(new TextFrame { text = $"{bm.Artist}  //  [ {group.Difficulties.Count} difficulties ]", fontName = "gsans", position = new UDim2(0f, 0f, 120f, 45f), anchorX = AnchorX.Left, anchorY = AnchorY.Top, textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top, scale = 1.0f, color = new Color(220, 220, 255) });

            return headerFrame;
        }

        private Frame CreateDifficultyHeaderRow(BeatmapGroup group)
        {
            // Tweener for the height/expansion animation
            Tweener heightTweener = new Tweener();
            heightTweener.SetValue(group.IsExpanded ? 24f : 0f);
            float lastTargetHeight = group.IsExpanded ? 24f : 0f;

            var headerFrame = new Frame
            {
                position = new UDim2(1f, 0f, -20f, 0f),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Top,
                color = new Color(0, 0, 0, 0) // transparent
            };

            // Mini Labels
            var hashLabel = new TextFrame
            {
                text = "#",
                fontName = "gsans_bold",
                position = new UDim2(0f, 0.5f, 20f, 0f), // aligned with the diff index
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 0.85f,
                color = Color.White
            };
            headerFrame.children.Add(hashLabel);

            var diffLabel = new TextFrame
            {
                text = "Difficulty",
                fontName = "gsans_bold",
                position = new UDim2(0f, 0.5f, 60f, 0f), // aligned with the diff name
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Center,
                scale = 0.85f,
                color = Color.White
            };
            headerFrame.children.Add(diffLabel);

            var ratingLabel = new TextFrame
            {
                text = "Rating",
                fontName = "gsans_bold",
                position = new UDim2(1f, 0.5f, -8f, 0f), // aligned with the color bar on the right
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Right,
                textAnchorY = AnchorY.Center,
                scale = 0.85f,
                color = Color.White
            };
            headerFrame.children.Add(ratingLabel);

            headerFrame.onUpdate = (f, dt) =>
            {
                float targetHeight = group.IsExpanded ? 24f : 0f;
                if (targetHeight != lastTargetHeight)
                {
                    lastTargetHeight = targetHeight;
                    heightTweener.Restart(0.25f, targetHeight, Easing.Cubic, Direction.Out);
                }
                
                headerFrame.skipDraw = (heightTweener.CurrentValue < 0.1f && !group.IsExpanded);
                if (headerFrame.skipDraw) return; // skip updating height when culled
                heightTweener.Update(_currentDt);

                headerFrame.size = new UDim2(1f, 0f, -80f, heightTweener.CurrentValue); // width aligned with difficulty rows

                float alphaFraction = heightTweener.CurrentValue / 24f;
                headerFrame.alpha = alphaFraction;
                hashLabel.alpha = alphaFraction;
                diffLabel.alpha = alphaFraction;
                ratingLabel.alpha = alphaFraction;
            };

            return headerFrame;
        }

        private Button CreateDifficultyRow(OsuBeatmap bm, BeatmapGroup group, string indexStr)
        {
            float currentOffsetX = -20f;
            float starRating = GetRealStarRating(bm);

            // Tweener for the height/expansion animation
            Tweener heightTweener = new Tweener();
            // Start it at the current state instantly (either 40 or 0)
            heightTweener.SetValue(group.IsExpanded ? 40f : 0f);
            float lastTargetHeight = group.IsExpanded ? 40f : 0f;

            var rowButton = new Button
            {
                position = new UDim2(1f, 0f, -20f, 0f),
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

            // Difficulty Index label (placed at X = 20px)
            var indexLabel = new TextFrame
            {
                text = indexStr,
                fontName = "gsans_bold",
                position = new UDim2(0f, 0.5f, 20f, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 0.95f,
                color = Color.White
            };
            rowButton.children.Add(indexLabel);

            // Difficulty Name & Star Rating (aligned 60px from left)
            string difficultyText = $"{bm.Version}  (★ {starRating:F2})";
            var label = new TextFrame
            {
                text = difficultyText,
                fontName = "gsans_bold",
                position = new UDim2(0f, 0.5f, 60f, 0f),
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

            rowButton.onUpdate = (btn) =>
            {
                // Update the height tweener
                float targetHeight = group.IsExpanded ? 40f : 0f;
                if (targetHeight != lastTargetHeight)
                {
                    lastTargetHeight = targetHeight;
                    heightTweener.Restart(0.25f, targetHeight, Easing.Cubic, Direction.Out);
                }
                
                rowButton.skipDraw = (heightTweener.CurrentValue < 0.1f && !group.IsExpanded);
                if (rowButton.skipDraw) return; // skip update when fully collapsed
                heightTweener.Update(_currentDt);

                float targetOffsetX = btn.IsHovered ? -26f : -20f;
                currentOffsetX = ArtMathHelper.Lerp(currentOffsetX, targetOffsetX, 0.1f);

                rowButton.position = new UDim2(1f, 0f, currentOffsetX, rowButton.position.OffsetY);
                rowButton.size = new UDim2(1f, 0f, -80f, heightTweener.CurrentValue); // Width is 100% of scrollframe minus 80px (for indent)

                // Fade alpha of background and children
                float alphaFraction = heightTweener.CurrentValue / 40f;
                rowButton.alpha = alphaFraction;

                indexLabel.alpha = alphaFraction;
                label.alpha = alphaFraction;
                colorBar.alpha = alphaFraction;

                byte r = (byte)(_currentCoverColor.R * 0.7f);
                byte g = (byte)(_currentCoverColor.G * 0.7f);
                byte b = (byte)(_currentCoverColor.B * 0.7f);

                btn.color = new Color(r, g, b, 175);
                btn.hoverColor = new Color(r, g, b, 235);
                btn.pressedColor = new Color(r, g, b, 255);
            };

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
            string id = bm.BeatmapSetId.ToString() + "_thumb";
            string bgPath = bm.GetBackgroundFullPath();
            _loadQueue.Enqueue(() =>
            {
                try
                {
                    Image loadedTexture = LoadImageResized(id, bgPath, 320, 180);
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
                        if (Enum.TryParse<Keys>(_settings.KeyHitLeft1, out var k4)) _keyHitLeft1 = k4;
                        if (Enum.TryParse<Keys>(_settings.KeyHitRight1, out var k5)) _keyHitRight1 = k5;
                        if (Enum.TryParse<Keys>(_settings.KeyExitGame, out var k7)) _keyExitGame = k7;
                        if (Enum.TryParse<Keys>(_settings.KeyHitLeft2, out var k8)) _keyHitLeft2 = k8;
                        if (Enum.TryParse<Keys>(_settings.KeyHitRight2, out var k9)) _keyHitRight2 = k9;

                        if (_taikofield != null)
                        {
                            _taikofield.ScrollSpeed = _settings.ScrollSpeed;
                            _taikofield.GlobalScale = _settings.GlobalScale;
                        }
                        if (_stackfield != null)
                        {
                            _stackfield.ScrollSpeed = _settings.ScrollSpeed;
                            _stackfield.GlobalScale = _settings.GlobalScale;
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
                else if (_stackfield != null)
                {
                    _settings.ScrollSpeed = _stackfield.ScrollSpeed;
                    _settings.GlobalScale = _stackfield.GlobalScale;
                }

                // Save keybindings
                _settings.KeyToggleCover = _keyToggleCover.ToString();
                _settings.KeyStartGame = _keyStartGame.ToString();
                _settings.KeyExitGameplay = _keyExitGameplay.ToString();
                _settings.KeyHitLeft1 = _keyHitLeft1.ToString();
                _settings.KeyHitLeft2 = _keyHitLeft2.ToString();
                _settings.KeyHitRight1 = _keyHitRight1.ToString();
                _settings.KeyHitRight2 = _keyHitRight2.ToString();
                _settings.KeyExitGame = _keyExitGame.ToString();

                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(_settings, options);
                File.WriteAllText(SettingsFileName, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MainGame] Error saving settings: {ex.Message}");
            }
        }

        private float GetRealStarRating(OsuBeatmap? bm) => bm != null ? StarRatingCache.GetRealStarRating(bm) : 0f;

        private Color GetDifficultyColor(OsuBeatmap? bm)
        {
            float sr = GetRealStarRating(bm);
            if (sr < 2.0f) return new Color(78, 186, 255);   // Easy — sky blue
            if (sr < 2.7f) return new Color(136, 224, 118);  // Normal — green
            if (sr < 4.0f) return new Color(255, 230, 118);  // Hard — yellow
            if (sr < 5.3f) return new Color(255, 118, 118);  // Insane — red
            if (sr < 6.5f) return new Color(200, 118, 255);  // Expert — purple
            return new Color(101, 99, 222);                   // Expert+ — dark indigo
        }

        private void UpdateWarningScreen(float dt)
        {
            // 1. Process sequential fade-in of words
            for (int i = 0; i <= _currentFadeWordIndex; i++)
            {
                if (i >= _allWords.Count) break;

                WordController word = _allWords[i];
                UDim2 wordTargetPos = word.TargetPosition;
                UDim2 wordBasePos = wordTargetPos + UDim2.FromOffset(0f, 12f);
                if (word.Alpha < 1.0f)
                {
                    //Console.WriteLine($"Fading in word: '{word.TextNode.text}' | Alpha: {word.Alpha:F2} | AccumulatedTime: {word.AcummulatedTime:F2}");
                    word.AcummulatedTime += dt * 1.15f;
                    word.Alpha = ArtMathHelper.Lerp(0f, 1.0f, word.AcummulatedTime); // Smooth 400ms fade per word
                    word.TextNode.alpha = word.Alpha * _warningParentAlpha;
                    word.TextNode.position = UDim2.Lerp(wordBasePos, wordTargetPos, MathF.Min(1f, word.Alpha));
                }
            }

            // Trigger the next word once the current word has reached >= 0.15f alpha
            if (_currentFadeWordIndex < _allWords.Count)
            {
                var currentWord = _allWords[_currentFadeWordIndex];
                if (currentWord.Alpha >= 0.05f)
                {
                    _currentFadeWordIndex++;
                }
            }

            // 2. Lifecycle transitions after all words are fully faded in
            bool allFinished = _currentFadeWordIndex >= _allWords.Count && _allWords[_allWords.Count - 1].Alpha >= 0.99f;
            if (allFinished)
            {
                _warningDoneTimer += dt;
            }

            // Exit / Skip Trigger: completed reading time or active input (key/click)
            bool triggerExit = _warningDoneTimer >= 2.1f || (allFinished && (Mouse.LeftClicked() || Keyboard.IsKeyPressed(Keys.Space) || Keyboard.IsKeyPressed(Keys.Enter)));

            if (triggerExit)
            {
                // Smoothly fade out the entire warning parent
                _warningParentAlpha = MathF.Max(0f, _warningParentAlpha - (dt * 2.0f));
                _warningScreenFrame.alpha = _warningParentAlpha;

                // Sync all word alphas to parent fadeout
                foreach (var w in _allWords)
                {
                    w.TextNode.alpha = w.Alpha * _warningParentAlpha;
                }

                if (_warningParentAlpha <= 0f)
                {
                    // Cleanup from pool, set state, and play the welcome song!
                    Remove(_warningScreenFrame);
                    _inWarningScreen = false;

                    PlayMusic("welcome");
                    SetMusicVolume("welcome", _targetVolume);
                }
            }
        }
    }
}
