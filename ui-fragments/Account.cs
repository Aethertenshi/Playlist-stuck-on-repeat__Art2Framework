using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UIModifier;
using ArtFrame.UserInterface;
using System;
using System.Collections.Generic;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
        private bool _isRegisterMode = false;

        public ScrollingFrame BuildAccountUI()
        {
            ScrollingFrame accountPanel = new ScrollingFrame
            {
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                size = new UDim2(0f, 1f, 500f, -60f), // Match Settings and Modifiers footprint
                scrollDirection = Axis.Vertical,
                showScrollbar = false,
                smoothing = 18f,
                clipMode = ClipMode.Clip,
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    // Smoothly slide in from -510px to 0px on the left edge based on _accountTweener
                    e.position = UDim2.Lerp(
                        new UDim2(0f, 0, -510f, 60f), 
                        new UDim2(0f, 0f, 0f, 60f), 
                        MathF.Min(_accountTweener.CurrentValue, _bgTweener.CurrentValue * (1f - _startTransitionTweener.CurrentValue))
                    );
                }
            };

            accountPanel.modifiers.Add(new UIListLayout
            {
                direction = Axis.Vertical,
                spacing = 10f,
                paddingY = 10f
            });

            // 1. Header / Title Row
            Frame titleRow = new Frame
            {
                size = new UDim2(.93f, 0f, 0f, 45f),
                onUpdate = (e, dt) =>
                {
                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);
                    e.color = new Color(r, g, b, 175);
                }
            };
            titleRow.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.5f,
                color = Color.White,
                onUpdate = (tf, dt) =>
                {
                    tf.text = _online.IsLoggedIn ? "Online Profile" : "Account Auth";
                }
            });
            accountPanel.children.Add(titleRow);

            // 2. Input Text Boxes (TextBoxFrame)
            TextBoxFrame usernameInput = new TextBoxFrame
            {
                fontName = "gsans",
                placeholder = "Enter Username...",
                fontScale = 1.15f,
                maxLength = 32,
                size = new UDim2(.9f, 0f, 0f, 55f),
                textColor = Color.White,
                borderWidth = 1f,
                onUpdate = (tb, dt) =>
                {
                    bool hidden = _online.IsLoggedIn || !_isRegisterMode;
                    tb.alpha = hidden? 0f : 1f;
                    tb.skipDraw = hidden;
                    tb.size = hidden ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 55f);
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);
                    tb.backgroundColor = new Color(r, g, b, 140);
                    tb.focusedColor = new Color(r, g, b, 200);
                    tb.borderColor = new Color(r, g, b, 80);
                    tb.focusedBorderColor = Color.White;
                }
            };

            TextBoxFrame emailInput = new TextBoxFrame
            {
                fontName = "gsans",
                placeholder = "Enter Email...",
                fontScale = 1.15f,
                maxLength = 64,
                size = new UDim2(.9f, 0f, 0f, 55f),
                textColor = Color.White,
                borderWidth = 1f,
                onUpdate = (tb, dt) =>
                {
                    tb.alpha = _online.IsLoggedIn? 0f : 1f;
                    tb.skipDraw = _online.IsLoggedIn;
                    tb.size = _online.IsLoggedIn ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 55f);
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);
                    tb.backgroundColor = new Color(r, g, b, 140);
                    tb.focusedColor = new Color(r, g, b, 200);
                    tb.borderColor = new Color(r, g, b, 80);
                    tb.focusedBorderColor = Color.White;
                }
            };

            TextBoxFrame passwordInput = new TextBoxFrame
            {
                fontName = "gsans",
                placeholder = "Enter Password...",
                fontScale = 1.15f,
                maxLength = 64,
                isPassword = true,
                size = new UDim2(.9f, 0f, 0f, 55f),
                textColor = Color.White,
                borderWidth = 1f,
                onUpdate = (tb, dt) =>
                {
                    tb.alpha = _online.IsLoggedIn? 0f : 1f;
                    tb.skipDraw = _online.IsLoggedIn;
                    tb.size = _online.IsLoggedIn ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 55f);
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);
                    tb.backgroundColor = new Color(r, g, b, 140);
                    tb.focusedColor = new Color(r, g, b, 200);
                    tb.borderColor = new Color(r, g, b, 80);
                    tb.focusedBorderColor = Color.White;
                }
            };

            accountPanel.children.Add(usernameInput);
            accountPanel.children.Add(emailInput);
            accountPanel.children.Add(passwordInput);

            // 3. Submit Button
            Button submitBtn = new Button
            {
                size = new UDim2(.9f, 0f, 0f, 55f),
                onHoverEnter = (btn) => AudioHelper.PlaySFX("hover"),
                onClick = (btn) =>
                {
                    AudioHelper.PlaySFX("select");
                    if (_isRegisterMode)
                    {
                        _online.Register(emailInput.currentText, passwordInput.currentText, usernameInput.currentText);
                    }
                    else
                    {
                        _online.Login(emailInput.currentText, passwordInput.currentText);
                    }
                },
                onUpdate = (btn) =>
                {
                    btn.skipDraw = _online.IsLoggedIn;
                    btn.size = _online.IsLoggedIn ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 55f);
                    byte r = (byte)(_currentCoverColor.R * 0.85f);
                    byte g = (byte)(_currentCoverColor.G * 0.85f);
                    byte b = (byte)(_currentCoverColor.B * 0.85f);
                    btn.color = new Color(r, g, b, 175);
                    btn.hoverColor = new Color(r, g, b, 235);
                    btn.pressedColor = new Color(r, g, b, 255);
                }
            };
            submitBtn.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.35f,
                color = Color.White,
                onUpdate = (tf, dt) =>
                {
                    tf.text = _isRegisterMode ? "Register" : "Log In";
                }
            });
            accountPanel.children.Add(submitBtn);

            // 4. Switch Mode Button (Need an account? / Already have one?)
            Button switchBtn = new Button
            {
                size = new UDim2(.9f, 0f, 0f, 45f),
                onHoverEnter = (btn) => AudioHelper.PlaySFX("hover"),
                onClick = (btn) =>
                {
                    AudioHelper.PlaySFX("select");
                    _isRegisterMode = !_isRegisterMode;
                },
                onUpdate = (btn) =>
                {
                    btn.skipDraw = _online.IsLoggedIn;
                    btn.size = _online.IsLoggedIn ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 45f);
                    byte r = (byte)(_currentCoverColor.R * 0.5f);
                    byte g = (byte)(_currentCoverColor.G * 0.5f);
                    byte b = (byte)(_currentCoverColor.B * 0.5f);
                    btn.color = new Color(r, g, b, 100);
                    btn.hoverColor = new Color(r, g, b, 160);
                    btn.pressedColor = new Color(r, g, b, 200);
                }
            };
            switchBtn.children.Add(new TextFrame
            {
                fontName = "gsans",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                color = Color.White,
                onUpdate = (tf, dt) =>
                {
                    tf.text = _isRegisterMode ? "Already have an account? Log In" : "Need an account? Register";
                }
            });
            accountPanel.children.Add(switchBtn);

            // 5. Logged In Profile Panel
            Frame profileRow = new Frame
            {
                size = new UDim2(.9f, 0f, 0f, 55f),
                onUpdate = (e, dt) =>
                {
                    e.skipDraw = !_online.IsLoggedIn;
                    e.size = !_online.IsLoggedIn ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 55f);
                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);
                    e.color = new Color(r, g, b, 175);
                }
            };
            profileRow.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.25f,
                color = Color.White,
                onUpdate = (tf, dt) =>
                {
                    tf.text = $"Logged In As: {_online.Username}";
                }
            });
            accountPanel.children.Add(profileRow);

            Frame userIdRow = new Frame
            {
                size = new UDim2(.9f, 0f, 0f, 45f),
                onUpdate = (e, dt) =>
                {
                    e.skipDraw = !_online.IsLoggedIn;
                    e.size = !_online.IsLoggedIn ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 45f);
                    byte r = (byte)(_currentCoverColor.R * 0.5f);
                    byte g = (byte)(_currentCoverColor.G * 0.5f);
                    byte b = (byte)(_currentCoverColor.B * 0.5f);
                    e.color = new Color(r, g, b, 120);
                }
            };
            userIdRow.children.Add(new TextFrame
            {
                fontName = "gsans",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.05f,
                color = Color.White,
                onUpdate = (tf, dt) =>
                {
                    tf.text = $"User ID: {_online.UserId}";
                }
            });
            accountPanel.children.Add(userIdRow);

            // 6. Logout Button
            Button logoutBtn = new Button
            {
                size = new UDim2(.9f, 0f, 0f, 55f),
                onHoverEnter = (btn) => AudioHelper.PlaySFX("hover"),
                onClick = (btn) =>
                {
                    AudioHelper.PlaySFX("select");
                    _online.Logout();
                    emailInput.currentText = "";
                    passwordInput.currentText = "";
                    usernameInput.currentText = "";
                },
                onUpdate = (btn) =>
                {
                    btn.skipDraw = !_online.IsLoggedIn;
                    btn.size = !_online.IsLoggedIn ? new UDim2(.9f, 0f, 0f, 0f) : new UDim2(.9f, 0f, 0f, 55f);
                    btn.color = new Color("#FF6666") * 0.8f;
                    btn.hoverColor = new Color("#FF8888");
                    btn.pressedColor = new Color("#FF4444");
                }
            };
            logoutBtn.children.Add(new TextFrame
            {
                text = "Log Out",
                fontName = "gsans_bold",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.35f,
                color = Color.White
            });
            accountPanel.children.Add(logoutBtn);

            // 7. Status Message Display Row
            Frame statusRow = new Frame
            {
                size = new UDim2(.9f, 0f, 0f, 50f),
                color = new Color(0, 0, 0, 0),
                onUpdate = (f, dt) =>
                {
                    f.skipDraw = string.IsNullOrEmpty(_online.StatusMessage);
                }
            };
            statusRow.children.Add(new TextFrame
            {
                fontName = "gsans",
                position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                color = new Color(255, 220, 100),
                onUpdate = (tf, dt) =>
                {
                    tf.text = _online.StatusMessage;
                }
            });
            accountPanel.children.Add(statusRow);

            return accountPanel;
        }
    }
}
