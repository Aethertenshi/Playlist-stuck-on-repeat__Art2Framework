using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UIModifier;
using ArtFrame.UserInterface;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
        public Frame BuildTopbarUI()
        {
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
                    AudioHelper.PlaySFX("hover");
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
                    btn.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);
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
                    AudioHelper.PlaySFX("hover");
                },
                onClick = (btn) =>
                {
                    AudioHelper.PlaySFX("select");
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
                    btn.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);
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
                    AudioHelper.PlaySFX("hover");
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
                    btn.alpha = _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue);
                },
                onClick = (btn) =>
                {
                    AudioHelper.PlaySFX("select");
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

            return topBar;
        }
    }
}
