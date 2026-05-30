using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.UserInterface;
using ArtFrame.UIModifier;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
        public ScrollingFrame BuildScoreboardUI()
        {
            ScrollingFrame scoreboard = new ScrollingFrame
            {
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                size = new UDim2(0f, 0f, 450f, 360f),
                onUpdate = (e, dt) =>
                {
                    float activePanelValue = MathF.Max(_settingsTweener.CurrentValue, _modifiersTweener.CurrentValue);
                    float coverState = _bgTweener.CurrentValue * (1f - activePanelValue) * (1f - _startTransitionTweener.CurrentValue);
                    float effectiveListenScore = _listenScoreTweener.CurrentValue * (1f - activePanelValue);

                    // X coordinate is always centered relative to current cover/info center
                    float baseTargetX = ArtMathHelper.Lerp(ArtMathHelper.Lerp(0.38f, 0.42f, activePanelValue), 0.5f, _startTransitionTweener.CurrentValue);
                    float x = baseTargetX;

                    // Y axis only: slides up from offscreen bottom (1.3f) to its position (0.70f)
                    float y = ArtMathHelper.Lerp(1.3f, 0.70f, effectiveListenScore * coverState);

                    e.position = new UDim2(x, y, 0f, 20f);

                    byte r = (byte)(_currentCoverColor.R * 0.12f);
                    byte g = (byte)(_currentCoverColor.G * 0.12f);
                    byte b = (byte)(_currentCoverColor.B * 0.12f);

                    // Only shown when effectiveListenScore is active
                    float totalState = effectiveListenScore * coverState;
                    e.color = new Color(r, g, b, (byte)(160 * totalState));
                    e.alpha = totalState;

                    // Disable scissor clipping when fully off-screen (below viewport) to prevent MonoGame viewport overlap warnings
                    e.clipMode = (e.position.ScaleY >= 1.1f) ? ClipMode.None : ClipMode.Clip;
                }
            };
            RefreshScoreboard(scoreboard);
            return scoreboard;
        }

        private void RefreshScoreboard(ScrollingFrame scoreboard)
        {
            if (scoreboard == null) return;

            scoreboard.children.Clear();

            if (_beatmap == null) return;

            // Spotify style header
            Frame header = new Frame
            {
                position = new UDim2(0f, 0f, 0f, 15f),
                size = new UDim2(1f, 0f, 0f, 35f),
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                color = new Color(0, 0, 0, 0)
            };

            header.children.Add(new TextFrame
            {
                text = "Listen Scores",
                fontName = "gsans_bold",
                position = new UDim2(0.05f, 0.5f, 0f, 0f),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.15f,
                color = Color.White
            });

            scoreboard.children.Add(header);

            // Fetch leaderboard from ScoreManager
            string mapKey = _beatmap.FilePath;
            var scores = ScoreManager.GetLeaderboard(mapKey, _beatmap.Title, _beatmap.Version, 5);

            float yOffset = 50f;
            for (int i = 0; i < scores.Count; i++)
            {
                var scoreEntry = scores[i];
                int rank = i + 1;

                float currentHoverScale = 1f;
                int rankIndex = rank; // local copy for lambda

                var rowBtn = new Button
                {
                    position = new UDim2(0f, 0f, 10f, yOffset),
                    size = new UDim2(0.95f, 0f, 0f, 55f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Top,
                    onClick = (b) => {
                        AudioHelper.PlaySFX("select");
                    },
                    onHoverEnter = (b) => {
                        AudioHelper.PlaySFX("hover");
                    }
                };

                rowBtn.onUpdate = (btn) =>
                {
                    float hoveredScale = btn.IsHovered ? 1.03f : 1f;
                    float targetScale = btn.IsPressed ? hoveredScale + 0.02f : hoveredScale;
                    currentHoverScale = ArtMathHelper.Lerp(currentHoverScale, targetScale, 0.1f);

                    rowBtn.size = new UDim2(0.95f, 0f, 0f, 55f * currentHoverScale);

                    byte r = (byte)(_currentCoverColor.R * 0.7f);
                    byte g = (byte)(_currentCoverColor.G * 0.7f);
                    byte b = (byte)(_currentCoverColor.B * 0.7f);

                    btn.color = new Color(r, g, b, 140);
                    btn.hoverColor = new Color((byte)Math.Clamp(r + 20, 0, 255), (byte)Math.Clamp(g + 20, 0, 255), (byte)Math.Clamp(b + 20, 0, 255), 190);
                    btn.pressedColor = new Color((byte)Math.Clamp(r + 40, 0, 255), (byte)Math.Clamp(g + 40, 0, 255), (byte)Math.Clamp(b + 40, 0, 255), 220);
                };

                // Rank badge (Left)
                Color rankColor = Color.White;
                if (rankIndex == 1) rankColor = new Color(255, 215, 0);       // Gold
                else if (rankIndex == 2) rankColor = new Color(192, 192, 192); // Silver
                else if (rankIndex == 3) rankColor = new Color(205, 127, 50);  // Bronze

                var rankBadge = new TextFrame
                {
                    text = $"#{rankIndex}",
                    fontName = "gsans_bold",
                    position = new UDim2(0.04f, 0.5f, 0f, 0f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Left,
                    textAnchorY = AnchorY.Center,
                    scale = 1.05f,
                    color = rankColor
                };
                rowBtn.children.Add(rankBadge);

                // Player name + Mods capsule
                string displayName = scoreEntry.PlayerName;
                var nameText = new TextFrame
                {
                    text = displayName,
                    fontName = "gsans_bold",
                    position = new UDim2(0.16f, 0.5f, 0f, -10f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Left,
                    textAnchorY = AnchorY.Center,
                    scale = 0.95f,
                    color = Color.White
                };
                rowBtn.children.Add(nameText);

                // Mods badge capsule if any mods are active (e.g. DT, HD)
                if (!string.IsNullOrEmpty(scoreEntry.Mods) && scoreEntry.Mods != "NM")
                {
                    var modCapsule = new TextFrame
                    {
                        text = scoreEntry.Mods,
                        fontName = "gsans_bold",
                        position = new UDim2(0.52f, 0.5f, 0f, -10f),
                        anchorX = AnchorX.Left,
                        anchorY = AnchorY.Center,
                        textAnchorX = AnchorX.Left,
                        textAnchorY = AnchorY.Center,
                        scale = 0.75f,
                        color = new Color(30, 215, 96),
                        backgroundColor = new Color(0, 0, 0),
                        backgroundAlpha = 0.6f,
                        backgroundPadding = 4f
                    };
                    rowBtn.children.Add(modCapsule);
                }

                // Stats (Bottom Left: Accuracy and Max Combo)
                string stats = $"{scoreEntry.Accuracy:F2}%  //  {scoreEntry.MaxCombo}x";
                var statsText = new TextFrame
                {
                    text = stats,
                    fontName = "gsans",
                    position = new UDim2(0.16f, 0.5f, 0f, 10f),
                    anchorX = AnchorX.Left,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Left,
                    textAnchorY = AnchorY.Center,
                    scale = 0.75f,
                    color = new Color(175, 175, 175)
                };
                rowBtn.children.Add(statsText);

                // Score (Right)
                var scoreText = new TextFrame
                {
                    text = $"{scoreEntry.Score:N0}",
                    fontName = "gsans_bold",
                    position = new UDim2(0.92f, 0.5f, -12f, 0f),
                    anchorX = AnchorX.Right,
                    anchorY = AnchorY.Center,
                    textAnchorX = AnchorX.Right,
                    textAnchorY = AnchorY.Center,
                    scale = 1.0f,
                    color = Color.White
                };
                rowBtn.children.Add(scoreText);

                // Right-Edge Color Stripe (Vertical color bar matching ranking colors)
                var edgeStripe = new Frame
                {
                    position = new UDim2(1f, 0.5f, -4f, 0f),
                    size = new UDim2(0f, 0f, 4f, 30f),
                    anchorX = AnchorX.Right,
                    anchorY = AnchorY.Center,
                    color = rankColor
                };
                rowBtn.children.Add(edgeStripe);

                scoreboard.children.Add(rowBtn);
                yOffset += 65f;
            }
        }
    }
}
