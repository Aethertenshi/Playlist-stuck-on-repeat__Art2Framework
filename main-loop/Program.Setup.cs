using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.FileProcessing;
using ArtFrame.UIModifier;
using ArtFrame.UserInterface;

using static ArtFrame.AudioHelper;
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
			// Start in smooth VSync mode for warning screen and logo intro!

			// Ensure the playlists directory exists in the output folder
			if (!Directory.Exists(SongsPath))
			{
				Directory.CreateDirectory(SongsPath);
			}

			// Load persistent star ratings cache
			StarRatingCache.Load();

			// Load persistent game settings
			LoadSettings();

			ConfigureWindow(width: 1920, height: 1080, title: "Playlist Stuck on Repeat", fullscreen: _settings.Fullscreen);
            SetVSyncMode();

            LoadSFX("normal", "sounds/hitsounds/soft-hitnormal.wav");
			LoadSFX("whistle", "sounds/hitsounds/soft-hitwhistle.wav");
			LoadSFX("finish", "sounds/hitsounds/soft-hitfinish.wav");
			LoadSFX("clap", "sounds/hitsounds/soft-hitclap.wav");

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
				ScrollSpeed = _settings.ScrollSpeed,
				GlobalScale = _settings.GlobalScale,
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
				//if (_taikofield.Score > 0 || _taikofield.HitsPerfect > 0 || _taikofield.HitsGood > 0 || _taikofield.HitsOk > 0 || _taikofield.HitsMiss > 0)
				//{
				//	string activeMods = "";
				//	if (_modHidden) activeMods += "HD ";
				//	if (Math.Abs(_speedMultiplier - 1f) > 0.01f)
				//	{
				//		activeMods += _speedMultiplier > 1f ? "DT " : "HT ";
				//	}
				//	if (_adjustPitch) activeMods += "NC ";
				//	activeMods = string.IsNullOrWhiteSpace(activeMods) ? "NM" : activeMods.TrimEnd();

				//	ScoreManager.AddScore(
				//		_beatmap?.FilePath ?? "", 
				//		"You", 
				//		_taikofield.Score, 
				//		_taikofield.GetAccuracy(), 
				//		_taikofield.MaxComboReached, 
				//		activeMods
				//	);
					
				//	RefreshScoreboard(_scoreboardPanel);
				//}

				// 1. Wipe the playfield clean and hide it
				_taikofield.ResetState();
				SetMusicLowPass(_currentAudioKey, false); // Make sure LowPass filter is off when returning to menu!

				// 2. Audio Transition: Jump back to menu preview and restore volume!
				//SeekMusic(_currentAudioKey, _beatmap?.PreviewTime / 1000f ?? 0f);
				if (_audioTweeners.ContainsKey(_currentAudioKey))
					_audioTweeners[_currentAudioKey].Restart(1.6f, _targetVolume, Easing.Exponential, Direction.Out);

				// 3. Tell the cinematic timeline to run backward
				_isStarting = false;
				_startPhase = 0;

				SetPerformanceMode(_settings.MenuFps);
				Engine.HighPrecisionLimiter.SetMaxFps(_settings.MenuFps);

				//_rythmIndexer = new RhythmIndexer(new InterpolatingAudioClock(), new RhythmTracker(), () => GetMusicTimePlayed(_currentAudioKey)) { Beatmap = _beatmap, MusicOffset = -55.35f };
				_startShrinkTweener.Restart(1f, 0f, Easing.Exponential, Direction.Out);
				_startTransitionTweener.Restart(1.5f, 0f, Easing.Exponential, Direction.Out);

                if (_beatmap != null)
                {
                    // Evict all gameplay hit-objects from memory
                    _beatmap = _parser.Parse(_beatmap.FilePath, metadataOnly: true);
                }
                _taikofield.LoadBeatmap(null);
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
			var scannedBeatmaps = _scanner.ScanLazy(SongsPath, metadataOnly: true).ToList();
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

					// 2. Lerp to a solid black background when starting
					e.color = Color.LerpColor(menuColor, new Color((byte)(_currentCoverColor.R * 0.35f), (byte)(_currentCoverColor.G * 0.35f), (byte)(_currentCoverColor.B * 0.35f)), _startTransitionTweener.CurrentValue);
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

						float t_shrink = _startShrinkTweener.CurrentValue;
						float finalY = ArtMathHelper.Lerp(targetY, -0.6f, t_shrink);

						e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(targetX, finalY), _bgTweener.CurrentValue);
						e.alpha = 1f - t_shrink;

						_blur.BlurAmount = ArtMathHelper.Lerp(0f, 3.7f, 1f - MathF.Max(_bgTweener.CurrentValue, _peekBg));
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
					// 3. Smoothly interpolate Color (Dark gray -> Dynamic Cover Color)
					e.color = Color.LerpColor(new Color(200, 200, 200), Color.White, _bgTweener.CurrentValue);

					e.alpha = 1f - _startShrinkTweener.CurrentValue;

					// Input Polling
					if ((Keyboard.IsKeyPressed(_keyToggleCover) || (Mouse.LeftClicked() && !_isCoverView)) && !_isStarting && !_inIntro && !_isListeningForKey && !Mouse.RightDown())
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
						e.rotation = _logoRotation.CurrentValue;

						if (_transitionFired)
						{
							// 1. Smooth Breath Swell: gently swells slightly larger (from 0.40f to 0.42f) and floats back smoothly
							float swell = 0.025f * MathF.Exp(-_introTransitionTimer * 3.5f) * MathF.Sin(_introTransitionTimer * MathF.PI);
							float sizeVal = 0.4f + swell;
							e.size = new UDim2(sizeVal, sizeVal);
						}
						else
						{
							// 2. Liquid Smooth Scale Swell: gently scales up as it fades in on startup
							float sizeVal = ArtMathHelper.Lerp(0.35f, 0.40f, _introAlpha);
							e.size = new UDim2(sizeVal, sizeVal);
						}

						e.position = UDim2.FromScale(0.5f, 0.5f);
						e.color = Color.White;
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

						float t_shrink = _startShrinkTweener.CurrentValue;
						float finalY = ArtMathHelper.Lerp(targetY, -0.6f, t_shrink);

						e.alpha = (1f - t_shrink) * (1f - _peekBg) * _introAlpha;
						e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(targetX, finalY), _bgTweener.CurrentValue);
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

					if (_modHidden) activeMods += "HD | ";

					// Check if speed has been altered
					if (Math.Abs(_speedMultiplier - 1f) > 0.01f)
					{
						activeMods += _speedMultiplier > 1f ? "DT " : "HT ";
						activeMods += $"({_speedMultiplier:F2}x) | ";
					}

					if (_adjustPitch) activeMods += "NC"; // Nightcore/Pitch modifier

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

					float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
					float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
					float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);
					float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

					// L-swoop: X moves first (0..0.7), Y moves after (0.3..1.0) with overlap zone
					float rawTx = Math.Clamp(effectiveListenScore / 0.7f, 0f, 1f);
					float rawTy = Math.Clamp((effectiveListenScore - 0.3f) / 0.7f, 0f, 1f);
					float tx = 1f - MathF.Pow(1f - rawTx, 2f); // EaseOutQuad — fast rightward sweep
					float ty = rawTy * rawTy;                    // EaseInQuad  — slow then upward sweep

					// Scale components lerp with the vertical phase
					float scaleX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, ty);
					float scaleY = ArtMathHelper.Lerp(0.5f, 0.32f, ty);
					float offsetX = ArtMathHelper.Lerp(-250f, 210f, tx);
					float offsetY = ArtMathHelper.Lerp(280f, -60f, ty);

					UDim2 fullScreenPos = new UDim2(0.38f, 0.5f, -250f, 320f);
					UDim2 coverViewPos  = new UDim2(scaleX, scaleY, offsetX, offsetY);
					UDim2 normalPos = UDim2.Lerp(fullScreenPos, coverViewPos, _bgTweener.CurrentValue);

					float t_shrink = _startShrinkTweener.CurrentValue;

					// Interpolate position smoothly to top-left area (ScaleX = 0f, ScaleY = 0.22f, OffsetX = 100f * GlobalScale) in Phase 2
					UDim2 gameplayPos = new UDim2(0f, 0.22f, 100f * _settings.GlobalScale, 0f);
					e.position = UDim2.Lerp(normalPos, gameplayPos, t_shrink);

					// Interpolate scale from 2.4f (normal) to 2.7f (slightly enlarged) in Phase 2
					e.scale = ArtMathHelper.Lerp(2.4f, 2.7f, t_shrink);

					// Title remains visible during gameplay
					e.alpha = _bgTweener.CurrentValue
							* (1f - _settingsTweener.CurrentValue * 0.4f)
							* (1f - _startShrinkTweener.CurrentValue * 0.3f);
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

					float rawTx = Math.Clamp(effectiveListenScore / 0.7f, 0f, 1f);
					float rawTy = Math.Clamp((effectiveListenScore - 0.3f) / 0.7f, 0f, 1f);
					float tx = 1f - MathF.Pow(1f - rawTx, 2f);
					float ty = rawTy * rawTy;

					float scaleX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, ty);
					float scaleY = ArtMathHelper.Lerp(0.5f, 0.32f, ty);
					float offsetX = ArtMathHelper.Lerp(-250f, 210f, tx);
					float offsetY = ArtMathHelper.Lerp(325f, -20f, ty);

					UDim2 fullScreenPos = new UDim2(0.38f, 0.5f, -250f, 350f);
					UDim2 coverViewPos  = new UDim2(scaleX, scaleY, offsetX, offsetY);
					UDim2 normalPos = UDim2.Lerp(fullScreenPos, coverViewPos, _bgTweener.CurrentValue);

					float t_shrink = _startShrinkTweener.CurrentValue;

					// Interpolate position smoothly to sit right below the title in the top-left area in Phase 2
					UDim2 gameplayPos = new UDim2(0f, 0.22f, 100f * _settings.GlobalScale, 55f * _settings.GlobalScale);
					e.position = UDim2.Lerp(normalPos, gameplayPos, t_shrink);

					// Interpolate scale from 1.8f (normal) to 1.95f (slightly enlarged) in Phase 2
					e.scale = ArtMathHelper.Lerp(1.8f, 1.95f, t_shrink);

					// Artist remains visible during gameplay
					e.alpha = _bgTweener.CurrentValue
							* (1f - _settingsTweener.CurrentValue * 0.4f)
							* (1f - _startShrinkTweener.CurrentValue * 0.3f);
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
					float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
					float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
					float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);
					float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

					float rawTx = Math.Clamp(effectiveListenScore / 0.7f, 0f, 1f);
					float rawTy = Math.Clamp((effectiveListenScore - 0.3f) / 0.7f, 0f, 1f);
					float tx = 1f - MathF.Pow(1f - rawTx, 2f);
					float ty = rawTy * rawTy;

					// anchorX = Center, so offsetX=0 means centered on the scale point
					float scaleX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, ty);
					float scaleY = ArtMathHelper.Lerp(0.5f, 0.32f, ty);
					float offsetX = ArtMathHelper.Lerp(0f, 415f, tx);
					float offsetY = ArtMathHelper.Lerp(390f, 40f, ty);

					UDim2 fullScreenPos = new UDim2(0.38f, 0.5f, 0f, 410f);
					UDim2 coverViewPos  = new UDim2(scaleX, scaleY, offsetX, offsetY);
					UDim2 normalPos = UDim2.Lerp(fullScreenPos, coverViewPos, _bgTweener.CurrentValue);

					float trackWidth = ArtMathHelper.Lerp(500f * _bgTweener.CurrentValue, 410f * _bgTweener.CurrentValue, effectiveListenScore);

					float t_shrink = _startShrinkTweener.CurrentValue;

					// Position: Smoothly center during Phase 1 (via normalPos), then slide up to vertical center in Phase 2
					e.position = UDim2.Lerp(normalPos, new UDim2(0.5f, 0.5f, 0f, 0f), t_shrink);

					// Size: Keep normal menu size during Phase 1, then expand to full screen width in Phase 2
					float targetWidth = 1920f;
					float targetHeight = 4f * _settings.GlobalScale;
					float w = ArtMathHelper.Lerp(trackWidth, targetWidth, t_shrink);
					float h = ArtMathHelper.Lerp(6f, targetHeight, t_shrink);
					e.size = new UDim2(0f, 0f, w, h);

					// Smooth bidirectional fade using playfield alpha and t_shrink
					e.alpha = _bgTweener.CurrentValue * MathF.Max(0f, 1f - _taikofield.alpha * t_shrink);
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
					float timePlayed = GetMusicTimePlayed(_currentAudioKey);
					float totalLength = GetMusicLength(_currentAudioKey);
					float progress = totalLength > 0 ? timePlayed / totalLength : 0f;

					e.size = new UDim2(Math.Clamp(progress, 0f, 1f), 1f, 0f, 0f);

					float t_shrink = _startShrinkTweener.CurrentValue;
					e.alpha = _bgTweener.CurrentValue * MathF.Max(0f, 1f - _taikofield.alpha * t_shrink) * (1f - t_shrink);
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
					// 1. Calculate the active progress ratio
					float timePlayed = GetMusicTimePlayed(_currentAudioKey);
					float totalLength = GetMusicLength(_currentAudioKey);
					float progress = totalLength > 0 ? Math.Clamp(timePlayed / totalLength, 0f, 1f) : 0f;

					UDim2 normalPos = new UDim2(progress, 0.5f, 0.5f, 0f);
					float t_shrink = _startShrinkTweener.CurrentValue;

					UDim2 targetPos = new UDim2(0f, 0.5f, 300f * _settings.GlobalScale, 0f);
					e.position = UDim2.Lerp(normalPos, targetPos, t_shrink);

					float normalSize = 20f;
					float targetSize = 40f * _settings.GlobalScale;
					float currentSize = ArtMathHelper.Lerp(normalSize, targetSize, t_shrink);
					e.size = new UDim2(0f, 0f, currentSize, currentSize);

					e.alpha = _bgTweener.CurrentValue * MathF.Max(0f, 1f - _taikofield.alpha * t_shrink);
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

					float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
					float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
					float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);
					float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

					float rawTx = Math.Clamp(effectiveListenScore / 0.7f, 0f, 1f);
					float rawTy = Math.Clamp((effectiveListenScore - 0.3f) / 0.7f, 0f, 1f);
					float tx = 1f - MathF.Pow(1f - rawTx, 2f);
					float ty = rawTy * rawTy;

					float scaleX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, ty);
					float scaleY = ArtMathHelper.Lerp(0.5f, 0.32f, ty);
					float offsetX = ArtMathHelper.Lerp(-250f, 210f, tx);
					float offsetY = ArtMathHelper.Lerp(405f, 60f, ty);

					UDim2 fullScreenPos = new UDim2(0.38f, 0.5f, -250f, 425f);
					UDim2 coverViewPos  = new UDim2(scaleX, scaleY, offsetX, offsetY);
					UDim2 normalPos = UDim2.Lerp(fullScreenPos, coverViewPos, _bgTweener.CurrentValue);

					float t_shrink = _startShrinkTweener.CurrentValue;
					e.position = UDim2.Lerp(normalPos, new UDim2(normalPos.ScaleX, normalPos.ScaleY, normalPos.OffsetX, normalPos.OffsetY - 600f), t_shrink);
					e.alpha = _bgTweener.CurrentValue * (1f - t_shrink);

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

					float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
					float baseTargetX = ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue);
					float currentTargetX = ArtMathHelper.Lerp(baseTargetX, 0.5f, _startTransitionTweener.CurrentValue);
					float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

					float rawTx = Math.Clamp(effectiveListenScore / 0.7f, 0f, 1f);
					float rawTy = Math.Clamp((effectiveListenScore - 0.3f) / 0.7f, 0f, 1f);
					float tx = 1f - MathF.Pow(1f - rawTx, 2f);
					float ty = rawTy * rawTy;

					float scaleX = ArtMathHelper.Lerp(currentTargetX, currentTargetX - 0.08f, ty);
					float scaleY = ArtMathHelper.Lerp(0.5f, 0.32f, ty);
					float offsetX = ArtMathHelper.Lerp(250f, 620f, tx);
					float offsetY = ArtMathHelper.Lerp(405f, 60f, ty);

					UDim2 fullScreenPos = new UDim2(0.38f, 0.5f, 250f, 425f);
					UDim2 coverViewPos  = new UDim2(scaleX, scaleY, offsetX, offsetY);
					UDim2 normalPos = UDim2.Lerp(fullScreenPos, coverViewPos, _bgTweener.CurrentValue);

					float t_shrink = _startShrinkTweener.CurrentValue;
					e.position = UDim2.Lerp(normalPos, new UDim2(normalPos.ScaleX, normalPos.ScaleY, normalPos.OffsetX, normalPos.OffsetY - 600f), t_shrink);
					e.alpha = _bgTweener.CurrentValue * (1f - t_shrink);

					float timePlayed = GetMusicTimePlayed(_currentAudioKey);
					float totalLength = GetMusicLength(_currentAudioKey);
					float left = MathF.Max(0f, totalLength - timePlayed);
					e.text = $"-{(int)(left / 60)}:{(int)(left % 60):D2}";
				}
			};

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
					// Disable scissor clipping when fully off-screen to prevent MonoGame viewport overlap warnings
					e.clipMode = (e.position.OffsetX >= 509f) ? ClipMode.None : ClipMode.Clip;
				}
			};

			_playlistScroll = playlistScroll;
			_starRating = GetRealStarRating(_beatmap);
			_scoreboardPanel = BuildScoreboardUI();

			// --- Drawing Index ---
			Add(bgDrop);

			Add(songTitle);
			Add(songArtist);
			Add(playlistScroll);
			Add(timeRemaining);
			Add(timePlayed);
			Add(progressBarTrack);

			Add(_blurBgUI);
			Add(_welcomeTransition); // Renders over the background but behind the logo
			Add(_shockwaveHolder);

			Add(_logoUI);

			Add(BuildSettingsUI());
			Add(BuildModifiersUI());
			Add(BuildTopbarUI());
			//Add(_scoreboardPanel);
			//RefreshScoreboard(_scoreboardPanel);

			// Initialize and add the WIP Warning Screen on top of everything!
			SetupWarningScreen();

			Add(_taikofield);

			// Populate Playlist
			RepopulatePlaylist();

			// Initialize Rhythm Indexer early so it's not null when added to helperPool
			_rythmIndexer = new RhythmIndexer(_audioClock, _rhythmTracker, () => GetMusicTimePlayed(_currentAudioKey), () => IsMusicPlaying(_currentAudioKey))
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
					_logoTweener.SetValue(.93f);
				else
					_logoTweener.SetValue(.975f);
				_logoTweener.Restart(1.4f, 1f, Easing.Fluid, Direction.Out);
			};

			AddHelper(_rythmIndexer);

			// Setup Welcome intro audio (we will play it after warning screen fades out!)
			// _inWarningScreen = false;
			LoadMusic("welcome", "sounds/sfxs/welcome.wav");
			// PlayMusic("welcome");
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
		private void SetupWarningScreen()
		{
			_warningScreenFrame = new Frame
			{
				size = new UDim2(1f, 1f),
				position = new UDim2(0.5f, 0.5f),
				anchorX = AnchorX.Center,
				anchorY = AnchorY.Center,
				color = Color.Black,
				alpha = 1.0f
			};

			// Define the 3 sentences to display
			string gameVer = "v2026.0530rc1";
			string s1 = "This project is work in progress";
			string s2 = "Expect bugs and something breaking. Contribute to this game through github!";
			string s3 = $"This game version is {gameVer}, read the changelog on github.";

			// We place them at different vertical offsets
			AddSentenceToWarning(s1, "gsans_bold", 2f, Color.White, -28f);
			AddSentenceToWarning(s2, "gsans", 1.6f, new Color(200, 200, 200), 0f);
			AddSentenceToWarning(s3, "gsans", 1.45f, new Color(170, 170, 170), 25f);

			// Add the parent warning screen frame to the pool so it draws over everything!
			Add(_warningScreenFrame);
			
		}

		private void AddSentenceToWarning(string sentence, string fontName, float scale, Color color, float yPixelOffset)
		{
			string[] blacklistwords = { "p","q","g" };
			string[] words = sentence.Split(' ');
			float totalWidth = MeasureText(fontName, sentence, scale * 10f).X;
			float spaceWidth = MeasureText(fontName, " ", scale * 10f).X;

			float currentX = (ScreenWidth - totalWidth) / 2f;

			foreach (var word in words)
			{
				bool ContainsBlacklistedChar = false;
				foreach (char c in word)
				{
					if (blacklistwords.Contains(c.ToString()))
						ContainsBlacklistedChar = true;
				}

				float wordWidth = MeasureText(fontName, word, scale * 10f).X;

				var textNode = new TextFrame
				{
					text = word,
					fontName = fontName,
					scale = scale,
					position = new UDim2(0f, 0.5f, currentX, ContainsBlacklistedChar? yPixelOffset : yPixelOffset - (scale * 2f)),
					anchorX = AnchorX.Left,
					anchorY = AnchorY.Bottom,
					textAnchorX = AnchorX.Left,
					textAnchorY = AnchorY.Bottom,
					color = color,
					alpha = 0f // Start completely hidden
				};

				_warningScreenFrame.children.Add(textNode);
				_allWords.Add(new WordController { TextNode = textNode, TargetPosition = textNode.position, Alpha = 0f });

				currentX += wordWidth + spaceWidth;
			}
		}
	}
}
