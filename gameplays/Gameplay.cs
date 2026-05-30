using System;
using System.Collections.Generic;
using System.Linq;
using ArtFrame.ArtTypes;
using ArtFrame.UserInterface;
using OsuLib;
using OsuLib.Models;

// Bind strictly to your custom wrappers!
using static ArtFrame.InputHelper;
using static ArtFrame.AudioHelper;

namespace CoreGame
{
    public class TaikoPlayfield : Frame
    {
        // --- Configuration ---
        public Keys[] HitKeys { get; set; } = { Keys.W, Keys.Q };
        public Keys ExitKey { get; set; } = Keys.RightShift;
        public float ScrollSpeed { get; set; } = 0.25f;
        public float GlobalScale { get; set; } = 1.0f; // Changes runtime UI sizing

        // Hit Windows (Milliseconds)
        public float Window300 { get; set; } = 35f;
        public float Window100 { get; set; } = 75f;
        public float Window50 { get; set; } = 100f; // This acts as the max HitWindow

        // --- State & Gameplay ---
        public int Score { get; private set; } = 0;
        public int Combo { get; private set; } = 0;
        private float _introAlpha = 0f; // Smooth fade-in tracker

        public int HitsPerfect { get; private set; } = 0;
        public int HitsGood { get; private set; } = 0;
        public int HitsOk { get; private set; } = 0;
        public int HitsMiss { get; private set; } = 0;
        public int MaxComboReached { get; private set; } = 0;

        // Custom Mode Toggle State: false = "Single!" (Taps), true = "Stream!" (Holds)
        private bool _isHoldMode = false;

        public float GetAccuracy()
        {
            int total = HitsPerfect + HitsGood + HitsOk + HitsMiss;
            if (total == 0) return 100f;
            
            float scorePoints = HitsPerfect * 300f + HitsGood * 100f + HitsOk * 50f;
            float maxPoints = total * 300f;
            return (scorePoints / maxPoints) * 100f;
        }

        // --- Events ---
        public Action<int>? OnPlayHitSound; // Hook your audio to this!
        public Action? OnExit; // Hook your main menu transition to this!
        public Action? OnPlayHoldTick { get; set; }

        // --- Visuals & Nodes ---
        private readonly Image _circleTexture;
        private readonly string _fontName;

        // Reverted back to a Single Track layout
        private Frame _leftTrack;
        private Frame _rightTrack;
        private ImageFrame _judgementRing;
        private float _judgementRingScale = 1.0f;

        private TextFrame _scoreUI;
        private TextFrame _comboUI;
        private TextFrame _modeUI; // Dynamic mode cue text

        private float BaseNoteScale = 40f;
        private float BaseJudgementOffsetX = 300f;

        private List<TaikoNote> _notes = new List<TaikoNote>();
        private List<FloatingText> _floatingTexts = new List<FloatingText>();

        // --- Hit Error Bar & Unstable Rate ---
        private Frame _hitErrorBarBg = null!;
        private Frame _hitErrorBarOk = null!;
        private Frame _hitErrorBarGood = null!;
        private Frame _hitErrorBarPerfect = null!;
        private Frame _avgIndicator = null!;
        private TextFrame _urText = null!;

        private List<HitErrorTick> _hitTicks = new List<HitErrorTick>();
        private List<double> _allHitErrors = new List<double>();
        private double _rollingAverageError = 0f;

        public TaikoPlayfield(Image circleTexture, string fontName = "gsans_bold")
        {
            _circleTexture = circleTexture;
            _fontName = fontName;

            this.color = new Color(0, 0, 0, 0);
            this.alpha = 0f; // Starts invisible

            // 1. Build Static Elements (Reverted to single line)
            _leftTrack = new Frame { anchorX = AnchorX.Left, anchorY = AnchorY.Center, color = Color.White };
            _rightTrack = new Frame { anchorX = AnchorX.Left, anchorY = AnchorY.Center, color = new Color(100, 100, 100, 255) };

            _judgementRing = new ImageFrame
            {
                texture = _circleTexture,
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                color = new Color(50, 180, 255, 180), // Blue-tinted ring initially
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

            // Visible Text showing current mode
            _modeUI = new TextFrame
            {
                fontName = _fontName,
                text = "SINGLE!",
                color = new Color(50, 180, 255, 255),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Center
            };

            children.Add(_leftTrack);
            children.Add(_rightTrack);
            children.Add(_judgementRing);
            children.Add(_scoreUI);
            children.Add(_comboUI);
            children.Add(_modeUI);

            // 3. Build Hit Error Bar & UR UI
            _hitErrorBarBg = new Frame
            {
                color = new Color(20, 20, 20, 160),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center
            };

            _hitErrorBarOk = new Frame
            {
                color = new Color(255, 150, 50, 60),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center
            };

            _hitErrorBarGood = new Frame
            {
                color = new Color(50, 220, 100, 100),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center
            };

            _hitErrorBarPerfect = new Frame
            {
                color = new Color(50, 150, 255, 160),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center
            };

            _avgIndicator = new Frame
            {
                color = Color.White,
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                alpha = 0f
            };

            _urText = new TextFrame
            {
                fontName = _fontName,
                text = "UR: --",
                color = new Color(220, 220, 220, 255),
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Bottom,
                textAnchorX = AnchorX.Center,
                textAnchorY = AnchorY.Bottom,
                scale = 1.0f
            };

            _hitErrorBarBg.children.Add(_hitErrorBarOk);
            _hitErrorBarBg.children.Add(_hitErrorBarGood);
            _hitErrorBarBg.children.Add(_hitErrorBarPerfect);
            _hitErrorBarBg.children.Add(_avgIndicator);
            _hitErrorBarBg.children.Add(_urText);

            children.Add(_hitErrorBarBg);
        }

        // --- Initialization ---
        public void LoadBeatmap(OsuBeatmap beatmap)
        {
            foreach (var note in _notes)
            {
                children.Remove(note.VisualNode);
                if (note.HoldBodyNode != null) children.Remove(note.HoldBodyNode);
            }
            _notes.Clear();
            _introAlpha = 0f;

            // Extract timing data & support hold notes
            foreach (OsuHitObject hitObject in beatmap.HitObjects)
            {
                bool isHold = hitObject.ObjectType == HitObjectType.Hold || hitObject.ObjectType == HitObjectType.Slider;
                double duration = 0;
                Frame? holdBody = null;

                if (isHold && hitObject is OsuSlider slider)
                {
                    duration = slider.DurationMs;
                    holdBody = new Frame
                    {
                        color = new Color(255, 120, 50, 100), // Orange-tinted hold tracks
                        anchorX = AnchorX.Left,
                        anchorY = AnchorY.Center,
                        position = new UDim2(0f, 0.5f, 9999f, 0f),
                        alpha = 0f
                    };
                    children.Add(holdBody); // Added first so it renders behind the note head
                }

                Color noteColor = isHold 
                    ? new Color(255, 120, 50, 255) // Red/Orange for Holds
                    : new Color(50, 200, 255, 255); // Blue for Taps

                var noteVisual = new ImageFrame
                {
                    texture = _circleTexture,
                    color = noteColor,
                    anchorX = AnchorX.Center,
                    anchorY = AnchorY.Center,
                    fit = ObjectFit.Contain,
                    position = new UDim2(0f, 0.5f, 9999f, 0f),
                    alpha = 0f
                };

                double scrollSpeedMult = beatmap.GetSliderVelocityAt(hitObject.Time) / 0.28;

                _notes.Add(new TaikoNote
                {
                    TargetTimeMs = hitObject.Time,
                    HitSoundMask = hitObject.HitSound,
                    VisualNode = noteVisual,
                    IsHold = isHold,
                    DurationMs = duration,
                    HoldBodyNode = holdBody,
                    VelocityMultiplier = scrollSpeedMult
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

            HitsPerfect = 0;
            HitsGood = 0;
            HitsOk = 0;
            HitsMiss = 0;
            MaxComboReached = 0;

            _isHoldMode = false;
            _modeUI.text = "SINGLE!";
            _modeUI.color = new Color(50, 180, 255, 255);
            _judgementRing.color = new Color(50, 180, 255, 180);

            // Clean up all dynamically generated nodes (including hold note tracks)
            foreach (var note in _notes)
            {
                children.Remove(note.VisualNode);
                if (note.HoldBodyNode != null) children.Remove(note.HoldBodyNode);
            }
            _notes.Clear();

            foreach (var fText in _floatingTexts) children.Remove(fText.Node);
            _floatingTexts.Clear();

            // Reset Hit Error Bar & Ticks state
            foreach (var tick in _hitTicks)
            {
                _hitErrorBarBg.children.Remove(tick.VisualNode);
            }
            _hitTicks.Clear();
            _allHitErrors.Clear();
            _rollingAverageError = 0f;
            _urText.text = "UR: --";
            
            _judgementRingScale = 1.0f;
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
                _introAlpha = Math.Min(1f, _introAlpha + (dt * 2.0f)); // Smooth 500ms fade-in
                this.alpha = _introAlpha;
            }

            // Propagate playfield alpha to static children
            _leftTrack.alpha = this.alpha;
            _rightTrack.alpha = this.alpha;
            _judgementRing.alpha = this.alpha;
            _scoreUI.alpha = this.alpha;
            _comboUI.alpha = this.alpha;
            _modeUI.alpha = this.alpha;

            // 2. Active Mouse Mode Toggle
            if (Mouse.LeftClicked())
            {
                _isHoldMode = false;
                PlaySFX("hover"); // satisfying dynamic mode swap tick
            }
            else if (Mouse.RightClicked())
            {
                _isHoldMode = true;
                PlaySFX("hover");
            }

            // Dynamic Runtime Scaling
            float currentNoteScale = BaseNoteScale * GlobalScale;
            float currentJudgeOffsetX = BaseJudgementOffsetX * GlobalScale;

            // Single Track Scaling
            _leftTrack.size = new UDim2(0f, 0f, currentJudgeOffsetX, 4f * GlobalScale);
            _leftTrack.position = new UDim2(0f, 0.5f, 0f, 0f);

            _rightTrack.size = new UDim2(1f, 0f, -currentJudgeOffsetX, 4f * GlobalScale);
            _rightTrack.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, 0f);

            // Smoothly decay the judgement ring pulse scale back to 1.0f
            _judgementRingScale += (1.0f - _judgementRingScale) * (dt * 15f);

            // Update Judgement Ring visual cue tint based on active mode
            _judgementRing.color = _isHoldMode 
                ? new Color(255, 120, 50, 180) // Red/Orange tint for Stream Mode
                : new Color(50, 180, 255, 180); // Blue tint for Single Mode

            _judgementRing.size = new UDim2(0f, 0f, currentNoteScale * _judgementRingScale, currentNoteScale * _judgementRingScale);
            _judgementRing.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, 0f);

            // Update Mode UI Text and Color
            _modeUI.text = _isHoldMode ? "STREAM!" : "SINGLE!";
            _modeUI.color = _isHoldMode ? new Color(255, 120, 50, 255) : new Color(50, 180, 255, 255);
            _modeUI.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, 55f * GlobalScale);
            _modeUI.scale = 1.3f * GlobalScale;

            _scoreUI.scale = 2f * GlobalScale;
            _comboUI.scale = 1.8f * GlobalScale;

            // Hit Error Bar Scaling
            float barW = 320f * GlobalScale;
            float barH = 8f * GlobalScale;

            _hitErrorBarBg.size = new UDim2(0f, 0f, barW, barH);
            _hitErrorBarBg.position = new UDim2(0.5f, 0f, 0.85f, 0f);
            _hitErrorBarBg.alpha = this.alpha;

            _hitErrorBarOk.size = new UDim2(1f, 0f, 1f, 0f);
            _hitErrorBarGood.size = new UDim2(Window100 / Window50, 0f, 1f, 0f);
            _hitErrorBarPerfect.size = new UDim2(Window300 / Window50, 0f, 1f, 0f);

            _avgIndicator.size = new UDim2(0f, 0f, 2f * GlobalScale, 18f * GlobalScale);
            float avgOffset = (float)(_rollingAverageError / Window50) * (barW * 0.5f);
            _avgIndicator.position = new UDim2(0.5f, 0.5f, avgOffset, 0f);
            _avgIndicator.alpha = _allHitErrors.Count > 0 ? this.alpha : 0f;

            _urText.position = new UDim2(0.5f, 0f, 0f, -8f * GlobalScale);
            _urText.scale = 0.9f * GlobalScale;
            _urText.alpha = this.alpha;

            // Update Hit Error Ticks
            for (int i = _hitTicks.Count - 1; i >= 0; i--)
            {
                var tick = _hitTicks[i];
                tick.Alpha -= dt * 0.5f; // Fades out over 2 seconds
                
                if (tick.Alpha <= 0f)
                {
                    _hitErrorBarBg.children.Remove(tick.VisualNode);
                    _hitTicks.RemoveAt(i);
                }
                else
                {
                    tick.VisualNode.alpha = tick.Alpha * this.alpha;
                    tick.VisualNode.size = new UDim2(0f, 0f, 2f * GlobalScale, 16f * GlobalScale);
                    float tickOffset = (tick.LocalError / Window50) * (barW * 0.5f);
                    tick.VisualNode.position = new UDim2(0.5f, 0.5f, tickOffset, 0f);
                }
            }

            // 3. Handle Esc to Menu
            if (Keyboard.IsKeyPressed(ExitKey))
            {
                OnExit?.Invoke();
                return;
            }

            // 4. Hit Detection Logic
            bool hitPressed = false;
            foreach (var key in HitKeys)
            {
                if (Keyboard.IsKeyPressed(key))
                {
                    hitPressed = true;
                    break;
                }
            }

            if (hitPressed)
            {
                _judgementRingScale = 1.22f; // Trigger responsive ring pulse

                var targetNote = _notes.FirstOrDefault(n =>
                    !n.IsProcessed &&
                    !n.IsHolding && // Don't re-target already holding notes
                    Math.Abs(n.TargetTimeMs - currentAudioTimeMs) <= Window50);

                if (targetNote != null)
                {
                    // Strict Mode Verification: Target note's hold type must match current toggled mode!
                    if (targetNote.IsHold != _isHoldMode)
                    {
                        // Wrong Mode -> Auto Miss!
                        targetNote.IsProcessed = true;
                        targetNote.IsHit = false;
                        HitsMiss++;

                        Combo = 0;
                        _comboUI.text = $"{Combo}x";

                        targetNote.Velocity = new Vector2(-150f, 600f) * GlobalScale; // Gravity fling down
                        targetNote.VisualNode.color = targetNote.VisualNode.color * 0.4f;

                        SpawnFloatingText("Wrong Mode!", new Color(255, 50, 50), targetNote.Velocity * 0.8f, currentJudgeOffsetX);
                    }
                    else
                    {
                        // Mode is correct! Proceed with hit calculations
                        float signedError = (float)currentAudioTimeMs - (float)targetNote.TargetTimeMs;
                        AddHitError(signedError);

                        if (targetNote.IsHold)
                        {
                            targetNote.IsHolding = true;
                            targetNote.HoldLastTickTime = currentAudioTimeMs;

                            OnPlayHitSound?.Invoke(targetNote.HitSoundMask);

                            Combo++;
                            Score += 150 * Combo;
                            _scoreUI.text = Score.ToString();
                            _comboUI.text = $"{Combo}x";

                            targetNote.Velocity = new Vector2(-150f, -400f) * GlobalScale;
                            SpawnFloatingText("Hold!", new Color(255, 150, 50), targetNote.Velocity * 0.8f, currentJudgeOffsetX);
                        }
                        else
                        {
                            targetNote.IsProcessed = true;
                            targetNote.IsHit = true;

                            float error = Math.Abs(signedError);
                            int hitValue = 0;
                            Color hitColor = Color.White;

                            if (error <= Window300) { hitValue = 300; hitColor = new Color(50, 200, 255); HitsPerfect++; }
                            else if (error <= Window100) { hitValue = 100; hitColor = new Color(100, 255, 100); HitsGood++; }
                            else if (error <= Window50) { hitValue = 50; hitColor = new Color(255, 150, 50); HitsOk++; }

                            Combo++;
                            Score += hitValue * Combo;
                            _scoreUI.text = Score.ToString();
                            _comboUI.text = $"{Combo}x";

                            OnPlayHitSound?.Invoke(targetNote.HitSoundMask);

                            targetNote.Velocity = new Vector2(-150f, -600f) * GlobalScale;
                            targetNote.VisualNode.color = targetNote.VisualNode.color * 0.4f;

                            SpawnFloatingText(hitValue.ToString(), hitColor, targetNote.Velocity * 0.8f, currentJudgeOffsetX);
                        }
                    }
                }
            }

            // 5. Update Notes & Miss/Holding Detection
            foreach (var note in _notes)
            {
                if (!note.IsProcessed)
                {
                    if (note.IsHolding)
                    {
                        // Check if the user is still holding one of the gameplay keys AND is in Stream mode
                        bool stillHolding = false;
                        foreach (var key in HitKeys)
                        {
                            if (Keyboard.IsKeyDown(key))
                            {
                                stillHolding = true;
                                break;
                            }
                        }

                        // Check if hold duration has finished successfully
                        if (currentAudioTimeMs >= note.TargetTimeMs + note.DurationMs)
                        {
                            note.IsProcessed = true;
                            note.IsHit = true;
                            note.IsHolding = false;
                            HitsPerfect++;

                            Combo++;
                            Score += 300 * Combo;
                            _scoreUI.text = Score.ToString();
                            _comboUI.text = $"{Combo}x";

                            OnPlayHitSound?.Invoke(note.HitSoundMask);

                            note.Velocity = new Vector2(-150f, -600f) * GlobalScale;
                            note.VisualNode.color = note.VisualNode.color * 0.4f;

                            SpawnFloatingText("Perfect!", new Color(50, 200, 255), note.Velocity * 0.8f, currentJudgeOffsetX);

                            if (note.HoldBodyNode != null)
                            {
                                children.Remove(note.HoldBodyNode);
                                note.HoldBodyNode = null;
                            }
                        }
                        else if (!stillHolding || !_isHoldMode) // Early release or switched modes mid-hold -> triggers miss checks!
                        {
                            note.IsProcessed = true;
                            note.IsHolding = false;

                            double heldTime = currentAudioTimeMs - note.TargetTimeMs;
                            double fraction = heldTime / note.DurationMs;

                            if (fraction >= 0.5 && stillHolding) // Held 1/2 or more -> Good!
                            {
                                note.IsHit = true;
                                HitsGood++;
                                Combo++;
                                Score += 200 * Combo;
                                _scoreUI.text = Score.ToString();
                                _comboUI.text = $"{Combo}x";

                                note.Velocity = new Vector2(-150f, -400f) * GlobalScale;
                                note.VisualNode.color = note.VisualNode.color * 0.4f;

                                SpawnFloatingText("Decent!", new Color(100, 255, 100), note.Velocity * 0.8f, currentJudgeOffsetX);
                            }
                            else if (fraction >= 0.333 && stillHolding) // Held 1/3 or more -> Ok!
                            {
                                note.IsHit = true;
                                HitsOk++;
                                Combo++;
                                Score += 50 * Combo;
                                _scoreUI.text = Score.ToString();
                                _comboUI.text = $"{Combo}x";

                                note.Velocity = new Vector2(-150f, -200f) * GlobalScale;
                                note.VisualNode.color = note.VisualNode.color * 0.4f;

                                SpawnFloatingText("Meh.", new Color(255, 235, 100), note.Velocity * 0.8f, currentJudgeOffsetX);
                            }
                            else // Held less than 1/3 or switched modes
                            {
                                note.IsHit = false;
                                HitsMiss++;
                                Combo = 0;
                                _comboUI.text = $"{Combo}x";

                                note.Velocity = new Vector2(-150f, 600f) * GlobalScale; // Fling down under gravity
                                note.VisualNode.color = note.VisualNode.color * 0.4f;

                                string rating = !_isHoldMode ? "Wrong Mode!" : "Let Go!";
                                SpawnFloatingText(rating, new Color(255, 50, 50), note.Velocity * 0.8f, currentJudgeOffsetX);
                            }

                            if (note.HoldBodyNode != null)
                            {
                                note.HoldBodyNode.color = note.HoldBodyNode.color * 0.4f;
                            }
                        }
                        else // Still successfully holding in Stream mode!
                        {
                            note.CurrentOffset = 0f;

                            // Passive score tick every 100ms
                            if (currentAudioTimeMs - note.HoldLastTickTime >= 100f)
                            {
                                Score += 10 * Combo;
                                _scoreUI.text = Score.ToString();
                                note.HoldLastTickTime = currentAudioTimeMs;
                                
                                OnPlayHoldTick?.Invoke();
                                _judgementRingScale = 1.08f; // satisfy pulse
                            }

                            // Shrink the hold bar from the left
                            if (note.HoldBodyNode != null)
                            {
                                float remainingTime = (float)(note.TargetTimeMs + note.DurationMs - currentAudioTimeMs);
                                float remainingWidth = Math.Max(0f, (float)(remainingTime * ScrollSpeed * note.VelocityMultiplier * GlobalScale));
                                note.HoldBodyNode.size = new UDim2(0f, 0f, remainingWidth, currentNoteScale * 0.7f);
                                note.HoldBodyNode.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, 0f);
                                note.HoldBodyNode.alpha = this.alpha;
                            }
                        }
                    }
                    else // Still approaching
                    {
                        if (currentAudioTimeMs > note.TargetTimeMs + Window50) // Passed start hit window
                        {
                            if (note.IsHold && !note.IsHolding)
                            {
                                // Ignored hold note / drumroll
                                if (note.CurrentOffset < -currentJudgeOffsetX - 100f)
                                {
                                    note.IsProcessed = true;
                                }
                                else
                                {
                                    float timeDiff = (float)note.TargetTimeMs - currentAudioTimeMs;
                                    note.CurrentOffset = (float)(timeDiff * ScrollSpeed * note.VelocityMultiplier * GlobalScale);
                                    note.VisualNode.alpha = this.alpha * Math.Clamp(1.0f + (note.CurrentOffset / currentJudgeOffsetX), 0f, 1f);
                                    
                                    if (note.HoldBodyNode != null)
                                    {
                                        float bodyWidth = (float)(note.DurationMs * ScrollSpeed * note.VelocityMultiplier * GlobalScale);
                                        note.HoldBodyNode.size = new UDim2(0f, 0f, bodyWidth, currentNoteScale * 0.7f);
                                        note.HoldBodyNode.position = new UDim2(0f, 0.5f, currentJudgeOffsetX + note.CurrentOffset + note.PositionOffset.X, note.PositionOffset.Y);
                                        note.HoldBodyNode.alpha = note.VisualNode.alpha;
                                    }
                                }
                            }
                            else
                            {
                                // Regular note Miss
                                note.IsProcessed = true;
                                note.IsHit = false;
                                HitsMiss++;

                                Combo = 0;
                                _comboUI.text = $"{Combo}x";

                                note.Velocity = new Vector2(-150f, 600f) * GlobalScale;
                                note.VisualNode.color = note.VisualNode.color * 0.4f;

                                SpawnFloatingText("Miss", new Color(255, 50, 50), note.Velocity * 0.8f, currentJudgeOffsetX);
                            }
                        }
                        else // Moving towards the judgement ring
                        {
                            float timeDiff = (float)note.TargetTimeMs - currentAudioTimeMs;
                            note.CurrentOffset = (float)(timeDiff * ScrollSpeed * note.VelocityMultiplier * GlobalScale);
                            note.VisualNode.alpha = this.alpha;

                            if (note.IsHold && note.HoldBodyNode != null)
                            {
                                float bodyWidth = (float)(note.DurationMs * ScrollSpeed * note.VelocityMultiplier * GlobalScale);
                                note.HoldBodyNode.size = new UDim2(0f, 0f, bodyWidth, currentNoteScale * 0.7f);
                                note.HoldBodyNode.position = new UDim2(0f, 0.5f, currentJudgeOffsetX + note.CurrentOffset + note.PositionOffset.X, note.PositionOffset.Y);
                                note.HoldBodyNode.alpha = this.alpha;
                            }
                        }
                    }
                }
                else // Physics gravity fling animations
                {
                    note.Velocity += new Vector2(0f, 1500f * dt * GlobalScale);
                    note.PositionOffset += note.Velocity * dt;
                    note.Alpha = Math.Max(0f, note.Alpha - (dt * 3f));
                    note.VisualNode.alpha = note.Alpha * this.alpha;

                    if (note.IsHold && note.HoldBodyNode != null)
                    {
                        note.HoldBodyNode.position = new UDim2(0f, 0.5f, currentJudgeOffsetX + note.CurrentOffset + note.PositionOffset.X, note.PositionOffset.Y);
                        note.HoldBodyNode.alpha = note.Alpha * this.alpha;
                    }
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

                fText.Node.alpha = Math.Max(0f, fText.Alpha) * this.alpha;
                fText.Node.position = new UDim2(0f, 0.5f, fText.StartX + fText.PositionOffset.X, fText.PositionOffset.Y);

                if (fText.Alpha <= 0f)
                {
                    children.Remove(fText.Node);
                    _floatingTexts.RemoveAt(i);
                }
            }

            // 7. Update Max Combo
            if (Combo > MaxComboReached) MaxComboReached = Combo;
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

        private void AddHitError(float errorMs)
        {
            errorMs = Math.Clamp(errorMs, -Window50, Window50);
            _allHitErrors.Add(errorMs);

            // Update rolling average error (last 20 hits)
            int avgCount = Math.Min(_allHitErrors.Count, 20);
            double sum = 0;
            for (int i = _allHitErrors.Count - avgCount; i < _allHitErrors.Count; i++)
            {
                sum += _allHitErrors[i];
            }
            _rollingAverageError = sum / avgCount;

            // Recalculate Unstable Rate (UR) = standard deviation * 10
            if (_allHitErrors.Count > 1)
            {
                double mean = _allHitErrors.Average();
                double variance = _allHitErrors.Select(x => (x - mean) * (x - mean)).Average();
                double stdDev = Math.Sqrt(variance);
                double ur = stdDev * 10.0;
                _urText.text = $"UR: {ur:F2}";
            }
            else
            {
                _urText.text = "UR: 0.00";
            }

            // Determine tick color based on window
            Color tickColor = new Color(255, 150, 50); // 50 Ok (Orange)
            float absErr = Math.Abs(errorMs);
            if (absErr <= Window300) tickColor = new Color(50, 200, 255); // 300 Perfect (Blue)
            else if (absErr <= Window100) tickColor = new Color(100, 255, 100); // 100 Good (Green)

            Frame tickNode = new Frame
            {
                color = tickColor,
                anchorX = AnchorX.Center,
                anchorY = AnchorY.Center,
                alpha = 1.0f
            };

            _hitErrorBarBg.children.Add(tickNode);

            _hitTicks.Add(new HitErrorTick
            {
                VisualNode = tickNode,
                LocalError = errorMs,
                Alpha = 1.0f
            });
        }

        // --- Data Structures ---
        private class HitErrorTick
        {
            public Frame VisualNode { get; set; } = null!;
            public float LocalError { get; set; }
            public float Alpha { get; set; } = 1.0f;
        }

        private class TaikoNote
        {
            public double TargetTimeMs { get; set; }
            public float CurrentOffset { get; set; }
            public int HitSoundMask { get; set; } // Tracks .osu bits (1, 2, 4, 8)

            public bool IsProcessed { get; set; }
            public bool IsHit { get; set; }

            // Hold note properties
            public bool IsHold { get; set; }
            public double DurationMs { get; set; }
            public Frame? HoldBodyNode { get; set; }
            public bool IsHolding { get; set; }
            public double HoldLastTickTime { get; set; }
            public double VelocityMultiplier { get; set; } = 1.0;

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