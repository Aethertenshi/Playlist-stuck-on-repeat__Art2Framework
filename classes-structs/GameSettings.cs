namespace CoreGame
{
    public class GameSettings
    {
        public float MainVolume { get; set; } = 0.5f;
        public float EffectsVolume { get; set; } = 0.5f;
        public float AudioOffset { get; set; } = -55.35f;

        // Gameplay Configuration
        public float ScrollSpeed { get; set; } = 0.25f;
        public float GlobalScale { get; set; } = 1.4f;

        // Graphics and Performance Configuration
        public bool EnableCanvasMovie { get; set; } = false;
        public bool Fullscreen { get; set; } = false;
        public int GameplayFps { get; set; } = 250;
        public int MenuFps { get; set; } = 120;
        public int GameplayPollingRate { get; set; } = 1200;
        public int MenuPollingRate { get; set; } = 250;

        // Keybindings
        public string KeyExitGame { get; set; } = "Escape";
        public string KeyToggleCover { get; set; } = "Space";
        public string KeyStartGame { get; set; } = "Tab";
        public string KeyExitGameplay { get; set; } = "RightShift";
        public string KeyHitLeft1 { get; set; } = "Q";
        public string KeyHitLeft2 { get; set; } = "W";
        public string KeyHitRight1 { get; set; } = "O";
        public string KeyHitRight2 { get; set; } = "P";
    }
}
