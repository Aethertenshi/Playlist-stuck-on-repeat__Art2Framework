using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UIModifier;
using ArtFrame.UserInterface;
using static ArtFrame.AudioHelper;
using static ArtFrame.GraphicsHelper;
using static ArtFrame.InputHelper;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
        public ScrollingFrame BuildSettingsUI()
        {
            ScrollingFrame settingsPanel = new ScrollingFrame
            {
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(0f, 1f, 500f, -60f), // Match the exact footprint of your song list
                scrollDirection = Axis.Vertical,
                showScrollbar = false,
                smoothing = 18f,
                clipMode = ClipMode.Clip,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    // Smoothly interpolate positions from tucked away (-510px) to resting at the left edge (0px)
                    e.position = UDim2.Lerp(new UDim2(0f, 0, -510f, 60f), new UDim2(0f, 0f, 0f, 60f), MathF.Min(_settingsTweener.CurrentValue, _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue)));
                    //e.alpha = _settingsTweener.CurrentValue;

                    // Disable scissor clipping when fully off-screen to prevent MonoGame viewport overlap warnings
                    //e.clipMode = (e.position.OffsetX >= -509f) ? ClipMode.None : ClipMode.Clip;
                }
            };

            string[] options = { "Volumes", "Audio Offset", "Key Bindings", "Graphics Config", "Gameplay Settings" };
            foreach (var optionName in options)
            {
                Frame optionRow = new Frame
                {
                    position = new UDim2(0f, 0f, 10f, _settingsYOffset),
                    size = new UDim2(.93f, 0f, 0f, 45f),
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

            return settingsPanel;
        }
        private void AddSettingsMenu(ScrollingFrame settingsPanel, string currentPage = "")
        {
            if (currentPage != "" && currentPage == "Volumes")
            {
                // --- Main Volume ---
                Frame mainVolumeFrame = new Frame
                {
                    position = new UDim2(0f, 0f, 10f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 55f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.7f);
                        byte g = (byte)(_currentCoverColor.G * 0.7f);
                        byte b = (byte)(_currentCoverColor.B * 0.7f);

                        e.color = new Color(r, g, b, 175);
                    },
                    children = new List<ArtObject>
                    {
                        new SliderFrame
                        {
                            fontName = "gsans_bold",
                            title = "Main Volume",
                            fontScale = 1.15f,
                            fillColor = new Color(230, 230, 230),
                            resetBtnColor = new Color("#FF6666"),
                            resetBtnHoverColor = Color.White,
                            size = new UDim2(.95f, 1f),
                            position = new UDim2(0.5f, 0.5f),
                            handleColor = Color.White,
                            handleWidth = 15f,
                            anchorX = AnchorX.Center,
                            anchorY = AnchorY.Center,
                            currentValue = _targetVolume,
                            trackColor = new Color(100, 100, 100, 75),
                            onValueChanges = (e) =>
                            {
                                _targetVolume = e.currentValue;
                                _audioTweeners[_currentAudioKey].Restart(0.7f, _targetVolume, Easing.Exponential, Direction.Out);
                                SaveSettings();
                            }
                        }
                    }
                };
                settingsPanel.children.Add(mainVolumeFrame);
                _settingsYOffset += 60f;

                // --- Effects Volume ---
                Frame effectFrame = new Frame
                {
                    position = new UDim2(0f, 0f, 10f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 55f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.7f);
                        byte g = (byte)(_currentCoverColor.G * 0.7f);
                        byte b = (byte)(_currentCoverColor.B * 0.7f);

                        // 3. Apply the colors dynamically
                        e.color = new Color(r, g, b, 175);
                    },
                    children = new List<ArtObject>
                    {
                        new SliderFrame
                        {
                            fontName = "gsans_bold",
                            title = "Effects Volume",
                            fontScale = 1.15f,
                            fillColor = new Color(230, 230, 230),
                            resetBtnColor = new Color("#FF6666"),
                            resetBtnHoverColor = Color.White,
                            size = new UDim2(.95f, 1f),
                            position = new UDim2(0.5f, 0.5f),
                            handleColor = Color.White,
                            handleWidth = 15f,
                            anchorX = AnchorX.Center,
                            anchorY = AnchorY.Center,
                            currentValue = _effectsVolume,
                            trackColor = new Color(100, 100, 100, 75),
                            onValueChanges = (e) =>
                            {
                                _effectsVolume = e.currentValue;
                                SetSFXVolume("play-click", _effectsVolume);
                                SetSFXVolume("hover", e.currentValue);
                                SetSFXVolume("select", e.currentValue);
                                SetSFXVolume("beat", e.currentValue);
                                SetSFXVolume("dwbeat", e.currentValue);

                                SetSFXVolume("normal", e.currentValue);
                                SetSFXVolume("whistle", e.currentValue);
                                SetSFXVolume("finish", e.currentValue);
                                SetSFXVolume("clap", e.currentValue);
                                SaveSettings();
                            }
                        }
                    }
                };
                settingsPanel.children.Add(effectFrame);
                _settingsYOffset += 60f;
            }
            
            if (currentPage != "" && currentPage == "Audio Offset")
            {
                // --- Audio Offset ---
                Frame audioOffsetFrame = new Frame
                {
                    position = new UDim2(0f, 0f, 10f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 55f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.7f);
                        byte g = (byte)(_currentCoverColor.G * 0.7f);
                        byte b = (byte)(_currentCoverColor.B * 0.7f);

                        // 3. Apply the colors dynamically
                        e.color = new Color(r, g, b, 175);
                    },
                    children = new List<ArtObject>
                    {
                        new SliderFrame
                        {
                            fontName = "gsans_bold",
                            title = "Audio Offset",
                            fontScale = 1.15f,
                            fillColor = new Color(230, 230, 230),
                            resetBtnColor = new Color("#FF6666"),
                            resetBtnHoverColor = Color.White,
                            size = new UDim2(.95f, 1f),
                            position = new UDim2(0.5f, 0.5f),
                            handleColor = Color.White,
                            handleWidth = 15f,
                            anchorX = AnchorX.Center,
                            anchorY = AnchorY.Center,
                            minValue = -80f,
                            maxValue = 80f,
                            defaultValue = 0,
                            currentValue = _audioOffset,
                            trackColor = new Color(100, 100, 100, 75),
                            onValueChanges = (e) =>
                            {
                                _audioOffset = e.currentValue;
                                if (_rythmIndexer != null)
                                {
                                    _rythmIndexer.MusicOffset = e.currentValue;
                                }
                                SaveSettings();
                            }
                        }
                    }
                };
                settingsPanel.children.Add(audioOffsetFrame);
                _settingsYOffset += 60f;
            }

            if (currentPage != "" && currentPage == "Key Bindings")
            {
                settingsPanel.children.Add(CreateKeybindRow("ToggleCover", "Menu Space Toggle", _settingsYOffset, () => _keyToggleCover, (val) => _keyToggleCover = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("StartGame", "Start/Play Song Key", _settingsYOffset, () => _keyStartGame, (val) => _keyStartGame = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("ExitGameplay", "Exit Song Key", _settingsYOffset, () => _keyExitGameplay, (val) => _keyExitGameplay = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("ExitGame", "Exit Game Key", _settingsYOffset, () => _keyExitGame, (val) => _keyExitGame = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("HitLeft1", "Left 1 Hit", _settingsYOffset, () => _keyHitLeft1, (val) => _keyHitLeft1 = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("HitLeft2", "Left 2 Hit", _settingsYOffset, () => _keyHitLeft2, (val) => _keyHitLeft2 = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("HitRight1", "Right 1 Hit", _settingsYOffset, () => _keyHitRight1, (val) => _keyHitRight1 = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("HitRight2", "Right 2 Hit", _settingsYOffset, () => _keyHitRight2, (val) => _keyHitRight2 = val));
                _settingsYOffset += 60f;

            }

            if (currentPage != "" && currentPage == "Gameplay Settings")
            {
                // --- Scroll Speed Slider ---
                Frame scrollSpeedFrame = new Frame
                {
                    position = new UDim2(0f, 0f, 10f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 55f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.7f);
                        byte g = (byte)(_currentCoverColor.G * 0.7f);
                        byte b = (byte)(_currentCoverColor.B * 0.7f);
                        e.color = new Color(r, g, b, 175);
                    },
                    children = new List<ArtObject>
                    {
                        new SliderFrame
                        {
                            fontName = "gsans_bold",
                            title = "Scroll Speed",
                            fontScale = 1.15f,
                            fillColor = new Color(230, 230, 230),
                            resetBtnColor = new Color("#FF6666"),
                            resetBtnHoverColor = Color.White,
                            size = new UDim2(.95f, 1f),
                            position = new UDim2(0.5f, 0.5f),
                            handleColor = Color.White,
                            handleWidth = 15f,
                            anchorX = AnchorX.Center,
                            anchorY = AnchorY.Center,
                            minValue = 0.1f,
                            maxValue = 3.0f,
                            defaultValue = 0.25f,
                            currentValue = _settings.ScrollSpeed,
                            trackColor = new Color(100, 100, 100, 75),
                            onValueChanges = (e) =>
                            {
                                _settings.ScrollSpeed = e.currentValue;
                                if (_taikofield != null)
                                {
                                    _taikofield.ScrollSpeed = e.currentValue;
                                }
                                if (_stackfield != null)
                                {
                                    _stackfield.ScrollSpeed = e.currentValue;
                                }
                                SaveSettings();
                            }
                        }
                    }
                };
                settingsPanel.children.Add(scrollSpeedFrame);
                _settingsYOffset += 60f;

                // --- Gameplay Scale Slider ---
                Frame globalScaleFrame = new Frame
                {
                    position = new UDim2(0f, 0f, 10f, _settingsYOffset),
                    size = new UDim2(.9f, 0f, 0f, 55f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onUpdate = (e, dt) =>
                    {
                        byte r = (byte)(_currentCoverColor.R * 0.7f);
                        byte g = (byte)(_currentCoverColor.G * 0.7f);
                        byte b = (byte)(_currentCoverColor.B * 0.7f);
                        e.color = new Color(r, g, b, 175);
                    },
                    children = new List<ArtObject>
                    {
                        new SliderFrame
                        {
                            fontName = "gsans_bold",
                            title = "Gameplay Scale",
                            fontScale = 1.15f,
                            fillColor = new Color(230, 230, 230),
                            resetBtnColor = new Color("#FF6666"),
                            resetBtnHoverColor = Color.White,
                            size = new UDim2(.95f, 1f),
                            position = new UDim2(0.5f, 0.5f),
                            handleColor = Color.White,
                            handleWidth = 15f,
                            anchorX = AnchorX.Center,
                            anchorY = AnchorY.Center,
                            minValue = 0.5f,
                            maxValue = 3.0f,
                            defaultValue = 1f,
                            currentValue = _settings.GlobalScale,
                            trackColor = new Color(100, 100, 100, 75),
                            onValueChanges = (e) =>
                            {
                                _settings.GlobalScale = e.currentValue;
                                if (_taikofield != null)
                                {
                                    _taikofield.GlobalScale = e.currentValue;
                                }
                                if (_stackfield != null)
                                {
                                    _stackfield.GlobalScale = e.currentValue;
                                }
                                SaveSettings();
                            }
                        }
                    }
                };
                settingsPanel.children.Add(globalScaleFrame);
                _settingsYOffset += 60f;
            }
            if (currentPage != "" && currentPage == "Graphics Config")
            {
                // --- Fullscreen Toggle ---
                settingsPanel.children.Add(CreateToggleRow("Fullscreen Mode", _settingsYOffset, 
                    () => _settings.Fullscreen, 
                    (val) => {
                        _settings.Fullscreen = val;
                        ConfigureWindow(DefaultScreenWidth, DefaultScreenHeight, "Playlist Stuck on Repeat", val);
                    }));
                _settingsYOffset += 60f;

                // --- Enable Canvas Movie Toggle ---
                settingsPanel.children.Add(CreateToggleRow("Enable Canvas Movie", _settingsYOffset, 
                    () => _settings.EnableCanvasMovie, 
                    (val) => {
                        _settings.EnableCanvasMovie = val;
                    }));
                _settingsYOffset += 60f;

                // --- Gameplay FPS ---
                settingsPanel.children.Add(CreateCycleSetting("Gameplay FPS", _settingsYOffset,
                    new int[] { 60, 80, 120, 250, 400, 500 },
                    () => _settings.GameplayFps,
                    (val) =>
                    {
                        _settings.GameplayFps = val;
                        if (_startPhase == 3) SetPerformanceMode(_settings.GameplayFps);
                    }));
                _settingsYOffset += 60f;

                // --- Menu FPS ---
                settingsPanel.children.Add(CreateCycleSetting("Menu FPS", _settingsYOffset,
                    new int[] { 30, 60, 80, 120, 250 },
                    () => _settings.MenuFps,
                    (val) =>
                    {
                        _settings.MenuFps = val;
                        if (_startPhase != 3) SetPerformanceMode(_settings.MenuFps);
                    }));
                _settingsYOffset += 60f;

                // --- Gameplay Update Rate ---
                //settingsPanel.children.Add(CreateCycleSetting("Gameplay Update Rate", _settingsYOffset,
                //    new int[] { 100, 250, 400, 900, 1200 },
                //    () => _settings.GameplayPollingRate,
                //    (val) =>
                //    {
                //        _settings.GameplayPollingRate = val;
                //        if (_startPhase == 3) SetPerformanceMode(_settings.GameplayPollingRate, _settings.GameplayFps);
                //    }));
                //_settingsYOffset += 60f;

                //// --- Menu Update Rate ---
                //settingsPanel.children.Add(CreateCycleSetting("Menu Update Rate", _settingsYOffset,
                //    new int[] { 100, 250, 400, 900, 1200 },
                //    () => _settings.MenuPollingRate,
                //    (val) =>
                //    {
                //        _settings.MenuPollingRate = val;
                //        if (_startPhase != 3) SetPerformanceMode(_settings.MenuPollingRate, _settings.MenuFps);
                //    }));
                //_settingsYOffset += 60f;
            }
        }

        private Button CreateToggleRow(string title, float yOffset, Func<bool> getState, Action<bool> setState)
        {
            float currentHoverScale = 0f;
            Button toggleBtn = new Button
            {
                position = new UDim2(0f, 0f, 10f, yOffset),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                onClick = (b) =>
                {
                    PlaySFX("select");
                    setState(!getState());
                    SaveSettings();
                },
                onHoverEnter = (b) => PlaySFX("hover"),
                onUpdate = (e) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);

                    float targetScale = e.IsHovered ? 1f : 0f;
                    currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                    e.size = new UDim2(.9f, 0f, 30f * currentHoverScale, 55f);

                    e.color = new Color(r, g, b, 175);
                    e.hoverColor = new Color(r, g, b, 235);
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
                scale = 1.15f,
                color = Color.White
            });

            toggleBtn.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                position = new UDim2(0.95f, 0.5f, 0, 0f),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Right,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                color = Color.White,
                onUpdate = (t, dt) =>
                {
                    t.text = getState() ? "ON" : "OFF";
                }
            });

            return toggleBtn;
        }

        private Button CreateCycleSetting<T>(string title, float yOffset, T[] options, Func<T> getValue, Action<T> setValue)
        {
            float currentHoverScale = 0f;
            Button cycleBtn = new Button
            {
                position = new UDim2(0f, 0f, 10f, yOffset),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                onClick = (b) =>
                {
                    PlaySFX("select");
                    T currentVal = getValue();
                    int index = Array.IndexOf(options, currentVal);
                    if (index == -1) index = 0;
                    int nextIndex = (index + 1) % options.Length;
                    setValue(options[nextIndex]);
                    SaveSettings();
                },
                onHoverEnter = (b) => PlaySFX("hover"),
                onUpdate = (e) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);

                    float targetScale = e.IsHovered ? 1f : 0f;
                    currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                    e.size = new UDim2(.9f, 0f, 30f * currentHoverScale, 55f);

                    e.color = new Color(r, g, b, 175);
                    e.hoverColor = new Color(r, g, b, 235);
                    e.pressedColor = new Color(r, g, b, 255);
                }
            };

            cycleBtn.children.Add(new TextFrame
            {
                text = title,
                fontName = "gsans_bold",
                position = new UDim2(0.05f, 0.5f, 0, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                color = Color.White
            });

            cycleBtn.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                position = new UDim2(0.95f, 0.5f, 0, 0f),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Right,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                onUpdate = (t, dt) => t.text = (getValue()?.ToString()?.ToUpper() ?? "") + " FPS",
            });

            return cycleBtn;
        }

        private Button CreateKeybindRow(string actionName, string title, float yOffset, Func<Keys> getKey, Action<Keys> setKey)
        {
            float currentHoverScale = 0f;

            Button keybindBtn = new Button
            {
                position = new UDim2(0f, 0f, 10f, yOffset),
                //size = new UDim2(.9f, 0f, 0f, 55f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                onClick = (b) =>
                {
                    PlaySFX("select");
                    _isListeningForKey = true;
                    _listeningActionName = actionName;
                },
                onHoverEnter = (b) => PlaySFX("hover"),
                onUpdate = (e) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);

                    bool isListeningThis = _isListeningForKey && _listeningActionName == actionName;

                    float targetScale = e.IsHovered ? 1f : 0f;
                    currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                    e.size = new UDim2(.9f, 0f, 30f * currentHoverScale, 55f);

                    e.color = isListeningThis ? new Color(255, 255, 255, 200) : new Color(r, g, b, 175);
                    e.hoverColor = isListeningThis ? new Color(255, 255, 255, 255) : new Color(r, g, b, 235);
                    e.pressedColor = new Color(255, 255, 255, 255);
                }
            };

            keybindBtn.children.Add(new TextFrame
            {
                text = title,
                fontName = "gsans_bold",
                position = new UDim2(0.05f, 0.5f, 0, 0f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Left,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                onUpdate = (t, dt) =>
                {
                    bool isListeningThis = _isListeningForKey && _listeningActionName == actionName;
                    t.color = isListeningThis ? Color.Black : Color.White;

                }
            });

            keybindBtn.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                position = new UDim2(0.95f, 0.5f, 0, 0f),
                anchorX = AnchorX.Right,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Right,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                onUpdate = (t, dt) =>
                {
                    bool isListeningThis = _isListeningForKey && _listeningActionName == actionName;
                    t.color = isListeningThis ? Color.Black : Color.White;
                    t.text = isListeningThis ? "press a key" : getKey().ToString().ToUpper();
                }
            });

            return keybindBtn;
        }
    }
}
