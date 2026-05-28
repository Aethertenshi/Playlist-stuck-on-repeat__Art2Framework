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
                                SetSFXVolume("hover", e.currentValue);
                                SetSFXVolume("select", e.currentValue);
                                SetSFXVolume("beat", e.currentValue);
                                SetSFXVolume("dwbeat", e.currentValue);
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
                settingsPanel.children.Add(CreateKeybindRow("ListenScore", "Toggle Listen Score", _settingsYOffset, () => _keyToggleListenScore, (val) => _keyToggleListenScore = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("ToggleCover", "Menu Space Toggle", _settingsYOffset, () => _keyToggleCover, (val) => _keyToggleCover = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("StartGame", "Start/Play Song Key", _settingsYOffset, () => _keyStartGame, (val) => _keyStartGame = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("ExitGameplay", "Exit Song Key", _settingsYOffset, () => _keyExitGameplay, (val) => _keyExitGameplay = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("HitLeft", "Left Drum Hit", _settingsYOffset, () => _keyHitLeft, (val) => _keyHitLeft = val));
                _settingsYOffset += 60f;
                settingsPanel.children.Add(CreateKeybindRow("HitRight", "Right Drum Hit", _settingsYOffset, () => _keyHitRight, (val) => _keyHitRight = val));
                _settingsYOffset += 60f;
            }
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
