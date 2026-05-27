using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UserInterface;

using static ArtFrame.AudioHelper;
using static ArtFrame.InputHelper;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
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

            if (currentPage != "" && currentPage == "Key Bindings")
            {
                settingsPanel.children.Add(CreateKeybindRow("ToggleCover", "Menu Space Toggle", _settingsYOffset, () => _keyToggleCover, (val) => _keyToggleCover = val));
                _settingsYOffset += 50f;
                settingsPanel.children.Add(CreateKeybindRow("StartGame", "Start/Play Song Key", _settingsYOffset, () => _keyStartGame, (val) => _keyStartGame = val));
                _settingsYOffset += 50f;
                settingsPanel.children.Add(CreateKeybindRow("ExitGameplay", "Exit Song Key", _settingsYOffset, () => _keyExitGameplay, (val) => _keyExitGameplay = val));
                _settingsYOffset += 50f;
                settingsPanel.children.Add(CreateKeybindRow("HitLeft", "Left Drum Hit", _settingsYOffset, () => _keyHitLeft, (val) => _keyHitLeft = val));
                _settingsYOffset += 50f;
                settingsPanel.children.Add(CreateKeybindRow("HitRight", "Right Drum Hit", _settingsYOffset, () => _keyHitRight, (val) => _keyHitRight = val));
                _settingsYOffset += 50f;
            }
        }

        private Button CreateKeybindRow(string actionName, string title, float yOffset, Func<Keys> getKey, Action<Keys> setKey)
        {
            Button keybindBtn = new Button
            {
                position = new UDim2(0.5f, 0f, 0f, yOffset),
                size = new UDim2(.9f, 0f, 0f, 45f),
                anchorX = AnchorX.Center,
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
                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);

                    bool isListeningThis = _isListeningForKey && _listeningActionName == actionName;

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
                scale = 1.35f,
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
                scale = 1.35f,
                onUpdate = (t, dt) =>
                {
                    bool isListeningThis = _isListeningForKey && _listeningActionName == actionName;
                    t.color = isListeningThis ? Color.Black : Color.White;
                    t.text = isListeningThis ? "press a key" : getKey().ToString().ToUpper();
                }
            });

            return keybindBtn;
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
    }
}
