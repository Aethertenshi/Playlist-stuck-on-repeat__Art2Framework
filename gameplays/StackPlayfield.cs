using System;
using System.Collections.Generic;
using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.UserInterface;
using OsuLib;
using OsuLib.Models;

// Bind strictly to wrappers!
using static ArtFrame.InputHelper;
using static ArtFrame.AudioHelper;
using static ArtFrame.GraphicsHelper;

namespace CoreGame
{
    public class StackPlayfield : Frame
    {
        // --- Configuration ---
        public Keys[] HitKeys { get; set; } = { Keys.W, Keys.Q, Keys.E, Keys.R };
        public Keys ExitKey { get; set; } = Keys.RightShift;
        public float ScrollSpeed { get; set; } = 0.25f;
        public float GlobalScale { get; set; } = 1.0f;
        public bool IsAutoplay { get; set; } = false;

        // Hit Windows (Milliseconds)
        public float Window300 { get; set; } = 35f;
        public float Window100 { get; set; } = 75f;
        public float Window50 { get; set; } = 100f;

        // --- State & Gameplay ---
        public int Score { get; private set; } = 0;
        public int Combo { get; private set; } = 0;
        private float _introAlpha = 0f;

        public int HitsPerfect { get; private set; } = 0;
        public int HitsGood { get; private set; } = 0;
        public int HitsOk { get; private set; } = 0;
        public int HitsMiss { get; private set; } = 0;
        public int MaxComboReached { get; private set; } = 0;

        public float GetAccuracy()
        {
            int total = HitsPerfect + HitsGood + HitsOk + HitsMiss;
            if (total == 0) return 100f;

            float scorePoints = HitsPerfect * 300f + HitsGood * 100f + HitsOk * 50f;
            float maxPoints = total * 300f;
            return (scorePoints / maxPoints) * 100f;
        }

        // --- Events ---
        public Action<int>? OnPlayHitSound;
        public Action? OnExit;
        public Action? OnSplitFinished;

        public bool IsGameplayFinished => _notes != null && _notes.Length > 0 && _firstActiveIndex >= _notes.Length;

        // --- Visuals & Nodes ---
        private readonly Image _circleTexture;
        private readonly string _fontName;
        private bool _onSplitFinishedTriggered = false;

        private Vector2 _lastParentSize = new Vector2(1920f, 1080f);
        private Vector2 _lastParentOrigin = Vector2.Zero;
        private float _lastAudioTimeMs = 0f;

        // --- Stack & Physics State ---
        private struct PlacedBlock
        {
            public Vector3 Center;
            public Vector3 Size;
            public Color Color;
            public bool IsMiss;
            public float Alpha;
        }

        private struct FallingBlock
        {
            public Vector3 Center;
            public Vector3 Size;
            public Color Color;
            public Vector3 Velocity;
            public float Alpha;
        }

        private readonly List<PlacedBlock> _placedBlocks = new();
        private readonly List<FallingBlock> _fallingBlocks = new();

        private Vector2 _currentBlockSize = new Vector2(120f, 120f);
        private readonly Vector2 _initialBlockSize = new Vector2(120f, 120f);
        private const float BlockHeight = 20f;
        private float _blockScale = 1.6f;
        private float _cameraY = 0f;
        private float _targetCameraY = 0f;

        private int _consecutivePerfects = 0;

        private OsuBeatmap? _activeBeatmap;

        private TextFrame _scoreUI;
        private TextFrame _comboUI;
        private TextFrame _perfectStreakUI;

        // Object Pooling & Zero-Allocation Data Structures
        private const int MaxFloatingText = 50;
        private FloatingText[] _floatingTexts = new FloatingText[MaxFloatingText];

        private GameplayNote[] _notes = new GameplayNote[0];
        private int _firstActiveIndex = 0;
        private int _nextSpawnIndex = 0;
        private readonly long[] _framePressTimestamps = new long[32];
        public float MusicSpeedMultiplier { get; set; } = 1.0f;

        // Input state tracking
        private bool[] _previousKeyStates;

        public StackPlayfield(Image circleTexture, string fontName = "gsans_bold")
        {
            _circleTexture = circleTexture;
            _fontName = fontName;

            this.color = new Color(0, 0, 0, 0);
            this.alpha = 0f;

            _previousKeyStates = new bool[HitKeys.Length];

            _scoreUI = new TextFrame { fontName = _fontName, text = "0", color = Color.White, anchorX = AnchorX.Left, anchorY = AnchorY.Top, position = new UDim2(0f, 20f, 0f, 20f) };
            _comboUI = new TextFrame { fontName = _fontName, text = "0x", color = new Color(138, 43, 226, 255), anchorX = AnchorX.Left, anchorY = AnchorY.Bottom, position = new UDim2(0f, 20f, 1f, -20f) };
            _perfectStreakUI = new TextFrame { fontName = _fontName, text = "", color = new Color(255, 215, 0, 255), anchorX = AnchorX.Center, anchorY = AnchorY.Top, position = new UDim2(0.5f, 0f, 0f, 40f) };

            children.Add(_scoreUI);
            children.Add(_comboUI);
            children.Add(_perfectStreakUI);

            for (int i = 0; i < MaxFloatingText; i++)
            {
                _floatingTexts[i].Alpha = 0f;
            }
        }

        public void LoadBeatmap(OsuBeatmap? beatmap)
        {
            _activeBeatmap = null;
            if (beatmap == null) return;

            _activeBeatmap = beatmap;
            ResetState();

            if (_previousKeyStates.Length != HitKeys.Length)
                _previousKeyStates = new bool[HitKeys.Length];

            int[] rawIntKeys = new int[HitKeys.Length];
            for (int k = 0; k < HitKeys.Length; k++)
            {
                rawIntKeys[k] = (int)HitKeys[k];
            }
            RealTimeInputEngine.ConfigureKeys(rawIntKeys);

            int noteCount = beatmap.HitObjects.Count;
            _notes = new GameplayNote[noteCount];

            for (int i = 0; i < noteCount; i++)
            {
                OsuHitObject hitObject = beatmap.HitObjects[i];

                double calculatedPreempt = 1000.0; // Standard 1 second approach time

                // Shifting Rainbow Color Cycle based on index
                Color noteColor = ColorFromHSV((i * 18.0) % 360.0, 0.75, 0.9);

                _notes[i] = new GameplayNote
                {
                    TargetTimeMs = hitObject.Time,
                    HitSoundMask = hitObject.HitSound,
                    PreemptTimeMs = calculatedPreempt,
                    Color = noteColor,
                    IsActive = false,
                    IsProcessed = false,
                    Alpha = 0f
                };
            }
        }

        public void ResetState()
        {
            _introAlpha = 0f;
            this.alpha = 0f;

            Score = 0;
            Combo = 0;
            _scoreUI.text = "0";
            _comboUI.text = "0x";
            _perfectStreakUI.text = "";

            HitsPerfect = 0;
            HitsGood = 0;
            HitsOk = 0;
            HitsMiss = 0;
            MaxComboReached = 0;

            _consecutivePerfects = 0;
            _currentBlockSize = _initialBlockSize;
            _cameraY = 0f;
            _targetCameraY = 0f;

            _placedBlocks.Clear();
            _fallingBlocks.Clear();

            // Add the base block
            _placedBlocks.Add(new PlacedBlock
            {
                Center = new Vector3(0, 0, BlockHeight * 0.5f),
                Size = new Vector3(_initialBlockSize.X, _initialBlockSize.Y, BlockHeight),
                Color = new Color(138, 43, 226, 255), // Indigo Purple Base
                IsMiss = false,
                Alpha = 1.0f
            });

            _onSplitFinishedTriggered = false;

            _firstActiveIndex = 0;
            _nextSpawnIndex = 0;

            for (int i = 0; i < MaxFloatingText; i++) { _floatingTexts[i].Alpha = 0f; }
        }

        public void UpdatePlayfield(float dt, float currentAudioTimeMs)
        {
            _lastAudioTimeMs = currentAudioTimeMs;

            // 1. Transitions
            if (_introAlpha < 1f)
            {
                _introAlpha = Math.Min(1f, _introAlpha + (dt * 1.5f));
                alpha = _introAlpha;
                if (_introAlpha >= 0.98f && !_onSplitFinishedTriggered)
                {
                    _onSplitFinishedTriggered = true;
                    OnSplitFinished?.Invoke();
                }
            }

            _scoreUI.alpha = alpha;
            _comboUI.alpha = alpha;
            _perfectStreakUI.alpha = alpha;

            _scoreUI.scale = 2f * GlobalScale;
            _comboUI.scale = 1.8f * GlobalScale;
            _perfectStreakUI.scale = 1.5f * GlobalScale;

            if (Keyboard.IsKeyPressed(ExitKey)) { OnExit?.Invoke(); return; }

            // 2. Camera Tracking Lerp
            float stackHeight = _placedBlocks.Count * BlockHeight;
            _targetCameraY = stackHeight * _blockScale;
            _cameraY = ArtMathHelper.Lerp(_cameraY, _targetCameraY, dt * 5f);

            // 3. Spawning Logic
            while (_nextSpawnIndex < _notes.Length && _notes[_nextSpawnIndex].TargetTimeMs - _notes[_nextSpawnIndex].PreemptTimeMs <= currentAudioTimeMs)
            {
                _notes[_nextSpawnIndex].IsActive = true;
                _nextSpawnIndex++;
            }

            // 4. Input Processing
            if (IsAutoplay)
            {
                for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
                {
                    ref var note = ref _notes[i];
                    if (!note.IsActive || note.IsProcessed) continue;

                    if (currentAudioTimeMs >= note.TargetTimeMs)
                    {
                        PlaceBlock(i, 0f);
                    }
                }
            }
            else
            {
                long frameStartStopwatchMs = RealTimeInputEngine.GetCurrentTimestampMs();
                int inputsThisFrame = RealTimeInputEngine.ConsumePressTimestamps(_framePressTimestamps);

                // Process high-precision key bindings
                for (int k = 0; k < inputsThisFrame; k++)
                {
                    long pressStopwatchMs = _framePressTimestamps[k];
                    float elapsedSincePress = (float)(frameStartStopwatchMs - pressStopwatchMs);
                    float inputAudioTimeMs = currentAudioTimeMs - (elapsedSincePress * MusicSpeedMultiplier);

                    // Find the closest active note within the hit window of this input time
                    int targetIndex = -1;
                    float minDiff = float.MaxValue;

                    for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
                    {
                        ref var n = ref _notes[i];
                        if (n.IsActive && !n.IsProcessed)
                        {
                            float diff = Math.Abs((float)(n.TargetTimeMs - inputAudioTimeMs));
                            if (diff <= Window50 && diff < minDiff)
                            {
                                minDiff = diff;
                                targetIndex = i;
                            }
                        }
                    }

                    if (targetIndex != -1)
                    {
                        float timeDiff = inputAudioTimeMs - (float)_notes[targetIndex].TargetTimeMs;
                        PlaceBlock(targetIndex, timeDiff);
                    }
                }
            }

            // 5. Update approaching notes & check for missed ones
            for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
            {
                ref var note = ref _notes[i];
                if (!note.IsActive || note.IsProcessed) continue;

                if (currentAudioTimeMs > note.TargetTimeMs + Window50)
                {
                    // Miss due to passing the hit window
                    PlaceBlock(i, Window50 + 10f); // Triggers a Miss
                }
                else
                {
                    // Fade in approaching block
                    if (note.Alpha < 1f)
                    {
                        note.Alpha = Math.Min(1f, note.Alpha + (dt / 0.15f));
                    }
                }
            }

            // 6. Update Debris / Falling Blocks Physics
            for (int i = _fallingBlocks.Count - 1; i >= 0; i--)
            {
                var fb = _fallingBlocks[i];
                fb.Velocity.Z -= 980f * dt; // Gravity
                fb.Center = new Vector3(
                    fb.Center.X + fb.Velocity.X * dt,
                    fb.Center.Y + fb.Velocity.Y * dt,
                    fb.Center.Z + fb.Velocity.Z * dt
                );
                fb.Alpha -= dt * 1.5f;

                if (fb.Alpha <= 0f || fb.Center.Z < -500f)
                {
                    _fallingBlocks.RemoveAt(i);
                }
                else
                {
                    _fallingBlocks[i] = fb;
                }
            }

            // 7. Update UI texts
            if (Combo > MaxComboReached) MaxComboReached = Combo;

            _scoreUI.text = Score.ToString();
            _comboUI.text = $"{Combo}x";
            _perfectStreakUI.text = _consecutivePerfects > 0 ? $"STREAK: {_consecutivePerfects}" : "";
            if (_consecutivePerfects > 0)
            {
                // Dynamic golden color glow animation for streak!
                float pulse = 0.5f + 0.5f * MathF.Sin(currentAudioTimeMs * 0.01f);
                _perfectStreakUI.color = Color.LerpColor(new Color(255, 180, 0), new Color(255, 255, 100), pulse);
            }

            // 8. Update Floating Texts
            for (int i = 0; i < MaxFloatingText; i++)
            {
                if (_floatingTexts[i].Alpha > 0)
                {
                    _floatingTexts[i].Velocity += new Vector2(0f, 1500f * dt * GlobalScale);
                    _floatingTexts[i].PositionOffset += _floatingTexts[i].Velocity * dt;
                    _floatingTexts[i].Alpha -= dt * 2.5f;
                }
            }
        }



        private void PlaceBlock(int noteIndex, float timeDiff)
        {
            if (noteIndex < 0 || noteIndex >= _notes.Length) return;
            ref var note = ref _notes[noteIndex];
            if (note.IsProcessed) return;

            note.IsProcessed = true;

            // Advance the active window index
            if (noteIndex == _firstActiveIndex)
            {
                while (_firstActiveIndex < _notes.Length && _notes[_firstActiveIndex].IsProcessed)
                {
                    _firstActiveIndex++;
                }
            }

            // Get previous top block details
            var topBlock = _placedBlocks[_placedBlocks.Count - 1];

            // Progress: how far along the approach we were when hit
            // timeDiff is (inputTime - TargetTime)
            // positive = hit late, negative = hit early
            float progress = -timeDiff / (float)note.PreemptTimeMs;
            float offsetVal = progress * 300f; // Max approach starting offset is 300

            // Slicing Calculations
            bool isMovingX = (noteIndex % 2 == 0);
            float dx = 0f;
            float dy = 0f;

            if (isMovingX)
            {
                dx = (noteIndex % 4 == 0) ? offsetVal : -offsetVal;
            }
            else
            {
                dy = (noteIndex % 4 == 1) ? offsetVal : -offsetVal;
            }

            // Check hit classification based on timing and offset
            float absDiff = Math.Abs(timeDiff);
            bool isMiss = absDiff > Window50;

            if (isMiss)
            {
                // Missed!
                _consecutivePerfects = 0;
                Combo = 0;
                HitsMiss++;

                SpawnFloatingText("Miss", new Color(255, 50, 50), new Vector2(-150f, 600f) * GlobalScale, ScreenWidth * 0.5f, ScreenHeight * 0.35f);

                // Play miss SFX
                PlaySFX("hover");
                return;
            }

            // We hit! Determine the slice size
            float tolerance = 12f * GlobalScale; // Perfect hit snap tolerance
            bool isPerfect = false;

            if (isMovingX)
            {
                if (Math.Abs(dx) < tolerance)
                {
                    dx = 0f; // Snap to perfect
                    isPerfect = true;
                }
            }
            else
            {
                if (Math.Abs(dy) < tolerance)
                {
                    dy = 0f; // Snap to perfect
                    isPerfect = true;
                }
            }

            float newW_x = _currentBlockSize.X;
            float newW_y = _currentBlockSize.Y;
            float newCenter_x = topBlock.Center.X;
            float newCenter_y = topBlock.Center.Y;



            if (isMovingX)
            {
                if (Math.Abs(dx) >= _currentBlockSize.X)
                {
                    // Miss because block completely overshot the stack
                    isMiss = true;
                }
                else if (dx != 0f)
                {

                    newW_x = _currentBlockSize.X - Math.Abs(dx);
                    newCenter_x = topBlock.Center.X + dx * 0.5f;

                    // Debris slice geometry
                    float sliceW = Math.Abs(dx);
                    float sliceCenter_x = topBlock.Center.X + (dx > 0 ? (_currentBlockSize.X * 0.5f + dx * 0.5f) : (-_currentBlockSize.X * 0.5f + dx * 0.5f));

                    _fallingBlocks.Add(new FallingBlock
                    {
                        Center = new Vector3(sliceCenter_x, topBlock.Center.Y, topBlock.Center.Z + BlockHeight),
                        Size = new Vector3(sliceW, _currentBlockSize.Y, BlockHeight),
                        Color = note.Color,
                        Velocity = new Vector3(dx > 0 ? 150f : -150f, 0f, 120f),
                        Alpha = 1.0f
                    });
                }
            }
            else
            {
                if (Math.Abs(dy) >= _currentBlockSize.Y)
                {
                    isMiss = true;
                }
                else if (dy != 0f)
                {

                    newW_y = _currentBlockSize.Y - Math.Abs(dy);
                    newCenter_y = topBlock.Center.Y + dy * 0.5f;

                    // Debris slice geometry
                    float sliceW = Math.Abs(dy);
                    float sliceCenter_y = topBlock.Center.Y + (dy > 0 ? (_currentBlockSize.Y * 0.5f + dy * 0.5f) : (-_currentBlockSize.Y * 0.5f + dy * 0.5f));

                    _fallingBlocks.Add(new FallingBlock
                    {
                        Center = new Vector3(topBlock.Center.X, sliceCenter_y, topBlock.Center.Z + BlockHeight),
                        Size = new Vector3(_currentBlockSize.X, sliceW, BlockHeight),
                        Color = note.Color,
                        Velocity = new Vector3(0f, dy > 0 ? 150f : -150f, 120f),
                        Alpha = 1.0f
                    });
                }
            }

            if (isMiss)
            {
                _consecutivePerfects = 0;
                Combo = 0;
                HitsMiss++;
                SpawnFloatingText("Miss", new Color(255, 50, 50), new Vector2(-150f, 600f) * GlobalScale, ScreenWidth * 0.5f, ScreenHeight * 0.35f);
                PlaySFX("hover");
                return;
            }

            // Apply size changes
            _currentBlockSize = new Vector2(newW_x, newW_y);

            // Perfect hit handling
            if (isPerfect)
            {
                _consecutivePerfects++;
                HitsPerfect++;
                Combo++;
                Score += 300 * Combo;

                SpawnFloatingText("300!", new Color(50, 200, 255), new Vector2(-100f, -400f) * GlobalScale, ScreenWidth * 0.5f, ScreenHeight * 0.35f);

                // Grow mechanic: 8 perfects expansions
                if (_consecutivePerfects >= 8)
                {
                    _consecutivePerfects = 0;
                    _currentBlockSize.X = Math.Min(_initialBlockSize.X, _currentBlockSize.X + 10f);
                    _currentBlockSize.Y = Math.Min(_initialBlockSize.Y, _currentBlockSize.Y + 10f);
                    SpawnFloatingText("+Size!", new Color(0, 255, 120), new Vector2(0f, -600f) * GlobalScale, ScreenWidth * 0.5f, ScreenHeight * 0.25f, 1.3f);
                    PlaySFX("select");
                }
            }
            else
            {
                _consecutivePerfects = 0;
                if (absDiff <= Window100)
                {
                    HitsGood++;
                    Combo++;
                    Score += 100 * Combo;
                    SpawnFloatingText("100", new Color(100, 255, 100), new Vector2(-100f, -400f) * GlobalScale, ScreenWidth * 0.5f, ScreenHeight * 0.35f);
                }
                else
                {
                    HitsOk++;
                    Combo++;
                    Score += 50 * Combo;
                    SpawnFloatingText("50", new Color(255, 150, 50), new Vector2(-100f, -400f) * GlobalScale, ScreenWidth * 0.5f, ScreenHeight * 0.35f);
                }
            }

            // Add the new block
            _placedBlocks.Add(new PlacedBlock
            {
                Center = new Vector3(newCenter_x, newCenter_y, topBlock.Center.Z + BlockHeight),
                Size = new Vector3(_currentBlockSize.X, _currentBlockSize.Y, BlockHeight),
                Color = note.Color,
                IsMiss = false,
                Alpha = 1.0f
            });

            // Trigger hit sound
            OnPlayHitSound?.Invoke(note.HitSoundMask);
        }

        public override void Draw(float dt, Vector2 parentSize, Vector2 parentOrigin)
        {
            if (this.alpha <= 0f) return;
            _lastParentSize = parentSize;
            _lastParentOrigin = parentOrigin;

            // 1. Draw a soft dark ground shadow at the base of the tower
            // Calculated at Z = 0
            DrawGroundShadow();

            // 2. Draw placed blocks (Limit to top 35 blocks for high performance)
            int startIdx = Math.Max(0, _placedBlocks.Count - 35);
            for (int i = startIdx; i < _placedBlocks.Count; i++)
            {
                var block = _placedBlocks[i];
                DrawBlock(block.Center, block.Size, block.Color, block.Alpha * this.alpha);
            }

            // 3. Draw falling blocks (slice debris)
            for (int i = 0; i < _fallingBlocks.Count; i++)
            {
                var block = _fallingBlocks[i];
                DrawBlock(block.Center, block.Size, block.Color, block.Alpha * this.alpha);
            }

            // 4. Draw incoming approaching blocks
            DrawIncomingBlocks();

            // 5. Draw floating texts
            DrawFloatingTexts();

            base.Draw(dt, parentSize, parentOrigin);
        }

        private void DrawGroundShadow()
        {
            float size = 150f;
            Vector2 top = ProjectPoint(0f, -size, 0f);
            Vector2 right = ProjectPoint(size, 0f, 0f);
            Vector2 bottom = ProjectPoint(0f, size, 0f);
            Vector2 left = ProjectPoint(-size, 0f, 0f);

            Color shadowColor = new Color(0, 0, 0, (byte)(110 * this.alpha));
            DrawFace(top, right, bottom, left, shadowColor);
        }

        private void DrawIncomingBlocks()
        {
            for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
            {
                ref var note = ref _notes[i];
                if (!note.IsActive || note.IsProcessed) continue;

                // Target center is the center of the upcoming stack block
                var topBlock = _placedBlocks[_placedBlocks.Count - 1];
                float targetZ = topBlock.Center.Z + (i - _firstActiveIndex + 1) * BlockHeight;

                float progress = (float)(note.TargetTimeMs - _lastAudioTimeMs) / (float)note.PreemptTimeMs;
                float baseOffset = progress * 300f;

                bool isMovingX = (i % 2 == 0);
                float x = topBlock.Center.X;
                float y = topBlock.Center.Y;

                // Render the incoming block with transparency based on approach progress
                float approachAlpha = Math.Clamp(1.0f - progress, 0.0f, 1.0f);
                Vector3 size = new Vector3(_currentBlockSize.X, _currentBlockSize.Y, BlockHeight);

                // 1. Draw shrinking approach rhombus outline on top of the stack for this note
                float scale = 1.0f + Math.Max(0f, progress) * 1.5f;
                float hx = _currentBlockSize.X * 0.5f * scale;
                float hy = _currentBlockSize.Y * 0.5f * scale;

                float outlineZ = topBlock.Center.Z + BlockHeight;
                Vector2 p_top = ProjectPoint(topBlock.Center.X - hx, topBlock.Center.Y - hy, outlineZ);
                Vector2 p_right = ProjectPoint(topBlock.Center.X + hx, topBlock.Center.Y - hy, outlineZ);
                Vector2 p_bottom = ProjectPoint(topBlock.Center.X + hx, topBlock.Center.Y + hy, outlineZ);
                Vector2 p_left = ProjectPoint(topBlock.Center.X - hx, topBlock.Center.Y + hy, outlineZ);

                Color outlineCol = new Color(note.Color.R, note.Color.G, note.Color.B, (byte)(180 * approachAlpha * this.alpha));
                float lineThickness = 3f * GlobalScale;

                DrawLine(p_top, p_right, outlineCol, lineThickness);
                DrawLine(p_right, p_bottom, outlineCol, lineThickness);
                DrawLine(p_bottom, p_left, outlineCol, lineThickness);
                DrawLine(p_left, p_top, outlineCol, lineThickness);

                // 2. Draw Main Block sliding in
                if (isMovingX)
                {
                    x += (i % 4 == 0) ? baseOffset : -baseOffset;
                }
                else
                {
                    y += (i % 4 == 1) ? baseOffset : -baseOffset;
                }

                DrawBlock(new Vector3(x, y, targetZ), size, note.Color, approachAlpha * 0.75f * this.alpha);
            }
        }

        private Vector2 ProjectPoint(float x, float y, float z)
        {
            float screenCenterX = _lastParentOrigin.X + _lastParentSize.X * 0.5f;
            float screenCenterY = _lastParentOrigin.Y + _lastParentSize.Y * 0.55f;

            float sx = screenCenterX + (x - y) * 1.0f * _blockScale * GlobalScale;
            float sy = screenCenterY + (x + y) * 0.5f * _blockScale * GlobalScale - z * _blockScale * GlobalScale + _cameraY * GlobalScale;

            return new Vector2(sx, sy);
        }

        private void DrawBlock(Vector3 center, Vector3 size, Color color, float drawAlpha)
        {
            float hx = size.X * 0.5f;
            float hy = size.Y * 0.5f;
            float hz = size.Z * 0.5f;

            // Project 3D corners to 2D
            Vector2 t_top = ProjectPoint(center.X - hx, center.Y - hy, center.Z + hz);
            Vector2 t_right = ProjectPoint(center.X + hx, center.Y - hy, center.Z + hz);
            Vector2 t_bottom = ProjectPoint(center.X + hx, center.Y + hy, center.Z + hz);
            Vector2 t_left = ProjectPoint(center.X - hx, center.Y + hy, center.Z + hz);

            Vector2 lf_bottom_left = ProjectPoint(center.X - hx, center.Y + hy, center.Z - hz);
            Vector2 lf_top_left = ProjectPoint(center.X - hx, center.Y + hy, center.Z + hz);
            Vector2 lf_top_right = ProjectPoint(center.X + hx, center.Y + hy, center.Z + hz);
            Vector2 lf_bottom_right = ProjectPoint(center.X + hx, center.Y + hy, center.Z - hz);

            Vector2 rf_bottom_left = ProjectPoint(center.X + hx, center.Y - hy, center.Z - hz);
            Vector2 rf_top_left = ProjectPoint(center.X + hx, center.Y - hy, center.Z + hz);
            Vector2 rf_top_right = ProjectPoint(center.X + hx, center.Y + hy, center.Z + hz);
            Vector2 rf_bottom_right = ProjectPoint(center.X + hx, center.Y + hy, center.Z - hz);

            // Color shading to give 3D block volume
            Color topCol = new Color(color.R, color.G, color.B, (byte)(255 * drawAlpha));
            Color leftCol = new Color((byte)(color.R * 0.85f), (byte)(color.G * 0.85f), (byte)(color.B * 0.85f), (byte)(255 * drawAlpha));
            Color rightCol = new Color((byte)(color.R * 0.70f), (byte)(color.G * 0.70f), (byte)(color.B * 0.70f), (byte)(255 * drawAlpha));

            // Draw Top Face
            DrawFace(t_top, t_right, t_bottom, t_left, topCol);

            // Draw Front-Left Face
            DrawFace(lf_bottom_left, lf_top_left, lf_top_right, lf_bottom_right, leftCol);

            // Draw Front-Right Face
            DrawFace(rf_bottom_left, rf_top_left, rf_top_right, rf_bottom_right, rightCol);
        }

        private void DrawFace(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4, Color col)
        {
            ArtVertex[] faceVerts = new ArtVertex[4];
            faceVerts[0] = new ArtVertex(new Vector3(p1.X, p1.Y, 0), col);
            faceVerts[1] = new ArtVertex(new Vector3(p2.X, p2.Y, 0), col);
            faceVerts[2] = new ArtVertex(new Vector3(p4.X, p4.Y, 0), col);
            faceVerts[3] = new ArtVertex(new Vector3(p3.X, p3.Y, 0), col);

            GraphicsHelper.DrawTriangleStrip(faceVerts, 4);
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
        {
            float len = Vector2.Distance(start, end);
            float angle = MathF.Atan2(end.Y - start.Y, end.X - start.X);
            DrawRectanglePro(start, new Vector2(len, thickness), new Vector2(0f, thickness * 0.5f), angle, color);
        }

        private void DrawFloatingTexts()
        {
            for (int i = 0; i < MaxFloatingText; i++)
            {
                if (_floatingTexts[i].Alpha > 0f)
                {
                    ref var fText = ref _floatingTexts[i];
                    float realAlpha = Math.Max(0f, Math.Min(1f, fText.Alpha)) * this.alpha;
                    Color finalCol = new Color(fText.Color.R, fText.Color.G, fText.Color.B, (byte)(realAlpha * 255));

                    float posX = fText.StartX + fText.PositionOffset.X;
                    float posY = fText.PositionY + fText.PositionOffset.Y;

                    FontHelper.DrawTextPro(_fontName, fText.Text, new Vector2(posX, posY), new Vector2(0.5f, 0.5f), 0f, fText.Scale * 14f, finalCol);
                }
            }
        }

        private void SpawnFloatingText(string text, Color color, Vector2 velocity, float startX, float startY, float scale = 1f)
        {
            for (int i = 0; i < MaxFloatingText; i++)
            {
                if (_floatingTexts[i].Alpha <= 0)
                {
                    _floatingTexts[i].Text = text;
                    _floatingTexts[i].Color = color;
                    _floatingTexts[i].Velocity = velocity;
                    _floatingTexts[i].StartX = startX;
                    _floatingTexts[i].PositionY = startY;
                    _floatingTexts[i].Scale = scale * GlobalScale;
                    _floatingTexts[i].PositionOffset = Vector2.Zero;
                    _floatingTexts[i].Alpha = 1.5f;
                    break;
                }
            }
        }

        // --- Color conversion helper ---
        private static Color ColorFromHSV(double hue, double saturation, double value)
        {
            int hi = Convert.ToInt32(Math.Floor(hue / 60)) % 6;
            double f = hue / 60 - Math.Floor(hue / 60);

            value = value * 255;
            byte v = Convert.ToByte(value);
            byte p = Convert.ToByte(value * (1 - saturation));
            byte q = Convert.ToByte(value * (1 - f * saturation));
            byte t = Convert.ToByte(value * (1 - (1 - f) * saturation));

            if (hi == 0)
                return new Color(v, t, p, 255);
            else if (hi == 1)
                return new Color(q, v, p, 255);
            else if (hi == 2)
                return new Color(p, v, t, 255);
            else if (hi == 3)
                return new Color(p, q, v, 255);
            else if (hi == 4)
                return new Color(t, p, v, 255);
            else
                return new Color(v, p, q, 255);
        }

        // --- Retained-Mode Data Structures ---
        private struct GameplayNote
        {
            public double TargetTimeMs;
            public int HitSoundMask;
            public double PreemptTimeMs;
            public Color Color;
            public bool IsActive;
            public bool IsProcessed;
            public float Alpha;
        }

        private struct FloatingText
        {
            public string Text;
            public Color Color;
            public Vector2 Velocity;
            public Vector2 PositionOffset;
            public float StartX;
            public float PositionY;
            public float Scale;
            public float Alpha;
        }
    }
}
