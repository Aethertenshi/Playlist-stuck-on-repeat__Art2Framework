using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UIModifier;
using ArtFrame.UserInterface;
using System;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
        public ScrollingFrame BuildModifiersUI()
        {
            ScrollingFrame modifiersPanel = new ScrollingFrame
            {
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(0f, 1f, 500f, -60f), // Match the exact footprint of your song list
                scrollDirection = Axis.Vertical,
                showScrollbar = false,
                smoothing = 18f,
                clipMode = ClipMode.None,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    // Smoothly interpolate positions from tucked away (-510px) to resting at the left edge (0px)
                    e.position = UDim2.Lerp(new UDim2(0f, 0, -510f, 60f), new UDim2(0f, 0f, 0f, 60f), MathF.Min(_modifiersTweener.CurrentValue, _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue)));

                    // Disable scissor clipping when fully off-screen to prevent MonoGame viewport overlap warnings
                    //e.clipMode = (e.position.OffsetX <= -509f) ? ClipMode.None : ClipMode.Clip;
                }
            };

            // 0. Header
            Frame modifiersTitle = new Frame
            {
                position = new UDim2(0f, 0f, 10f, _modifiersYOffset),
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
            modifiersTitle.children.Add(new TextFrame
            {
                text = "Plugins",
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f, 0, 0f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.5f,
                color = Color.White
            });
            modifiersPanel.children.Add(modifiersTitle);
            _modifiersYOffset += 50f;

            // 1. Double Time (Speed) Slider
            Frame mainDTSlider = new Frame
            {
                position = new UDim2(0f, 0f, 10f, _modifiersYOffset),
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
                        minValue = 0.5f,
                        maxValue = 2.0f,
                        defaultValue = 1.0f,
                        currentValue = _speedMultiplier,
                        trackColor = new Color(100, 100, 100, 75),
                        onSlide = (e) =>
                        {
                            _speedMultiplier = e.currentValue;
                        },
                        onValueChanges = (e) =>
                        {
                            _speedMultiplier = e.currentValue;
                        },
                    }
                }
            };
            modifiersPanel.children.Add(mainDTSlider);
            _modifiersYOffset += 60f;

            // 2. Adjust Pitch Toggle
            modifiersPanel.children.Add(CreateModToggle("Adjust Pitch", _modifiersYOffset, () => _adjustPitch, (val) => { _adjustPitch = val; AudioHelper.SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch); }));
            _modifiersYOffset += 60f;

            // 3. Hidden Toggle
            modifiersPanel.children.Add(CreateModToggle("Hidden", _modifiersYOffset, () => _modHidden, (val) => { _modHidden = val; AudioHelper.SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch); }));
            _modifiersYOffset += 60f;

            // 4. Autoplay
            modifiersPanel.children.Add(CreateModToggle("Autoplay", _modifiersYOffset, () => _modAutoplay, (val) => { _modAutoplay = val; AudioHelper.SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch); }));
            _modifiersYOffset += 60f;

            // 5. Play Mode Toggle
            modifiersPanel.children.Add(CreatePlayModeToggle("Play Mode", _modifiersYOffset, () => _modSingleMode, (val) => { _modSingleMode = val; }));
            _modifiersYOffset += 60f;

            return modifiersPanel;
        }

        private Button CreatePlayModeToggle(string title, float yOffset, Func<bool> getState, Action<bool> setState)
        {
            float currentHoverScale = 0f;

            Button toggleBtn = new Button
            {
                position = new UDim2(0f, 0f, 10f, yOffset),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                onClick = (b) =>
                {
                    AudioHelper.PlaySFX("select");
                    setState(!getState()); // Flip the boolean
                },
                onHoverEnter = (b) => AudioHelper.PlaySFX("hover"),
                onUpdate = (e) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);

                    float hoveredScale = e.IsHovered ? 1f : 0f;
                    float targetScale = getState() ? hoveredScale + 0.25f : hoveredScale;
                    currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                    // Height pop micro-animation matching settings!
                    e.size = new UDim2(.9f, 0f, 30f * currentHoverScale, 55f);

                    e.color = new Color(!getState() ? r : (byte)(r * 1.3f), !getState() ? g : (byte)(g * 1.3f), !getState() ? b : (byte)(b * 1.3f), 175);
                    e.hoverColor = new Color(r, g, b, 235);
                    e.pressedColor = new Color(r, g, b, 250);
                }
            };

            // Title label on the left
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

            // Status label (Single Mode / Switch Mode) on the right
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
                    t.text = getState() ? "Single Mode" : "Switch Mode";
                }
            });

            return toggleBtn;
        }

        private Button CreateModToggle(string title, float yOffset, Func<bool> getState, Action<bool> setState)
        {
            float currentHoverScale = 0f;

            Button toggleBtn = new Button
            {
                position = new UDim2(0f, 0f, 10f, yOffset),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                onClick = (b) =>
                {
                    AudioHelper.PlaySFX("select");
                    setState(!getState()); // Flip the boolean
                },
                onHoverEnter = (b) => AudioHelper.PlaySFX("hover"),
                onUpdate = (e) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);

                    float hoveredScale = e.IsHovered ? 1f : 0f;
                    float targetScale = getState() ? hoveredScale + 0.25f : hoveredScale;
                    currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.05f);

                    // Height pop micro-animation matching settings!
                    e.size = new UDim2(.9f, 0f, 30f * currentHoverScale, 55f);

                    e.color = new Color(!getState()? r : (byte)(r * 1.3f), !getState()? g : (byte)(g * 1.3f), !getState()? b : (byte)(b * 1.3f), 175);
                    e.hoverColor = new Color(r, g, b, 235);
                    e.pressedColor = new Color(r, g, b, 250);
                }
            };

            // Title label on the left
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

            // Status label (ON/OFF) on the right
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
    }
}