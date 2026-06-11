using System;
using System.Collections.Generic;
using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.UserInterface;
using OsuLib;
using OsuLib.Models;

using static ArtFrame.AudioHelper;
using static ArtFrame.GraphicsHelper;

namespace CoreGame
{
    public class ResultScreen : Frame
    {
        // Callbacks
        public Action? OnRetry;
        public Action? OnQuit;

        // Visual components
        private Frame? _cardContainer;
        private TextFrame? _scoreText;
        private TextFrame? _maxComboText;
        private TextFrame? _rankText;
        private TextFrame? _hits300Text;
        private TextFrame? _hits200Text;
        private TextFrame? _hits50Text;
        private TextFrame? _hitsMissText;
        private TextFrame? _accuracyText;

        // Animation state
        private float _animationProgress = 0f;
        private bool _isShowing = false;
        private bool _isAutoplay = false;

        // Autoplay notice banner
        private Frame? _autoplayNotice;

        // Theme color matching current cover color
        public Color CoverColor { get; set; } = new Color(80, 60, 120);

        // Target score for animated counting
        private int _targetScore = 0;
        private float _displayedScore = 0f;

        // Card size — matches original footprint
        private const float CARD_W = 0.25f;
        private const float CARD_H = 0.32f;
        private const float SCALE_START = 0.88f;

        public ResultScreen()
        {
            size = new UDim2(1f, 1f, 0f, 0f);
            position = new UDim2(0.5f, 0.5f);
            anchorX = AnchorX.Center;
            anchorY = AnchorY.Center;
            color = new Color(0, 0, 0, 180);
            alpha = 0f;

            onUpdate = (self, dt) =>
            {
                if (_isShowing)
                    _animationProgress = Math.Min(1f, _animationProgress + dt * 2.8f);
                else
                    _animationProgress = Math.Max(0f, _animationProgress - dt * 3.5f);

                float t = _animationProgress;

                // Fade the dim overlay — children inherit via their own onUpdate
                self.alpha = t;

                _cardContainer?.alpha = t;

                // Scale-punch on show-in only; hold full size during hide so only alpha fades
                if (_isShowing)
                {
                    float scaleT = 1f - MathF.Pow(1f - t, 4f);
                    float s = ArtMathHelper.Lerp(SCALE_START, 1f, scaleT);
                    _cardContainer?.size = new UDim2(CARD_W * s, CARD_H * s, 0f, 0f);
                }

                // Animated score counter
                if (_isShowing && _displayedScore < _targetScore)
                {
                    _displayedScore = Math.Min(_targetScore, _displayedScore + _targetScore * dt * 3.5f + 200f);
                    if (_scoreText != null) _scoreText.text = $"{(int)_displayedScore:N0}";
                }
                else if (!_isShowing && _animationProgress <= 0f)
                {
                    _displayedScore = 0f;
                }
            };

            // ── Card container ──
            _cardContainer = new Frame
            {
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                position = new UDim2(0.5f, 0.5f),
                size = new UDim2(CARD_W, CARD_H, 0f, 0f),
                color = new Color(16, 18, 24, 252),
                alpha = 0f,
                onUpdate = (e, dt) =>
                {
                    byte r = (byte)(CoverColor.R * 0.12f);
                    byte g = (byte)(CoverColor.G * 0.12f);
                    byte b = (byte)(CoverColor.B * 0.12f);
                    e.color = new Color(r, g, b, 252);
                }
            };
            children.Add(_cardContainer);

            BuildCard();
        }

        private void BuildCard()
        {
            // ── Accent glow border (top edge) ──
                _cardContainer?.children.Add(new Frame
                {
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Top,
                    position = new UDim2(0.5f, 0f, 0f, 0f),
                    size = new UDim2(1f, 0f, 0f, 3f),
                    color = new Color(120, 80, 255),
                    onUpdate = (e, dt) =>
                    {
                        e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                        byte r = (byte)Math.Clamp(CoverColor.R * 1.2f, 0, 255);
                        byte g = (byte)Math.Clamp(CoverColor.G * 1.2f, 0, 255);
                        byte b = (byte)Math.Clamp(CoverColor.B * 1.2f, 0, 255);
                        e.color = new Color(r, g, b, 255);
                    }
                });

            // ── Header strip ──
            Frame header = new Frame
            {
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Top,
                position = new UDim2(0.5f, 0f, 0f, 3f),
                size = new UDim2(1f, 0f, 0f, 30f),
                color = new Color(28, 22, 48, 240),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer != null ? _cardContainer.alpha : 0 : 0;
                    byte r = (byte)(CoverColor.R * 0.25f);
                    byte g = (byte)(CoverColor.G * 0.25f);
                    byte b = (byte)(CoverColor.B * 0.25f);
                    e.color = new Color(r, g, b, 240);
                }
            };
            _cardContainer?.children.Add(header);

            header.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                text = "RESULTS",
                anchorX = AnchorX.Center, anchorY = AnchorY.Center,
                position = new UDim2(0.5f, 0.5f),
                textAnchorX = AnchorX.Center, textAnchorY = AnchorY.Center,
                scale = 1.1f,
                color = new Color(220, 200, 255),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer != null ? _cardContainer.alpha : 0 : 0;
                    byte r = (byte)Math.Clamp(CoverColor.R * 0.9f + 80, 0, 255);
                    byte g = (byte)Math.Clamp(CoverColor.G * 0.9f + 80, 0, 255);
                    byte b = (byte)Math.Clamp(CoverColor.B * 0.9f + 80, 0, 255);
                    e.color = new Color(r, g, b, 255);
                }
            });

            // ═══════════════════════════════════
            // LEFT COLUMN  (x: 0..0.50)
            // ═══════════════════════════════════

            // Rank badge
            Frame rankBadge = new Frame
            {
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                position = new UDim2(0.05f, 0f, 0f, 38f),
                size = new UDim2(0f, 0f, 70f, 70f),
                color = new Color(28, 24, 50, 200),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)(CoverColor.R * 0.25f);
                    byte g = (byte)(CoverColor.G * 0.25f);
                    byte b = (byte)(CoverColor.B * 0.25f);
                    e.color = new Color(r, g, b, 200);
                }
            };
            _cardContainer?.children.Add(rankBadge);

            _rankText = new TextFrame
            {
                fontName = "gsans_bold",
                text = "S",
                anchorX = AnchorX.Center, anchorY = AnchorY.Center,
                position = new UDim2(0.5f, 0.5f),
                textAnchorX = AnchorX.Center, textAnchorY = AnchorY.Center,
                scale = 3.5f,
                color = new Color(255, 215, 0),
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            };
            rankBadge.children.Add(_rankText);

            // Max combo label
                _cardContainer?.children.Add(new TextFrame
                {
                    fontName = "gsans",
                    text = "MAX COMBO",
                    anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                    textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top,
                    position = new UDim2(0.05f, 0f, 0f, 114f),
                    scale = 0.85f,
                    color = new Color(160, 140, 200),
                    onUpdate = (e, dt) =>
                    {
                        e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                        byte r = (byte)Math.Clamp(CoverColor.R * 0.65f + 100, 0, 255);
                        byte g = (byte)Math.Clamp(CoverColor.G * 0.65f + 100, 0, 255);
                        byte b = (byte)Math.Clamp(CoverColor.B * 0.65f + 100, 0, 255);
                        e.color = new Color(r, g, b, 255);
                    }
                });

            _maxComboText = new TextFrame
            {
                fontName = "gsans_bold",
                text = "0x",
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top,
                position = new UDim2(0.05f, 0f, 0f, 128f),
                scale = 2.0f,
                color = Color.White,
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            };
            _cardContainer?.children.Add(_maxComboText);

            // Thin divider
            _cardContainer?.children.Add(new Frame
            {
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                position = new UDim2(0.05f, 0f, 0f, 172f),
                size = new UDim2(0.42f, 0f, 0f, 1f),
                color = new Color(80, 60, 120, 180),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)(CoverColor.R * 0.4f);
                    byte g = (byte)(CoverColor.G * 0.4f);
                    byte b = (byte)(CoverColor.B * 0.4f);
                    e.color = new Color(r, g, b, 180);
                }
            });

            // Score label
            _cardContainer?.children.Add(new TextFrame
            {
                fontName = "gsans",
                text = "TOTAL SCORE",
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top,
                position = new UDim2(0.05f, 0f, 0f, 178f),
                scale = 0.85f,
                color = new Color(160, 140, 200),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)Math.Clamp(CoverColor.R * 0.65f + 100, 0, 255);
                    byte g = (byte)Math.Clamp(CoverColor.G * 0.65f + 100, 0, 255);
                    byte b = (byte)Math.Clamp(CoverColor.B * 0.65f + 100, 0, 255);
                    e.color = new Color(r, g, b, 255);
                }
            });

            _scoreText = new TextFrame
            {
                fontName = "gsans_bold",
                text = "0",
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top,
                position = new UDim2(0.05f, 0f, 0f, 192f),
                scale = 1.55f,
                color = new Color(200, 180, 255),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)Math.Clamp(CoverColor.R * 1.0f + 100, 0, 255);
                    byte g = (byte)Math.Clamp(CoverColor.G * 1.0f + 100, 0, 255);
                    byte b = (byte)Math.Clamp(CoverColor.B * 1.0f + 100, 0, 255);
                    e.color = new Color(r, g, b, 255);
                }
            };
            _cardContainer?.children.Add(_scoreText);

            // Accuracy label
            _cardContainer?.children.Add(new TextFrame
            {
                fontName = "gsans",
                text = "ACCURACY",
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top,
                position = new UDim2(0.05f, 0f, 0f, 225f),
                scale = 0.85f,
                color = new Color(160, 140, 200),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)Math.Clamp(CoverColor.R * 0.65f + 100, 0, 255);
                    byte g = (byte)Math.Clamp(CoverColor.G * 0.65f + 100, 0, 255);
                    byte b = (byte)Math.Clamp(CoverColor.B * 0.65f + 100, 0, 255);
                    e.color = new Color(r, g, b, 255);
                }
            });

            _accuracyText = new TextFrame
            {
                fontName = "gsans_bold",
                text = "100.00%",
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top,
                position = new UDim2(0.05f, 0f, 0f, 238f),
                scale = 1.25f,
                color = new Color(100, 255, 180),
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            };
            _cardContainer?.children.Add(_accuracyText);

            // ═══════════════════════════════════
            // Vertical divider
            // ═══════════════════════════════════            
            _cardContainer?.children.Add(new Frame
            {
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                position = new UDim2(0.50f, 0f, 0f, 35f),
                size = new UDim2(0f, 0f, 1f, 240f),
                color = new Color(50, 40, 80, 200),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)(CoverColor.R * 0.3f);
                    byte g = (byte)(CoverColor.G * 0.3f);
                    byte b = (byte)(CoverColor.B * 0.3f);
                    e.color = new Color(r, g, b, 200);
                }
            });

            // ═══════════════════════════════════
            // RIGHT COLUMN — hit breakdown
            // ═══════════════════════════════════            
            _cardContainer?.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                text = "HITS",
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Top,
                position = new UDim2(0.54f, 0f, 0f, 40f),
                scale = 0.85f,
                color = new Color(140, 120, 180),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)Math.Clamp(CoverColor.R * 0.65f + 100, 0, 255);
                    byte g = (byte)Math.Clamp(CoverColor.G * 0.65f + 100, 0, 255);
                    byte b = (byte)Math.Clamp(CoverColor.B * 0.65f + 100, 0, 255);
                    e.color = new Color(r, g, b, 255);
                }
            });

            AddHitRow(0.54f,  62f, new Color(80,  180, 255), "300",  out _hits300Text);
            AddHitRow(0.54f, 104f, new Color(100, 220, 100), "200",  out _hits200Text);
            AddHitRow(0.54f, 146f, new Color(255, 165,  50), "50",   out _hits50Text);
            AddHitRow(0.54f, 188f, new Color(255,  70,  70), "MISS", out _hitsMissText);

            // ═══════════════════════════════════
            // Autoplay notice (hidden by default)
            // ═══════════════════════════════════
            _autoplayNotice = new Frame
            {
                anchorX = AnchorX.Center, anchorY = AnchorY.Bottom,
                position = new UDim2(0.5f, 1f, 0f, -28f),
                size = new UDim2(0.9f, 0f, 0f, 0f),
                color = new Color(200, 160, 0, 60),
                skipDraw = true,
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            };
            _autoplayNotice.children.Add(new Frame
            {
                anchorX = AnchorX.Left, anchorY = AnchorY.Center,
                position = new UDim2(0f, 0.5f),
                size = new UDim2(0f, 0f, 3f, 22f),
                color = new Color(255, 210, 0),
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            });
            _autoplayNotice.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                text = "AUTOPLAYED: score not submitted",
                anchorX = AnchorX.Center, anchorY = AnchorY.Center,
                position = new UDim2(0.5f, 0.5f),
                textAnchorX = AnchorX.Center, textAnchorY = AnchorY.Center,
                scale = 0.75f,
                color = new Color(255, 210, 50),
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            });
            _cardContainer?.children.Add(_autoplayNotice);

            // ═══════════════════════════════════
            // Bottom bar
            // ═══════════════════════════════════
            Frame bottomBar = new Frame
            {
                anchorX = AnchorX.Center, anchorY = AnchorY.Bottom,
                position = new UDim2(0.5f, 1f, 0f, 0f),
                size = new UDim2(1f, 0f, 0f, 28f),
                color = new Color(10, 8, 20, 255),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)(CoverColor.R * 0.08f);
                    byte g = (byte)(CoverColor.G * 0.08f);
                    byte b = (byte)(CoverColor.B * 0.08f);
                    e.color = new Color(r, g, b, 255);
                }
            };
            _cardContainer?.children.Add(bottomBar);

            Button backBtn = new Button
            {
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                position = new UDim2(0f, 0f, 0.5f, 0f),
                size = new UDim2(0f, 0f, 80f, 22f),
                color = new Color(255, 255, 255, 0),
                hoverColor = new Color(255, 255, 255, 18),
                pressedColor = new Color(255, 255, 255, 35),
                onHoverEnter = (_) => AudioHelper.PlaySFX("hover"),
                onClick = (_) => { AudioHelper.PlaySFX("select"); Hide(); OnQuit?.Invoke(); },
                onUpdate = (e) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            };
            backBtn.children.Add(new TextFrame
            {
                fontName = "gsans", position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center, anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center, textAnchorY = AnchorY.Center,
                scale = 0.9f, color = new Color(160, 140, 200), text = "Back",
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)Math.Clamp(CoverColor.R * 0.65f + 100, 0, 255);
                    byte g = (byte)Math.Clamp(CoverColor.G * 0.65f + 100, 0, 255);
                    byte b = (byte)Math.Clamp(CoverColor.B * 0.65f + 100, 0, 255);
                    e.color = new Color(r, g, b, 255);
                }
            });
            bottomBar.children.Add(backBtn);

            Button retryBtn = new Button
            {
                anchorX = AnchorX.Right, anchorY = AnchorY.Top,
                position = new UDim2(1f, 0f, 0.5f, 0f),
                size = new UDim2(0f, 0f, 80f, 22f),
                color = new Color(255, 255, 255, 0),
                hoverColor = new Color(120, 80, 255, 60),
                pressedColor = new Color(120, 80, 255, 120),
                onHoverEnter = (_) => AudioHelper.PlaySFX("hover"),
                onClick = (_) => { AudioHelper.PlaySFX("select"); Hide(); OnRetry?.Invoke(); },
                onUpdate = (e) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    if (e is Button btn)
                    {
                        btn.hoverColor = new Color((byte)(CoverColor.R * 0.7f), (byte)(CoverColor.G * 0.7f), (byte)(CoverColor.B * 0.7f), 60);
                        btn.pressedColor = new Color(CoverColor.R, CoverColor.G, CoverColor.B, 120);
                    }
                }
            };
            retryBtn.children.Add(new TextFrame
            {
                fontName = "gsans", position = new UDim2(0.5f, 0.5f),
                anchorX = AnchorX.Center, anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center, textAnchorY = AnchorY.Center,
                scale = 0.9f, color = new Color(180, 160, 255), text = "Retry",
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)Math.Clamp(CoverColor.R * 0.9f + 80, 0, 255);
                    byte g = (byte)Math.Clamp(CoverColor.G * 0.9f + 80, 0, 255);
                    byte b = (byte)Math.Clamp(CoverColor.B * 0.9f + 80, 0, 255);
                    e.color = new Color(r, g, b, 255);
                }
            });
            bottomBar.children.Add(retryBtn);
        }

        /// <summary>Adds a compact hit row: colored accent bar + label + count.</summary>
        private void AddHitRow(float xBase, float yOffset, Color rowColor, string label, out TextFrame countText)
        {
            // Accent bar            
            _cardContainer?.children.Add(new Frame
            {
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                position = new UDim2(xBase, 0f, 0f, yOffset),
                size = new UDim2(0f, 0f, 2f, 36f),
                color = rowColor,
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            });

            // Row background
            Frame rowBg = new Frame
            {
                anchorX = AnchorX.Left, anchorY = AnchorY.Top,
                position = new UDim2(xBase + 0.01f, 0f, 0f, yOffset),
                size = new UDim2(0.44f, 0f, 0f, 36f),
                color = new Color((byte)(rowColor.R / 7), (byte)(rowColor.G / 7), (byte)(rowColor.B / 7), 120),
                onUpdate = (e, dt) =>
                {
                    e.alpha = _cardContainer != null ? _cardContainer.alpha : 0;
                    byte r = (byte)(CoverColor.R * 0.18f);
                    byte g = (byte)(CoverColor.G * 0.18f);
                    byte b = (byte)(CoverColor.B * 0.18f);
                    e.color = new Color(r, g, b, 120);
                }
            };
            _cardContainer?.children.Add(rowBg);

            // Label
            rowBg.children.Add(new TextFrame
            {
                fontName = "gsans_bold",
                text = label,
                anchorX = AnchorX.Left, anchorY = AnchorY.Center,
                position = new UDim2(0.08f, 0.5f),
                textAnchorX = AnchorX.Left, textAnchorY = AnchorY.Center,
                scale = 1.05f,
                color = rowColor,
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            });

            // Count
            countText = new TextFrame
            {
                fontName = "gsans_bold",
                text = "0",
                anchorX = AnchorX.Right, anchorY = AnchorY.Center,
                position = new UDim2(0.92f, 0.5f),
                textAnchorX = AnchorX.Right, textAnchorY = AnchorY.Center,
                scale = 1.25f,
                color = Color.White,
                onUpdate = (e, dt) => e.alpha = _cardContainer != null ? _cardContainer.alpha : 0
            };
            rowBg.children.Add(countText);
        }

        private string ComputeRank(int perfects, int goods, int oks, int misses)
        {
            int total = perfects + goods + oks + misses;
            if (total == 0) return "F";
            float acc = (float)(perfects * 300 + goods * 200 + oks * 50) / (total * 300);
            if (misses == 0 && goods == 0 && oks == 0) return "SS";
            if (misses == 0 && acc >= 0.97f) return "S";
            if (acc >= 0.93f) return "A";
            if (acc >= 0.80f) return "B";
            if (acc >= 0.65f) return "C";
            return "D";
        }

        private Color RankColor(string rank) => rank switch
        {
            "SS" => new Color(255, 230, 50),
            "S"  => new Color(255, 215, 0),
            "A"  => new Color(100, 230, 100),
            "B"  => new Color(80, 160, 255),
            "C"  => new Color(200, 120, 40),
            _    => new Color(220, 60, 60)
        };

        public override void Draw(float dt, Vector2 parentSize, Vector2 parentOrigin)
        {
            if (alpha <= 0f && !_isShowing) return;
            base.Draw(dt, parentSize, parentOrigin);
        }

        public void Show(OsuBeatmap beatmap, int score, int maxCombo, int perfects, int goods, int oks, int misses, bool isAutoplay = false)
        {
            _isShowing = true;
            _isAutoplay = isAutoplay;
            _animationProgress = 0f;
            _displayedScore = 0f;

            // Autoplay notice
            _autoplayNotice?.skipDraw = !isAutoplay;
            _autoplayNotice?.size = isAutoplay
                ? new UDim2(0.9f, 0f, 0f, 22f)
                : new UDim2(0.9f, 0f, 0f, 0f);

            _targetScore = score;
            _scoreText?.text = "0";
            _maxComboText?.text = $"{maxCombo}x";
            _hits300Text?.text = perfects.ToString();
            _hits200Text?.text = goods.ToString();
            _hits50Text?.text = oks.ToString();
            _hitsMissText?.text = misses.ToString();

            string rank = ComputeRank(perfects, goods, oks, misses);
            _rankText?.text = rank;
            _rankText?.color = RankColor(rank);

            int total = perfects + goods + oks + misses;
            float acc = total > 0
                ? (float)(perfects * 300 + goods * 200 + oks * 50) / (total * 300) * 100f
                : 100f;
            _accuracyText?.text = $"{acc:F2}%";
            _accuracyText?.color = acc >= 95f
                ? new Color(100, 255, 180)
                : acc >= 80f
                    ? new Color(255, 200, 80)
                    : new Color(255, 100, 100);

            // Reset card to start of entrance animation
            _cardContainer?.size = new UDim2(CARD_W * SCALE_START, CARD_H * SCALE_START, 0f, 0f);
        }

        public void Hide()
        {
            _isShowing = false;
        }
    }
}
