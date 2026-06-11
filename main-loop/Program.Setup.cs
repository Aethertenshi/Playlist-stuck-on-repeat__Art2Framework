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
            SetVSyncMode();

			// Ensure the playlists directory exists in the output folder
			if (!Directory.Exists(SongsPath))
			{
				Directory.CreateDirectory(SongsPath);
			}

			// Load persistent star ratings cache
			StarRatingCache.Load();

			// Load persistent game settings
			LoadSettings();

			ConfigureWindow(width: DefaultScreenWidth, height: DefaultScreenHeight, title: "Playlist Stuck on Repeat", fullscreen: _settings.Fullscreen);

            LoadSFX("normal", "sounds/hitsounds/normal-hitnormal.wav");
			LoadSFX("whistle", "sounds/hitsounds/normal-hitwhistle.wav");
			LoadSFX("finish", "sounds/hitsounds/normal-hitfinish.wav");
			LoadSFX("clap", "sounds/hitsounds/normal-hitclap.wav");

			LoadSFX("beat", "sounds/sfxs/heartbeat.mp3");
			LoadSFX("hover", "sounds/sfxs/default-hover.wav");
			LoadSFX("select", "sounds/sfxs/default-select.wav");
			LoadSFX("keypress1", "sounds/sfxs/key-press-1.mp3");
			LoadSFX("keypress2", "sounds/sfxs/key-press-2.mp3");
			LoadSFX("keypress3", "sounds/sfxs/key-press-3.mp3");
			LoadSFX("keypress4", "sounds/sfxs/key-press-4.mp3");
			LoadSFX("keydel", "sounds/sfxs/key-delete.mp3");
			LoadSFX("play-click", "sounds/sfxs/menu-play-click.wav");

			// Apply loaded SFX volumes
			SetSFXVolume("normal", _effectsVolume);
			SetSFXVolume("whistle", _effectsVolume);
			SetSFXVolume("finish", _effectsVolume);
			SetSFXVolume("clap", _effectsVolume);
			SetSFXVolume("play-click", _effectsVolume);

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
				HitKeys = new Keys[] { _keyHitLeft1, _keyHitLeft2, _keyHitRight1, _keyHitRight2 },
				alpha = 0f // Start hidden
			};

			_stackfield = new StackPlayfield(LoadImage("circle", "content/hitcircle.png"), "gsans_bold")
			{
				size = new UDim2(1f, 0, 1f, 0),
				position = new UDim2(0.5f, 0.5f),
				anchorX = AnchorX.Center,
				anchorY = AnchorY.Center,
				ScrollSpeed = _settings.ScrollSpeed,
				GlobalScale = _settings.GlobalScale,
				ExitKey = _keyExitGameplay,
				HitKeys = new Keys[] { _keyHitLeft1, _keyHitLeft2, _keyHitRight1, _keyHitRight2 },
				alpha = 0f // Start hidden
			};

			Action<int> playHitSound = (hitSoundMask) =>
			{
				PlaySFX("beat");
				if ((hitSoundMask & 2) > 0) PlaySFX("whistle");
				if ((hitSoundMask & 4) > 0) PlaySFX("finish");
				if ((hitSoundMask & 8) > 0) PlaySFX("clap");
			};

			_taikofield.OnPlayHitSound = playHitSound;
			_stackfield.OnPlayHitSound = playHitSound;

			Action exitGameplay = () =>
			{
				_taikofield.ResetState();
				_stackfield.ResetState();
				SetMusicLowPass(_currentAudioKey, false); // Make sure LowPass filter is off when returning to menu!

				// 2. Audio Transition: Jump back to menu preview and restore volume!
				if (_audioTweeners.ContainsKey(_currentAudioKey))
					_audioTweeners[_currentAudioKey].Restart(1.6f, _targetVolume, Easing.Exponential, Direction.Out);

				// 3. Tell the cinematic timeline to run backward
				_isStarting = false;
				_startPhase = 0;

				SetPerformanceMode(_settings.MenuFps);
				Engine.HighPrecisionLimiter.SetMaxFps(_settings.MenuFps);

				_startShrinkTweener.Restart(1f, 0f, Easing.Exponential, Direction.Out);
				_startTransitionTweener.Restart(1.5f, 0f, Easing.Exponential, Direction.Out);

				if (_beatmap != null)
				{
					// Evict all gameplay hit-objects from memory
					_beatmap = _parser.Parse(_beatmap.FilePath, metadataOnly: true);
				}
				_taikofield.LoadBeatmap(null);
				_stackfield.LoadBeatmap(null);

				if (_bgVideoFrame != null)
				{
					_bgVideoFrame.Stop();
					_bgVideoFrame.skipDraw = true;
				}
				_bgDrop.alpha = 1f;
			};

			_taikofield.OnExit = exitGameplay;
			_stackfield.OnExit = exitGameplay;

			// Bind the update loop directly to the Frame components
			_taikofield.onUpdate = (e, dt) =>
			{
				// ONLY run physics and hit detection if we are actively in the game scene and mode matches
				if (_activeGameplayMode == GameplayMode.Taiko && _startPhase == 3 && _rythmIndexer != null)
				{
					_taikofield.MusicSpeedMultiplier = _actualMusicSpeed;
					_taikofield.UpdatePlayfield(dt, _rythmIndexer.CurrentProgress);
				}
			};

			_stackfield.onUpdate = (e, dt) =>
			{
				if (_activeGameplayMode == GameplayMode.Stack && _startPhase == 3 && _rythmIndexer != null)
				{
					_stackfield.MusicSpeedMultiplier = _actualMusicSpeed;
					_stackfield.UpdatePlayfield(dt, _rythmIndexer.CurrentProgress);
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
			_bgDrop = bgDrop;

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
						float t_shrink = _startShrinkTweener.CurrentValue;

						// 1. Sizing: Shrink cover to 0.5f (Taiko) or 0.7f (Stack)
						float targetScale = (_activeGameplayMode == GameplayMode.Taiko) ? 0.5f : 0.7f;
						float currentTarget = ArtMathHelper.Lerp(0.8f, targetScale, t_shrink);
						e.size = UDim2.Lerp(UDim2.FromScale(1f, 1f), UDim2.FromScale(currentTarget, currentTarget), _bgTweener.CurrentValue);

						// 2. Position: Shift cover to left boundary (X=0.0f) and slide up off-screen (Taiko) or stay centered (Stack)
						float targetY = (_activeGameplayMode == GameplayMode.Taiko) ? ArtMathHelper.Lerp(0.5f, -0.5f, t_shrink) : 0.5f;
						float targetX = ArtMathHelper.Lerp(0f, 0.5f, _startTransitionTweener.CurrentValue);
						e.position = UDim2.Lerp(UDim2.FromScale(0.5f, 0.5f), UDim2.FromScale(targetX, targetY), _bgTweener.CurrentValue);

						// 3. Alpha: Fade out completely (Taiko) or fade to 0.5f (Stack)
						float targetAlpha = (_activeGameplayMode == GameplayMode.Taiko) ? (1f - t_shrink) : ArtMathHelper.Lerp(1f, 0.1f, t_shrink);
						e.alpha = targetAlpha;

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

					float targetAlpha = (_activeGameplayMode == GameplayMode.Taiko) ? (1f - _startShrinkTweener.CurrentValue) : ArtMathHelper.Lerp(1f, 0.25f, _startShrinkTweener.CurrentValue);
					e.alpha = targetAlpha;
					// e.alpha = (_activeGameplayMode == GameplayMode.Taiko) ? (1f - _startShrinkTweener.CurrentValue) : 1f;

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

						// Calculate dynamic size matching the cover shrink factor
						e.size = new UDim2(0.4f, 0.3f) * MathF.Max(_logoTweener.CurrentValue, _startTransitionTweener.CurrentValue) * MathF.Max(1f - _bgTweener.CurrentValue, 0.35f);
						e.rotation = (_logoRotation.CurrentValue * (1f - _bgTweener.CurrentValue));

						// Position: Logo target X is at 0.2f (3/4 of the cover width, i.e., -0.4f + 0.8f * 0.75f = 0.2f) when cover view is active
						float currentTargetX = ArtMathHelper.Lerp(0.2f, 0.5f, _startTransitionTweener.CurrentValue);

						float t_shrink = _startShrinkTweener.CurrentValue;

						e.alpha = (1f - t_shrink) * (1f - _peekBg) * _introAlpha;
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
				position = new UDim2(0.5f, 0f, 15f, 15f), // 15px inset from the cover's top-left
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
				position = new UDim2(0.5f, 1f, 15f, -15f), // 15px inset from the cover's bottom-left
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

			_playerControlFrame = new Frame
			{
				anchorX = AnchorX.Left,
				anchorY = AnchorY.Top,
				color = new Color(0, 0, 0, 0), // Transparent container
				onUpdate = (e, dt) =>
				{
					float t_shrink = _startShrinkTweener.CurrentValue;

					// Size: 100px high horizontal bar on menu -> full screen on gameplay start
					e.size = UDim2.Lerp(new UDim2(1f, 0f, 0f, 100f), new UDim2(1f, 1f, 0f, 0f), t_shrink);

					// Position: slides up from bottom off-screen to bottom on-screen based on _bgTweener.
					// Then slides to top-left (0,0) in gameplay.
					UDim2 menuPos = UDim2.Lerp(new UDim2(0f, 1f, 0f, 0f), new UDim2(0f, 1f, 0f, -100f), _bgTweener.CurrentValue);
					e.position = UDim2.Lerp(menuPos, new UDim2(0f, 0f, 0f, 0f), t_shrink);
				}
			};

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

					float t_shrink = _startShrinkTweener.CurrentValue;

					UDim2 normalPos = new UDim2(0f, 0f, 20f, 15f);
					UDim2 gameplayPos = new UDim2(0f, 0.22f, 100f * _settings.GlobalScale, 0f);
					e.position = UDim2.Lerp(normalPos, gameplayPos, t_shrink);

					e.scale = ArtMathHelper.Lerp(2.4f, 2.7f, t_shrink);
					e.alpha = _bgTweener.CurrentValue
							* (1f - _settingsTweener.CurrentValue * 0.4f)
							* (1f - _startShrinkTweener.CurrentValue * 0.3f);
				}
			};
			_playerControlFrame.children.Add(songTitle);

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

					float t_shrink = _startShrinkTweener.CurrentValue;

					UDim2 normalPos = new UDim2(0f, 0f, 20f, 50f);
					UDim2 gameplayPos = new UDim2(0f, 0.22f, 100f * _settings.GlobalScale, 55f * _settings.GlobalScale);
					e.position = UDim2.Lerp(normalPos, gameplayPos, t_shrink);

					e.scale = ArtMathHelper.Lerp(1.8f, 1.95f, t_shrink);
					e.alpha = _bgTweener.CurrentValue
							* (1f - _settingsTweener.CurrentValue * 0.4f)
							* (1f - _startShrinkTweener.CurrentValue * 0.3f);
				}
			};
			_playerControlFrame.children.Add(songArtist);

			// 3. Progress Bar Track
			Button progressBarTrack = new Button
			{
				anchorX = AnchorX.Center,
				anchorY = AnchorY.Top,
				color = new Color(80, 80, 80),
				hoverColor = new Color(120, 120, 120),
				pressedColor = new Color(60, 60, 60),
				onUpdate = (btn) =>
				{
					float t_shrink = _startShrinkTweener.CurrentValue;

					UDim2 normalPos = new UDim2(0.5f, 0f, 0f, 35f);
					UDim2 targetPos = (_activeGameplayMode == GameplayMode.Taiko) ? new UDim2(0.5f, 0.5f, 0f, 0f) : new UDim2(0.5f, 1f, 0f, -65f);
					btn.position = UDim2.Lerp(normalPos, targetPos, t_shrink);

					float trackWidth = 600f;
					float targetWidth = (_activeGameplayMode == GameplayMode.Taiko) ? 1920f : trackWidth;
					float targetHeight = (_activeGameplayMode == GameplayMode.Taiko) ? 4f * _settings.GlobalScale : 6f;
					float w = ArtMathHelper.Lerp(trackWidth, targetWidth, t_shrink);
					float h = ArtMathHelper.Lerp(6f, targetHeight, t_shrink);
					btn.size = new UDim2(0f, 0f, w, h);

					btn.alpha = _bgTweener.CurrentValue * MathF.Max(0f, 1f - GetActivePlayfieldAlpha() * t_shrink);

					// Dragging seeking logic
					if (btn.IsPressed && !_isDraggingProgressBar && !_isStarting)
					{
						_isDraggingProgressBar = true;
					}

					if (_isDraggingProgressBar)
					{
						if (Mouse.LeftDown())
						{
							float centerX = btn.position.ScaleX * ScreenWidth + btn.position.OffsetX;
							float leftX = centerX - w * 0.5f;
							_dragProgress = Math.Clamp((Mouse.Position.X - leftX) / w, 0f, 1f);
						}
						else
						{
							_isDraggingProgressBar = false;
							float totalLength = GetMusicLength(_currentAudioKey);
							SeekMusic(_currentAudioKey, _dragProgress * totalLength);
						}
					}
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
					float progress = 0f;
					if (_isDraggingProgressBar)
					{
						progress = _dragProgress;
					}
					else
					{
						float timePlayed = GetMusicTimePlayed(_currentAudioKey);
						float totalLength = GetMusicLength(_currentAudioKey);
						progress = totalLength > 0 ? timePlayed / totalLength : 0f;
					}

					e.size = new UDim2(Math.Clamp(progress, 0f, 1f), 1f, 0f, 0f);

					float t_shrink = _startShrinkTweener.CurrentValue;
					e.alpha = _bgTweener.CurrentValue * MathF.Max(0f, 1f - GetActivePlayfieldAlpha() * t_shrink) * (1f - t_shrink);
				}
			};
			progressBarTrack.children.Add(progressBarFill);

			// 5. Progress Bar Dot / Handle
			ArtObject progressBarDot = new CircleFrame
			{
				// Anchor from the center of the dot so it sits perfectly over the end of the line
				anchorX = AnchorX.Center,
				anchorY = AnchorY.Center,
				color = Color.White,
				onUpdate = (e, dt) =>
				{
					float progress = 0f;
					if (_isDraggingProgressBar)
					{
						progress = _dragProgress;
					}
					else
					{
						float timePlayed = GetMusicTimePlayed(_currentAudioKey);
						float totalLength = GetMusicLength(_currentAudioKey);
						progress = totalLength > 0 ? Math.Clamp(timePlayed / totalLength, 0f, 1f) : 0f;
					}

					UDim2 normalPos = new UDim2(progress, 0.5f, 0.5f, 0f);
					float t_shrink = _startShrinkTweener.CurrentValue;

					UDim2 targetPos = (_activeGameplayMode == GameplayMode.Taiko) ? new UDim2(0f, 0.5f, 300f * _settings.GlobalScale, 0f) : normalPos;
					e.position = UDim2.Lerp(normalPos, targetPos, t_shrink);

					float normalSize = 20f;
					float targetSize = (_activeGameplayMode == GameplayMode.Taiko) ? 40f * _settings.GlobalScale : normalSize;
					float currentSize = ArtMathHelper.Lerp(normalSize, targetSize, t_shrink);
					e.size = new UDim2(0f, 0f, currentSize, currentSize);

					e.alpha = _bgTweener.CurrentValue * MathF.Max(0f, 1f - GetActivePlayfieldAlpha() * t_shrink);
				}
			};
			progressBarTrack.children.Add(progressBarDot);
			_playerControlFrame.children.Add(progressBarTrack);

			// 6. Time Played Text
			ArtObject timePlayed = new TextFrame
			{
				fontName = "gsans",
				anchorX = AnchorX.Right, // Grow outward to the left, away from the progress bar
				anchorY = AnchorY.Top,
				textAnchorX = AnchorX.Right,
				textAnchorY = AnchorY.Top,
				scale = 1.35f,
				onUpdate = (e, dt) =>
				{
					e.color = new Color((byte)(_currentCoverColor.R * 0.4f), (byte)(_currentCoverColor.G * 0.4f), (byte)(_currentCoverColor.B * 0.4f));

					float t_shrink = _startShrinkTweener.CurrentValue;
					float w = ArtMathHelper.Lerp(600f, 1920f, t_shrink);
					UDim2 normalPos = new UDim2(0.5f, 0f, -w * 0.5f - 15f, 35f);
					e.position = UDim2.Lerp(normalPos, new UDim2(normalPos.ScaleX, normalPos.ScaleY, normalPos.OffsetX, normalPos.OffsetY - 600f), t_shrink);
					e.alpha = _bgTweener.CurrentValue * (1f - t_shrink);

					float time = GetMusicTimePlayed(_currentAudioKey);
					e.text = $"{(int)(time / 60)}:{(int)(time % 60):D2}";
				}
			};
			_playerControlFrame.children.Add(timePlayed);

			// 7. Time Remaining Text
			ArtObject timeRemaining = new TextFrame
			{
				fontName = "gsans",
				anchorX = AnchorX.Left, // Grow outward to the right, away from the progress bar
				anchorY = AnchorY.Top,
				textAnchorX = AnchorX.Left,
				textAnchorY = AnchorY.Top,
				scale = 1.35f,
				onUpdate = (e, dt) =>
				{
					e.color = new Color((byte)(_currentCoverColor.R * 0.4f), (byte)(_currentCoverColor.G * 0.4f), (byte)(_currentCoverColor.B * 0.4f));

					float t_shrink = _startShrinkTweener.CurrentValue;
					float w = ArtMathHelper.Lerp(600f, 1920f, t_shrink);
					UDim2 normalPos = new UDim2(0.5f, 0f, w * 0.5f + 15f, 35f);
					e.position = UDim2.Lerp(normalPos, new UDim2(normalPos.ScaleX, normalPos.ScaleY, normalPos.OffsetX, normalPos.OffsetY - 600f), t_shrink);
					e.alpha = _bgTweener.CurrentValue * (1f - t_shrink);

					float timePlayed = GetMusicTimePlayed(_currentAudioKey);
					float totalLength = GetMusicLength(_currentAudioKey);
					float left = MathF.Max(0f, totalLength - timePlayed);
					e.text = $"-{(int)(left / 60)}:{(int)(left % 60):D2}";
				}
			};
			_playerControlFrame.children.Add(timeRemaining);

			// === Playlist Scroll ===
			ScrollingFrame playlistScroll = new ScrollingFrame
			{
				anchorX = AnchorX.Right,
				anchorY = AnchorY.Top,
				size = new UDim2(0.5f, 1f, 0f, -200f), // shifted down by 40px
				scrollDirection = Axis.Vertical,
				showScrollbar = false,
				scrollbarColor = new Color(255, 255, 255, 100),
				smoothing = 8f,
				scrollSensitivity = 68f,
				clipMode = ClipMode.Clip,
				alpha = 0f,
				onUpdate = (e, dt) =>
				{
					e.position = UDim2.Lerp(new UDim2(1.5f, 0f, 0f, 100f), new UDim2(1f, 0f, 0f, 100f), _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue));
					// Disable scissor clipping when fully off-screen to prevent MonoGame viewport overlap warnings
					e.clipMode = (e.position.ScaleX >= 1.49f) ? ClipMode.None : ClipMode.Clip;
				}
			};
			playlistScroll.modifiers.Add(new ArtFrame.UIModifier.UIListLayout
			{
				direction = ArtFrame.UIModifier.Axis.Vertical,
				spacing = 10f,
				paddingY = 10f
			});

			_playlistScroll = playlistScroll;
			_starRating = GetRealStarRating(_beatmap);

			// === Stationary Playlist Header ===
			Frame playlistHeader = new Frame
			{
				anchorX = AnchorX.Right,
				anchorY = AnchorY.Top,
				size = new UDim2(0.5f, 0f, -40f, 35f),
				color = new Color(0, 0, 0, 0), // transparent
				onUpdate = (e, dt) =>
				{
					e.position = UDim2.Lerp(new UDim2(1.5f, 0f, 0f, 60f), new UDim2(1f, 0f, 0f, 60f), _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue));
				}
			};

			playlistHeader.children.Add(new TextFrame
			{
				text = "#",
				fontName = "gsans_bold",
				position = new UDim2(0f, 0.5f, 15f, 0f),
				anchorX = AnchorX.Left,
				anchorY = AnchorY.Center,
				textAnchorX = AnchorX.Center,
				textAnchorY = AnchorY.Center,
				scale = 1.0f,
				color = Color.White
			});

			playlistHeader.children.Add(new TextFrame
			{
				text = "Title / Song",
				fontName = "gsans_bold",
				position = new UDim2(0f, 0.5f, 120f, 0f),
				anchorX = AnchorX.Left,
				anchorY = AnchorY.Center,
				textAnchorX = AnchorX.Left,
				textAnchorY = AnchorY.Center,
				scale = 1.0f,
				color = Color.White
			});

			playlistHeader.children.Add(new Frame
			{
				position = new UDim2(0f, 1f, 0f, 0f),
				size = new UDim2(1f, 0f, 0f, 1f), // 1px separator line
				anchorX = AnchorX.Left,
				anchorY = AnchorY.Bottom,
				color = new Color(255, 255, 255, 40)
			});

			// === Initialize Video Frame ===
			_bgVideoFrame = new VideoFrame
			{
				size = new UDim2(1f, 1f),
				position = new UDim2(0.5f, 0.5f),
				anchorX = AnchorX.Center,
				anchorY = AnchorY.Center,
				fit = ObjectFit.Cover,
				skipDraw = true
			};

			// --- Drawing Index ---
			Add(bgDrop);
			Add(_bgVideoFrame);

			Add(_playerControlFrame);
			Add(playlistHeader);
			Add(playlistScroll);

			Add(_blurBgUI);
			Add(_welcomeTransition); // Renders over the background but behind the logo
			Add(_shockwaveHolder);

			Add(_logoUI);

			Add(BuildSettingsUI());
			Add(BuildModifiersUI());
			Add(BuildAccountUI());
			Add(BuildTopbarUI());

			// Initialize and add the WIP Warning Screen on top of everything!
			if (_showWarningScreen)
			{
				SetupWarningScreen();
			}
			else
			{
				_inWarningScreen = false;
			}

			Add(_taikofield);
			Add(_stackfield);

			_resultscreen = new ResultScreen
			{
				OnRetry = RestartGameplay,
				OnQuit = () =>
				{
					_inResultScreen = false;
					exitGameplay();
				}
			};
			Add(_resultscreen);

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
						PlaySFX("beat");
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
			if (!_showWarningScreen)
			{
				PlayMusic("welcome");
			}
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
