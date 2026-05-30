using ArtFrame;
using ArtFrame.UserInterface;
using System;

namespace CoreGame
{
    public class WordController
    {
        public TextFrame TextNode { get; set; } = null!;
        public float Alpha { get; set; } = 0f;
        public float AcummulatedTime { get; set; } = 0f;
    }
}