using System.Collections.Generic;
using OsuLib;

namespace CoreGame
{
    public class BeatmapGroup
    {
        public string Key { get; set; } = string.Empty;
        public OsuBeatmap Representative { get; set; } = null!;
        public List<OsuBeatmap> Difficulties { get; set; } = new();
        public bool IsExpanded { get; set; } = false;
    }
}
