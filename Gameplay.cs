using System;
using System.Collections.Generic;
using System.Linq;
using ArtFrame.ArtTypes;
using ArtFrame.UserInterface;
using OsuLib;
using OsuLib.Models;

// Bind strictly to your custom wrappers!
using static ArtFrame.InputHelper;

namespace CoreGame
{
    public class TaikoPlayfield : Frame
    {
        // --- Configuration ---
        public Keys[] HitKeys { get; set; } = { Keys.W, Keys.Q };
        public float ScrollSpeed { get; set; } = 0.5f;
        public float GlobalScale { get; set; } = 1.0f; // Changes runtime UI sizing

        // Hit Windows (Milliseconds)
        public float Window300 { get; set; } = 35f;
        public float Window100 { get; set; } = 75f;
        public float Window50 { get; set; } = 100f; // This acts as the max HitWindow

        // --- State & Gameplay ---
        public int Score { get; private set; } = 0;
        public int Combo { get; private set; } = 0;
        private float _introAlpha = 0f; // Smooth fade-in tracker

        // --- Events ---
        public Action<int>? OnPlayHitSound; // Hook your audio to this!
        public Action? OnExit; // Hook your main menu transition to this!

        // --- Visuals & Nodes ---
        private readonly Image _circleTexture;
        private readonly string _fontName;

        private Frame _leftTrack;
        private Frame _rightTrack;
        private ImageFrame _judgementRing;
        private TextFrame _scoreUI;
        private TextFrame _comboUI;

        private float BaseNoteScale = 40f;
        private float BaseJudgementOffsetX = 300f;

        private List<TaikoNote> _notes = new List<TaikoNote>();
        private List<FloatingText> _floatingTexts = new List<FloatingText>();

        public TaikoPlayfield(Image circleTexture, string fontName = "gsans_bold")
        {
            _circleTexture = circleTexture;
            _fontName = fontName;

            this.color = new Color(0, 0, 0, 0);
            this.alpha = 0f; // Starts invisible

            // 1. Build Static Elements
            _leftTrack = new Frame { anchorX = AnchorX.Left, anchorY = AnchorY.Center, color = Color.White };
            _rightTrack = new Frame { anchorX = AnchorX.Left, anchorY = AnchorY.Center, color = new Color(100, 100, 100, 255) };

            _judgementRing = new ImageFrame
            {
                texture = _circleTexture,
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                color = Color.White,
                fit = ObjectFit.Contain
            };

            // 2. Build Score & Combo UI
            _scoreUI = new TextFrame
            {
                fontName = _fontName,
                text = "0",
                color = Color.White,
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Top,
                position = new UDim2(0f, 0f, 20f, 20f)
            };

            _comboUI = new TextFrame
            {
                fontName = _fontName,
                text = "0x",
                color = new Color(138, 43, 226, 255), // Nice purple
                anchorX = AnchorX.Left,
                anchorY = AnchorY.Bottom,
                position = new UDim2(0f, 1f, 20f, -20f)
            };

            children.Add(_leftTrack);
            children.Add(_rightTrack);
            children.Add(_judgementRing);
            children.Add(_scoreUI);
            children.Add(_comboUI);
        }

        // --- Initialization ---
        public void LoadBeatmap(OsuBeatmap beatmap)
        {
            foreach (var note in _notes) children.Remove(note.VisualNode);
            _notes.Clear();
            _introAlpha = 0f;

            // Extract ONLY the timing data[cite: 9]
            foreach (OsuHitObject hitObject in beatmap.HitObjects)
            {
                var noteVisual = new ImageFrame
                {
                    texture = _circleTexture,
                    color = new Color(255, 235, 100, 255),
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Center,
                    fit = ObjectFit.Contain,
                    position = new UDim2(0f, 0.5f, 9999f, 0f)
                };

                _notes.Add(new TaikoNote
                {
                    TargetTimeMs = hitObject.Time,
                    HitSoundMask = hitObject.HitSound, // Grabs the bitmask from OsuLib[cite: 9]
                    VisualNode = noteVisual
                });

                children.Add(noteVisual);
            }
        }

        // --- Game Loop ---
        public void ResetState()
        {
            _introAlpha = 0f;
            this.alpha = 0f;

            Score = 0;
            Combo = 0;
            _scoreUI.text = "0";
            _comboUI.text = "0x";

            // Clean up all dynamically generated nodes
            foreach (var note in _notes) children.Remove(note.VisualNode);
            _notes.Clear();

            foreach (var fText in _floatingTexts) children.Remove(fText.Node);
            _floatingTexts.Clear();
        }
        public override void Draw(float dt, Vector2 parentSize, Vector2 parentOrigin)
        {
            if (this.alpha <= 0f) return;
            base.Draw(dt, parentSize, parentOrigin);
        }
        public void UpdatePlayfield(float dt, float currentAudioTimeMs)
        {
            // 1. Smooth Intro Fade-In
            if (_introAlpha < 1f)
            {
                _introAlpha = Math.Min(1f, _introAlpha + (dt * .025f));
                this.alpha = _introAlpha;
            }

            // 2. Dynamic Runtime Scaling
            float currentNoteScale = BaseNoteScale * GlobalScale;
            float currentJudgeOffsetX = BaseJudgementOffsetX * GlobalScale;

            _leftTrack.size = new UDim2(0f, 0f, currentJudgeOffsetX, 4f * GlobalScale);
            _leftTrack.position = new UDim2(0f, 0.5f, 0f, 0f);

            _rightTrack.size = new UDim2(1f, 0f, -currentJudgeOffsetX, 4f * GlobalScale);
            _rightTrack.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, 0f);

            _judgementRing.size = new UDim2(0f, 0f, currentNoteScale, currentNoteScale);
            _judgementRing.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, 0f);

            _scoreUI.scale = 2f * GlobalScale;
            _comboUI.scale = 1.8f * GlobalScale;

            // 3. Handle Esc to Menu
            if (Keyboard.IsKeyPressed(Keys.RightShift)) //[cite: 10]
            {
                OnExit?.Invoke();
                return;
            }

            // 4. Hit Detection Logic
            bool hitPressed = false;
            foreach (var key in HitKeys)
            {
                if (Keyboard.IsKeyPressed(key)) //[cite: 10]
                {
                    hitPressed = true;
                    break;
                }
            }

            if (hitPressed)
            {
                var targetNote = _notes.FirstOrDefault(n =>
                    !n.IsProcessed &&
                    Math.Abs(n.TargetTimeMs - currentAudioTimeMs) <= Window50);

                if (targetNote != null)
                {
                    targetNote.IsProcessed = true;
                    targetNote.IsHit = true;

                    float error = Math.Abs((float)targetNote.TargetTimeMs - currentAudioTimeMs);
                    int hitValue = 0;
                    Color hitColor = Color.White;

                    // Calculate Judgement
                    if (error <= Window300) { hitValue = 300; hitColor = new Color(50, 200, 255); }
                    else if (error <= Window100) { hitValue = 100; hitColor = new Color(100, 255, 100); }
                    else if (error <= Window50) { hitValue = 50; hitColor = new Color(255, 150, 50); }

                    // Apply Score & Sound
                    Combo++;
                    Score += hitValue * Combo;
                    _scoreUI.text = Score.ToString();
                    _comboUI.text = $"{Combo}x";

                    OnPlayHitSound?.Invoke(targetNote.HitSoundMask); // Fire sound event!

                    // Fling Upwards & slightly left
                    targetNote.Velocity = new Vector2(-150f, -600f) * GlobalScale;
                    targetNote.VisualNode.color = new Color(255, 235, 100, 255) * 0.4f;

                    // Spawn physical floating text
                    SpawnFloatingText(hitValue.ToString(), hitColor, targetNote.Velocity * 0.8f, currentJudgeOffsetX);
                }
            }

            // 5. Update Notes & Miss Detection
            foreach (var note in _notes)
            {
                if (!note.IsProcessed)
                {
                    if (currentAudioTimeMs > note.TargetTimeMs + Window50) // Miss
                    {
                        note.IsProcessed = true;
                        note.IsHit = false;

                        Combo = 0; // Break combo
                        _comboUI.text = $"{Combo}x";

                        note.Velocity = new Vector2(-150f, 600f) * GlobalScale; // Fling down
                        note.VisualNode.color = new Color(255, 235, 100, 255) * 0.4f;

                        SpawnFloatingText("X", new Color(255, 50, 50), note.Velocity * 0.8f, currentJudgeOffsetX);
                    }
                    else // Still Approaching
                    {
                        float timeDiff = (float)note.TargetTimeMs - currentAudioTimeMs;
                        note.CurrentOffset = timeDiff * ScrollSpeed * GlobalScale;
                    }
                }
                else // Apply Physics to flung notes
                {
                    note.Velocity += new Vector2(0f, 1500f * dt * GlobalScale); // Gravity
                    note.PositionOffset += note.Velocity * dt;
                    note.Alpha = Math.Max(0f, note.Alpha - (dt * 3f));
                    note.VisualNode.alpha = note.Alpha;
                }

                note.VisualNode.size = new UDim2(0f, 0f, currentNoteScale, currentNoteScale);
                note.VisualNode.position = new UDim2(0f, 0.5f, currentJudgeOffsetX + note.CurrentOffset + note.PositionOffset.X, note.PositionOffset.Y);
            }

            // 6. Update Floating Text Physics
            for (int i = _floatingTexts.Count - 1; i >= 0; i--)
            {
                var fText = _floatingTexts[i];
                fText.Velocity += new Vector2(0f, 1500f * dt * GlobalScale); // Gravity
                fText.PositionOffset += fText.Velocity * dt;
                fText.Alpha -= dt * 2.5f;

                fText.Node.alpha = Math.Max(0f, fText.Alpha);
                fText.Node.position = new UDim2(0f, 0.5f, fText.StartX + fText.PositionOffset.X, fText.PositionOffset.Y);

                if (fText.Alpha <= 0f)
                {
                    children.Remove(fText.Node);
                    _floatingTexts.RemoveAt(i);
                }
            }
        }

        private void SpawnFloatingText(string text, Color color, Vector2 velocity, float startX)
        {
            var textNode = new TextFrame
            {
                text = text,
                fontName = _fontName,
                color = color,
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center,
                scale = 1.8f * GlobalScale,
                position = new UDim2(0f, 0.5f, startX, 0f)
            };

            children.Add(textNode);

            _floatingTexts.Add(new FloatingText
            {
                Node = textNode,
                Velocity = velocity,
                StartX = startX
            });
        }

        // --- Internal Data Structures ---
        private class TaikoNote
        {
            public double TargetTimeMs { get; set; }
            public float CurrentOffset { get; set; }
            public int HitSoundMask { get; set; } // Tracks .osu bits (1, 2, 4, 8)[cite: 9]

            public bool IsProcessed { get; set; }
            public bool IsHit { get; set; }

            public Vector2 PositionOffset { get; set; } = Vector2.Zero;
            public Vector2 Velocity { get; set; } = Vector2.Zero;
            public float Alpha { get; set; } = 1f;
            public ImageFrame VisualNode { get; set; } = null!;
        }

        private class FloatingText
        {
            public TextFrame Node { get; set; } = null!;
            public Vector2 Velocity { get; set; } = Vector2.Zero;
            public Vector2 PositionOffset { get; set; } = Vector2.Zero;
            public float StartX { get; set; }
            public float Alpha { get; set; } = 1.5f; // Gives it a slight delay before fading starts
        }
    }
}