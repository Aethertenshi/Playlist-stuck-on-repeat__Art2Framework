using ArtFrame.UserInterface;

namespace CoreGame
{
    public class MenuParticle
    {
        public CircleFrame VisualNode { get; set; } = null!;
        public float DriftSpeedX { get; set; }
        public float DriftSpeedY { get; set; }
        public float BaseSize { get; set; }
        public float PulsePhase { get; set; }
    }
}
