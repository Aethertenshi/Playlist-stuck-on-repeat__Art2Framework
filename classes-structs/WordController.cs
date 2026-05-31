using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.UserInterface;
using System;

namespace CoreGame
{
    public class WordController
    {
        public TextFrame TextNode { get; set; } = null!;
        public UDim2 TargetPosition { get; set; } = default;
        public float Alpha { get; set; } = 0f;
        public float AcummulatedTime { get; set; } = 0f;
    }
}