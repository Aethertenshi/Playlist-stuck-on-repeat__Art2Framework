using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.UserInterface;

namespace CoreGame
{
    public partial class MainGame : IArt
    {
        public ScrollingFrame BuildScoreboardUI()
        {
            return new ScrollingFrame
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
                }
            };
        }
    }
}
