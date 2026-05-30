using ArtFrame;
using ArtFrame.ArtTypes;
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
                size = new UDim2(0f, 1f, 510f, -60f), // Match the exact footprint of your song list
                scrollDirection = Axis.Vertical,
                showScrollbar = false,
                smoothing = 18f,
                clipMode = ClipMode.None,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    // Smoothly interpolate positions from tucked away (-510px) to resting at the left edge (0px)
                    e.position = UDim2.Lerp(new UDim2(0f, 0, -510f, 60f), new UDim2(0f, 0f, 0f, 60f), MathF.Min(_modifiersTweener.CurrentValue, _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue)));
                    e.alpha = _modifiersTweener.CurrentValue;
                    e.color = new Color((byte)(_currentCoverColor.R * 0.85f), (byte)(_currentCoverColor.G * 0.85f), (byte)(_currentCoverColor.B * 0.85f), 100);
                }
            };

            // 0. Header
            Frame modifiersTitle = new Frame
            {
                position = new UDim2(0f, 0f, 0f, _modifiersYOffset),
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
            modifiersPanel.children.Add(CreateModToggle("Adjust Pitch", _modifiersYOffset, () => _adjustPitch, (val) => { _adjustPitch = val; AudioHelper.SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch); }));
            _modifiersYOffset += 60f;

            // 3. Hidden Toggle
            modifiersPanel.children.Add(CreateModToggle("Hidden", _modifiersYOffset, () => _modHidden, (val) => { _modHidden = val; AudioHelper.SetMusicSpeed(_currentAudioKey, _actualMusicSpeed, _adjustPitch); }));
            _modifiersYOffset += 60f;

            return modifiersPanel;
        }
    }
}