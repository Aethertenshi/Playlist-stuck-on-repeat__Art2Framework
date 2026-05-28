namespace CoreGame
{
    public class GameSettings
    {
        public float MainVolume { get; set; } = 0.5f;
        public float EffectsVolume { get; set; } = 0.5f;
        public float AudioOffset { get; set; } = -55.35f;

        // Keybindings
        public string KeyToggleCover { get; set; } = "Space";
        public string KeyStartGame { get; set; } = "Tab";
        public string KeyExitGameplay { get; set; } = "RightShift";
        public string KeyHitLeft { get; set; } = "W";
        public string KeyHitRight { get; set; } = "Q";
        public string KeyToggleListenScore { get; set; } = "LeftShift";
    }
}
