using System;
using ArtFrame;
using ArtFrame.ArtTypes;
using ArtFrame.Easings;
using ArtFrame.UserInterface;
using OsuLib;
using OsuLib.Models;

// Bind strictly to your custom wrappers!
using static ArtFrame.InputHelper;
using static ArtFrame.AudioHelper;
using static ArtFrame.GraphicsHelper;

namespace CoreGame
{
    public class TaikoPlayfield : Frame
    {
        // --- Configuration ---
        public Keys[] HitKeys { get; set; } = { Keys.W, Keys.Q, Keys.E, Keys.R };
        public Keys ExitKey { get; set; } = Keys.RightShift;
        public float ScrollSpeed { get; set; } = 0.25f;
        public float GlobalScale { get; set; } = 1.0f;
        public bool IsAutoplay { get; set; } = false;
        public bool SingleMode { get; set; } = false;

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
        public Action<int>? OnPlayHitSound;
        public Action? OnExit;
        public Action? OnPlayHoldTick;
        public Action? OnSplitFinished;

        // --- Visuals & Nodes ---
        private readonly Image _circleTexture;
        private readonly string _fontName;

        private bool _onSplitFinishedTriggered = false;
        private Frame _leftTrack;
        private Frame _rightTrack;
        private ImageFrame _topJudgementRing;
        private ImageFrame _bottomJudgementRing;
        private float _judgementRingScale = 1.0f;
        private float _splitProgress = 0f;
        private Vector2 _lastParentSize = new Vector2(1920f, 1080f);
        private Vector2 _lastParentOrigin = Vector2.Zero;
        private float _lastAudioTimeMs = 0f;

        // --- Retained-Mode Curved Tracks ---
        private ArtVertex[] _topCurveVertices = new ArtVertex[(240 + 1) * 2];
        private ArtVertex[] _bottomCurveVertices = new ArtVertex[(240 + 1) * 2];
        private ArtVertex[] _holdCurveVertices = new ArtVertex[(50 + 1) * 2];
        private float _lastCalculatedSplitProgress = -1f;
        private bool _lastCalculatedHoldMode = false;
        private float _lastCalculatedAlpha = -1f;

        private OsuBeatmap? _activeBeatmap;
        private bool _isKiaiActive = false;
        private float _kiaiScale = 1.0f;

        private TextFrame _scoreUI;
        private TextFrame _comboUI;
        private TextFrame _modeUI;

        private float BaseNoteScale = 40f;
        private float BaseJudgementOffsetX = 300f;
        private float CurvatureExponent = 0.25f;

        // --- Object Pooling & Zero-Allocation Data Structures ---
        private const int MaxVisualPool = 128;
        private ImageFrame[] _visualPool = new ImageFrame[MaxVisualPool];
        private bool[] _visualPoolInUse = new bool[MaxVisualPool];



        private const int MaxFloatingText = 50;
        private FloatingText[] _floatingTexts = new FloatingText[MaxFloatingText];

        // Flat array for gameplay notes to avoid GC allocations
        private GameplayNote[] _notes = new GameplayNote[0];
        private int _firstActiveIndex = 0;
        private int _nextSpawnIndex = 0;
        //private const float PreemptTime = 2000f; // Defines how early notes spawn visually

        // --- Hit Error Bar & Unstable Rate ---
        private Frame _hitErrorBarBg = null!;
        private Frame _hitErrorBarOk = null!;
        private Frame _hitErrorBarGood = null!;
        private Frame _hitErrorBarPerfect = null!;
        private Frame _avgIndicator = null!;
        private TextFrame _urText = null!;

        private const int MaxHitTicks = 64;
        private HitErrorTick[] _hitTicks = new HitErrorTick[MaxHitTicks];
        //private Frame[] _hitTickVisuals = new Frame[MaxHitTicks];

        // High performance UR tracking
        private double[] _hitErrors = new double[1000];
        private int _hitErrorCount = 0;
        private double _rollingAverageError = 0f;

        // Input state tracking
        private bool[] _previousKeyStates;

        public TaikoPlayfield(Image circleTexture, string fontName = "gsans_bold")
        {
            _circleTexture = circleTexture;
            _fontName = fontName;

            this.color = new Color(0, 0, 0, 0);
            this.alpha = 0f;

            _previousKeyStates = new bool[HitKeys.Length];

            // 1. Build Static Elements
            _leftTrack = new Frame { anchorX = AnchorX.Left, anchorY = AnchorY.Center, color = Color.White };
            _rightTrack = new Frame { anchorX = AnchorX.Left, anchorY = AnchorY.Center, color = new Color(100, 100, 100, 255) };

            _topJudgementRing = new ImageFrame { texture = _circleTexture, anchorX = AnchorX.Center, anchorY = AnchorY.Center, color = Color.White, fit = ObjectFit.Contain };
            _bottomJudgementRing = new ImageFrame { texture = _circleTexture, anchorX = AnchorX.Center, anchorY = AnchorY.Center, color = Color.White, fit = ObjectFit.Contain };

            _scoreUI = new TextFrame { fontName = _fontName, text = "0", color = Color.White, anchorX = AnchorX.Left, anchorY = AnchorY.Top, position = new UDim2(0f, 0f, 20f, 20f) };
            _comboUI = new TextFrame { fontName = _fontName, text = "0x", color = new Color(138, 43, 226, 255), anchorX = AnchorX.Left, anchorY = AnchorY.Bottom, position = new UDim2(0f, 1f, 20f, -20f) };
            _modeUI = new TextFrame { fontName = _fontName, text = "SINGLE!", color = new Color(50, 180, 255, 255), anchorX = AnchorX.Center, anchorY = AnchorY.Center, textAnchorX = AnchorX.Center, textAnchorY = AnchorY.Center };

            children.Add(_leftTrack);
            children.Add(_rightTrack); // Fixed typo from backup2
            children.Add(_topJudgementRing);
            children.Add(_bottomJudgementRing);
            children.Add(_scoreUI);
            children.Add(_comboUI);
            children.Add(_modeUI);

            // 2. Pre-Allocate Object Pools
            for (int i = 0; i < MaxVisualPool; i++)
            {
                _visualPool[i] = new ImageFrame { texture = _circleTexture, anchorX = AnchorX.Center, anchorY = AnchorY.Center, fit = ObjectFit.Contain, alpha = 0f };
                children.Add(_visualPool[i]);
            }

            // 3. Build Hit Error Bar
            _hitErrorBarBg = new Frame { color = new Color(20, 20, 20, 160), anchorX = AnchorX.Center, anchorY = AnchorY.Center };
            _hitErrorBarOk = new Frame { color = new Color(255, 150, 50, 60), anchorX = AnchorX.Center, anchorY = AnchorY.Center };
            _hitErrorBarGood = new Frame { color = new Color(50, 220, 100, 100), anchorX = AnchorX.Center, anchorY = AnchorY.Center };
            _hitErrorBarPerfect = new Frame { color = new Color(50, 150, 255, 160), anchorX = AnchorX.Center, anchorY = AnchorY.Center };
            _avgIndicator = new Frame { color = Color.White, anchorX = AnchorX.Center, anchorY = AnchorY.Center, alpha = 0f };
            _urText = new TextFrame { fontName = _fontName, text = "UR: --", color = new Color(220, 220, 220, 255), anchorX = AnchorX.Center, anchorY = AnchorY.Bottom, textAnchorX = AnchorX.Center, textAnchorY = AnchorY.Bottom, scale = 1.0f };

            _hitErrorBarBg.children.Add(_hitErrorBarOk);
            _hitErrorBarBg.children.Add(_hitErrorBarGood);
            _hitErrorBarBg.children.Add(_hitErrorBarPerfect);

            // Pre-allocate tick visuals
            //for (int i = 0; i < MaxHitTicks; i++)
            //{
            //    _hitTickVisuals[i] = new Frame { anchorX = AnchorX.Center, anchorY = AnchorY.Center, alpha = 0f };
            //    _hitErrorBarBg.children.Add(_hitTickVisuals[i]);
            //}

            _hitErrorBarBg.children.Add(_avgIndicator);
            _hitErrorBarBg.children.Add(_urText);
            children.Add(_hitErrorBarBg);
        }

        public void LoadBeatmap(OsuBeatmap beatmap)
        {
            _activeBeatmap = null; // Clear reference for GC before loading new map
            if (beatmap == null) return;

            _activeBeatmap = beatmap;
            ResetState();

            // Sync Hitkeys length
            if (_previousKeyStates.Length != HitKeys.Length)
                _previousKeyStates = new bool[HitKeys.Length];

            // --- ADD THIS BLOCK HERE ---
            // Convert HitKeys array to raw ints using an explicit cast to fix CS0266
            int[] rawIntKeys = new int[HitKeys.Length];
            for (int k = 0; k < HitKeys.Length; k++)
            {
                rawIntKeys[k] = (int)HitKeys[k]; // <--- The explicit cast!
            }
            RealTimeInputEngine.ConfigureKeys(rawIntKeys);
            // ----------------------------

            // Compute base velocity from the first timing point so that only green-line
            // SV changes affect scroll speed — BPM alone no longer distorts note speed.
            // Previously this was a hardcoded / 0.28 which made high-BPM maps scroll
            // disproportionately faster than low-BPM maps.
            double baseVelocity = 0.28; // safe fallback
            if (beatmap.TimingPoints.Count > 0)
            {
                baseVelocity = beatmap.GetSliderVelocityAt(beatmap.TimingPoints[0].Time);
                if (baseVelocity <= 0) baseVelocity = 0.28;
            }

            bool isMania = beatmap.Mode == 3;

            int noteCount = beatmap.HitObjects.Count;
            _notes = new GameplayNote[noteCount];

            for (int i = 0; i < noteCount; i++)
            {
                OsuHitObject hitObject = beatmap.HitObjects[i];

                // Determine if this object is a hold/sustained note:
                // - In mania (Mode 3): only explicit Hold notes are holds
                // - In other modes: Sliders are treated as holds, spinners as holds
                bool isHold;
                double duration = 0;

                if (isMania)
                {
                    isHold = hitObject.ObjectType == HitObjectType.Hold;
                    if (isHold && hitObject is OsuSlider maniaHold)
                        duration = maniaHold.DurationMs;
                }
                else
                {
                    isHold = hitObject.ObjectType == HitObjectType.Slider
                          || hitObject.ObjectType == HitObjectType.Spinner;

                    if (hitObject is OsuSlider slider)
                        duration = slider.DurationMs;
                    else if (hitObject is OsuNote spinnerNote && spinnerNote.ObjectType == HitObjectType.Spinner)
                        duration = spinnerNote.DurationMs;
                }

                double scrollSpeedMult = beatmap.GetSliderVelocityAt(hitObject.Time) / baseVelocity;

                double noteSpeed = ScrollSpeed * scrollSpeedMult * GlobalScale;
                double calculatedPreempt = Math.Clamp(ScreenWidth / noteSpeed, 100.0, 15000.0);

                _notes[i] = new GameplayNote
                {
                    TargetTimeMs = hitObject.Time,
                    HitSoundMask = hitObject.HitSound,
                    PreemptTimeMs = calculatedPreempt,
                    IsHold = isHold,
                    DurationMs = duration,
                    VelocityMultiplier = scrollSpeedMult,
                    Color = isHold ? new Color(255, 120, 50, 255) : new Color(50, 200, 255, 255),
                    IsActive = false,
                    IsProcessed = false,
                    VisualPoolIndex = -1,
                    Alpha = 0f // Start faded out for smooth entry
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

            HitsPerfect = 0;
            HitsGood = 0;
            HitsOk = 0;
            HitsMiss = 0;
            MaxComboReached = 0;

            _splitProgress = 0f;
            _isHoldMode = false;
            _modeUI.text = "SINGLE!";
            _modeUI.color = new Color(50, 180, 255, 255);
            _topJudgementRing.color = new Color(50, 180, 255, 180);
            _bottomJudgementRing.color = Color.White;
            _onSplitFinishedTriggered = false;

            _firstActiveIndex = 0;
            _nextSpawnIndex = 0;

            // Free all pools
            for (int i = 0; i < MaxVisualPool; i++) { _visualPoolInUse[i] = false; _visualPool[i].alpha = 0f; }
            for (int i = 0; i < MaxFloatingText; i++) { _floatingTexts[i].Alpha = 0f; }

            _hitErrorCount = 0;
            _rollingAverageError = 0f;
            _urText.text = "UR: --";
            _judgementRingScale = 1.0f;
        }

        public void UpdatePlayfield(float dt, float currentAudioTimeMs)
        {
            _lastAudioTimeMs = currentAudioTimeMs;

            // Compute Kiai state
            _isKiaiActive = false;
            _kiaiScale = 1.0f;
            /* Uncomment to restore Kiai logic
            if (_activeBeatmap != null)
            {
                var activePoint = _activeBeatmap.GetTimingPointAt(currentAudioTimeMs);
                if (activePoint != null && activePoint.IsKiai)
                {
                    _isKiaiActive = true;
                    var bpmPoint = _activeBeatmap.GetTimingPointAt(currentAudioTimeMs, uninheritedOnly: true);
                    if (bpmPoint != null)
                    {
                        double beatLength = bpmPoint.BeatLength;
                        double timeSinceLastBeat = (currentAudioTimeMs - bpmPoint.Time) % beatLength;
                        if (timeSinceLastBeat < 0) timeSinceLastBeat += beatLength;
                        double beatProgress = timeSinceLastBeat / beatLength;
                        _kiaiScale = 1.0f + 0.06f * (float)Math.Exp(-6.0 * beatProgress);
                    }
                }
            }
            */

            // 1. Transitions
            if (_introAlpha < 1f)
            {
                _introAlpha = Math.Min(1f, _introAlpha + (dt * 1.5f));
                alpha = _introAlpha;
            }
            else if (_splitProgress < 1f)
            {
                _splitProgress = Math.Min(1f, _splitProgress + (dt * 1.5f));
            }
            else if (_splitProgress > 0.98f && !_onSplitFinishedTriggered)
            {
                _onSplitFinishedTriggered = true;
                OnSplitFinished?.Invoke();
            }

            _leftTrack.alpha = 0f;
            _rightTrack.alpha = 0f;
            _topJudgementRing.alpha = alpha;
            _bottomJudgementRing.alpha = alpha;
            _scoreUI.alpha = alpha;
            _comboUI.alpha = alpha;
            _modeUI.alpha = alpha;

            // Rebuild retained-mode curve geometry if visual parameters change
            if (_splitProgress != _lastCalculatedSplitProgress || _isHoldMode != _lastCalculatedHoldMode || this.alpha != _lastCalculatedAlpha)
            {
                RebuildCurveVertices();
                _lastCalculatedSplitProgress = _splitProgress;
                _lastCalculatedHoldMode = _isHoldMode;
                _lastCalculatedAlpha = this.alpha;
            }

            // 2. Mouse Mode Toggle
            if (!SingleMode)
            {
                if (Mouse.LeftClicked()) { _isHoldMode = false; PlaySFX("hover"); }
                else if (Mouse.RightClicked()) { _isHoldMode = true; PlaySFX("hover"); }
            }

            // Dynamic Sizing
            float currentNoteScale = BaseNoteScale * GlobalScale;
            float currentJudgeOffsetX = BaseJudgementOffsetX * GlobalScale;
            float splitOffset = 25f * GlobalScale;

            _judgementRingScale += (1.0f - _judgementRingScale) * (dt * 15f);

            _topJudgementRing.color = !_isHoldMode ? new Color(50, 180, 255, 180) : new Color(255, 255, 255, 180);
            _bottomJudgementRing.color = _isHoldMode ? new Color(255, 120, 50, 180) : new Color(255, 255, 255, 180);

            _topJudgementRing.size = new UDim2(0f, 0f, currentNoteScale * _judgementRingScale * _kiaiScale, currentNoteScale * _judgementRingScale * _kiaiScale);
            _topJudgementRing.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, -splitOffset * _splitProgress);

            _bottomJudgementRing.size = new UDim2(0f, 0f, currentNoteScale * _judgementRingScale * _kiaiScale, currentNoteScale * _judgementRingScale * _kiaiScale);
            _bottomJudgementRing.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, splitOffset * _splitProgress);

            _modeUI.text = _isHoldMode ? "STREAM!" : "SINGLE!";
            _modeUI.color = _isHoldMode ? new Color(255, 120, 50, 255) : new Color(50, 180, 255, 255);
            _modeUI.position = new UDim2(0f, 0.5f, currentJudgeOffsetX, _isHoldMode ? (splitOffset * _splitProgress + 45f * GlobalScale) : (-splitOffset * _splitProgress - 45f * GlobalScale));
            _modeUI.scale = 1.3f * GlobalScale;

            _scoreUI.scale = 2f * GlobalScale;
            _comboUI.scale = 1.8f * GlobalScale;

            UpdateHitErrorBar(dt);

            if (Keyboard.IsKeyPressed(ExitKey)) { OnExit?.Invoke(); return; }

            // 3. Spawning Logic (Sliding Window into Pool)
            while (_nextSpawnIndex < _notes.Length && _notes[_nextSpawnIndex].TargetTimeMs - _notes[_nextSpawnIndex].PreemptTimeMs <= currentAudioTimeMs)
            {
                ref var note = ref _notes[_nextSpawnIndex];
                note.IsActive = true;
                note.VisualPoolIndex = RentVisualNode();

                _visualPool[note.VisualPoolIndex].color = note.Color;
                _nextSpawnIndex++;
            }

            // 4. Hit Detection Logic
            if (IsAutoplay)
            {
                for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
                {
                    ref var note = ref _notes[i];
                    if (!note.IsActive || note.IsProcessed || note.IsHolding) continue;

                    if (currentAudioTimeMs >= note.TargetTimeMs)
                    {
                        _isHoldMode = note.IsHold;
                        _judgementRingScale = 1.22f;

                        if (note.IsHold)
                        {
                            note.IsHolding = true;
                            note.HoldLastTickTime = currentAudioTimeMs;
                            OnPlayHitSound?.Invoke(note.HitSoundMask);
                            Combo++; Score += 150 * Combo;
                            _scoreUI.text = Score.ToString(); _comboUI.text = $"{Combo}x";
                            note.Velocity = new Vector2(-150f, -400f) * GlobalScale;
                            SpawnFloatingText("Hold!", new Color(255, 150, 50), note.Velocity * 0.8f, currentJudgeOffsetX, _bottomJudgementRing.position.OffsetY);
                        }
                        else
                        {
                            note.IsProcessed = true;
                            note.IsHit = true;
                            HitsPerfect++; Combo++; Score += 300 * Combo;
                            _scoreUI.text = Score.ToString(); _comboUI.text = $"{Combo}x";
                            OnPlayHitSound?.Invoke(note.HitSoundMask);
                            note.Velocity = new Vector2(-150f, -600f) * GlobalScale;
                            _visualPool[note.VisualPoolIndex].color *= 0.4f;
                            SpawnFloatingText("300", new Color(50, 200, 255), note.Velocity * 0.8f, currentJudgeOffsetX, _topJudgementRing.position.OffsetY);
                        }
                    }
                }
            }
            else
            {
                // Edge trigger input to prevent spam
                //int inputsThisFrame = 0;
                //for (int k = 0; k < HitKeys.Length; k++)
                //{
                //    bool isDown = Keyboard.IsKeyPressed(HitKeys[k]);
                //    if (isDown && !_previousKeyStates[k]) inputsThisFrame++;
                //    _previousKeyStates[k] = isDown;
                //}

                int inputsThisFrame = RealTimeInputEngine.ConsumePressCount();

                while (inputsThisFrame > 0)
                {
                    _judgementRingScale = 1.3f;

                    int targetIndex = -1;
                    float minDiff = float.MaxValue;

                    // Replaced LINQ with fast direct array scan
                    for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
                    {
                        ref var n = ref _notes[i];
                        if (n.IsActive && !n.IsProcessed && !n.IsHolding)
                        {
                            float diff = Math.Abs((float)(n.TargetTimeMs - currentAudioTimeMs));
                            if (diff <= Window50 && diff < minDiff)
                            {
                                minDiff = diff;
                                targetIndex = i;
                            }
                        }
                    }

                    if (targetIndex != -1)
                    {
                        ref var targetNote = ref _notes[targetIndex];
                        if (SingleMode) _isHoldMode = targetNote.IsHold;

                        if (targetNote.IsHold != _isHoldMode)
                        {
                            targetNote.IsProcessed = true; targetNote.IsHit = false; HitsMiss++;
                            Combo = 0; _comboUI.text = $"{Combo}x";
                            targetNote.Velocity = new Vector2(-150f, 600f) * GlobalScale;
                            _visualPool[targetNote.VisualPoolIndex].color *= 0.4f;
                            SpawnFloatingText("Wrong Mode!", new Color(255, 50, 50), targetNote.Velocity * 0.8f, currentJudgeOffsetX, targetNote.PositionOffset.Y);
                        }
                        else
                        {
                            float signedError = (float)currentAudioTimeMs - (float)targetNote.TargetTimeMs;
                            AddHitError(signedError);

                            if (targetNote.IsHold)
                            {
                                targetNote.IsHolding = true; targetNote.HoldLastTickTime = currentAudioTimeMs;
                                OnPlayHitSound?.Invoke(targetNote.HitSoundMask);
                                Combo++; Score += 150 * Combo;
                                _scoreUI.text = Score.ToString(); _comboUI.text = $"{Combo}x";
                                targetNote.Velocity = new Vector2(-150f, -400f) * GlobalScale;
                                SpawnFloatingText("Hold!", new Color(255, 150, 50), targetNote.Velocity * 0.8f, currentJudgeOffsetX, _topJudgementRing.position.OffsetY);
                            }
                            else
                            {
                                targetNote.IsProcessed = true; targetNote.IsHit = true;
                                float error = Math.Abs(signedError);
                                int hitValue = 0; Color hitColor = Color.White;

                                if (error <= Window300) { hitValue = 300; hitColor = new Color(50, 200, 255); HitsPerfect++; }
                                else if (error <= Window100) { hitValue = 100; hitColor = new Color(100, 255, 100); HitsGood++; }
                                else if (error <= Window50) { hitValue = 50; hitColor = new Color(255, 150, 50); HitsOk++; }

                                Combo++; Score += hitValue * Combo;
                                _scoreUI.text = Score.ToString(); _comboUI.text = $"{Combo}x";
                                OnPlayHitSound?.Invoke(targetNote.HitSoundMask);
                                targetNote.Velocity = new Vector2(-150f, -600f) * GlobalScale;
                                _visualPool[targetNote.VisualPoolIndex].color *= 0.4f;
                                SpawnFloatingText(hitValue.ToString(), hitColor, targetNote.Velocity * 0.8f, currentJudgeOffsetX, _bottomJudgementRing.position.OffsetY);
                            }
                        }
                    }
                    inputsThisFrame--;
                }
            }

            // 5. Update Notes & Physics
            for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
            {
                ref var note = ref _notes[i];
                if (!note.IsActive) continue;

                if (!note.IsProcessed || note.IsHolding)
                {
                    // Pre-calculate CurrentOffset for approaching notes before computing Y coordinates 
                    // to prevent a 1-frame visual lag/snap when first spawning or when clock jumps.
                    if (!note.IsHolding)
                    {
                        float timeDiff = (float)note.TargetTimeMs - currentAudioTimeMs;
                        note.CurrentOffset = (float)(timeDiff * ScrollSpeed * note.VelocityMultiplier * GlobalScale);
                    }

                    float noteX = currentJudgeOffsetX + note.CurrentOffset;
                    float noteY = 0f;
                    float splitPointX = _lastParentSize.X * 0.52f;

                    if (noteX <= splitPointX)
                    {
                        noteY = note.IsHold ? (splitOffset * _splitProgress) : (-splitOffset * _splitProgress);
                    }
                    else
                    {
                        float t = Math.Clamp((noteX - splitPointX) / (_lastParentSize.X - splitPointX), 0f, 1f);
                        float startY = note.IsHold ? splitOffset * _splitProgress : -splitOffset * _splitProgress;
                        float endY = note.IsHold ? _lastParentSize.Y * CurvatureExponent : -_lastParentSize.Y * CurvatureExponent;
                        noteY = startY + (endY - startY) * (t * t);
                    }

                    note.PositionOffset = new Vector2(0f, noteY);

                    if (note.IsHolding)
                    {
                        if (currentAudioTimeMs >= note.TargetTimeMs + note.DurationMs)
                        {
                            note.IsProcessed = true; note.IsHit = true; note.IsHolding = false;
                            HitsPerfect++; Combo++; Score += 300 * Combo;
                            _scoreUI.text = Score.ToString(); _comboUI.text = $"{Combo}x";
                            OnPlayHitSound?.Invoke(note.HitSoundMask);
                            note.Velocity = new Vector2(-150f, -600f) * GlobalScale;
                            _visualPool[note.VisualPoolIndex].color *= 0.4f;
                            SpawnFloatingText("Perfect!", new Color(50, 200, 255), note.Velocity * 0.8f, currentJudgeOffsetX, _bottomJudgementRing.position.OffsetY);
                        }
                        else
                        {
                            note.CurrentOffset = 0f;
                            if (note.Alpha < 1f)
                            {
                                note.Alpha = Math.Min(1f, note.Alpha + (dt / 0.15f)); // 150ms fade-in
                            }
                            _visualPool[note.VisualPoolIndex].alpha = note.Alpha * this.alpha; // Keep the head circle visible

                            bool stillHolding = IsAutoplay || RealTimeInputEngine.IsAnyKeyHeld();
                            bool canCatch = IsAutoplay || (stillHolding && (SingleMode || _isHoldMode));

                            // Frame-accurate, zero-allocation timer-based ticks evaluation (highly performant!)
                            float tickInterval = 100f; // Every 100ms
                            while (currentAudioTimeMs >= note.HoldLastTickTime + tickInterval)
                            {
                                note.HoldLastTickTime += tickInterval;

                                if (note.HoldLastTickTime > note.TargetTimeMs + note.DurationMs)
                                    break;

                                if (canCatch)
                                {
                                    Combo++; Score += 50 * Combo;
                                    _scoreUI.text = Score.ToString(); _comboUI.text = $"{Combo}x";
                                    //OnPlayHoldTick?.Invoke();
                                    _judgementRingScale = 1.15f;
                                    //SpawnFloatingText("yay!", new Color(186, 85, 211), new Vector2(20f, 90f) * GlobalScale, currentJudgeOffsetX, _bottomJudgementRing.position.OffsetY, 1.1f);
                                }
                                else
                                {
                                    HitsMiss++;
                                    Combo = 0; _comboUI.text = $"{Combo}x";
                                }
                            }
                        }
                    }
                    else // Approaching
                    {
                        if (currentAudioTimeMs > note.TargetTimeMs + Window50)
                        {
                            note.IsProcessed = true; note.IsHit = false; HitsMiss++;
                            Combo = 0; _comboUI.text = $"{Combo}x";

                            note.Velocity = new Vector2(-150f, 600f) * GlobalScale;
                            _visualPool[note.VisualPoolIndex].color *= 0.4f;
                            SpawnFloatingText("Miss", new Color(255, 50, 50), note.Velocity * 0.8f, currentJudgeOffsetX, _bottomJudgementRing.position.OffsetY);
                        }
                        else
                        {
                            if (note.Alpha < 1f)
                            {
                                note.Alpha = Math.Min(1f, note.Alpha + (dt / 0.15f)); // 150ms fade-in
                            }
                            _visualPool[note.VisualPoolIndex].alpha = note.Alpha * this.alpha;
                        }
                    }
                }
                else // Gravity Fling Physics
                {
                    note.Velocity += new Vector2(0f, 3200f * dt * GlobalScale);
                    note.PositionOffset += note.Velocity * dt;
                    note.Alpha = Math.Max(0f, note.Alpha - (dt * 3f));

                    _visualPool[note.VisualPoolIndex].alpha = note.Alpha * this.alpha;

                    // Recycle fully faded notes
                    if (note.Alpha <= 0f)
                    {
                        ReturnVisualNode(note.VisualPoolIndex);
                        note.IsActive = false;

                        // Advance window boundary
                        if (i == _firstActiveIndex) _firstActiveIndex++;
                        continue;
                    }
                }

                float finalNoteScale = currentNoteScale * (_isKiaiActive && !note.IsProcessed ? _kiaiScale : 1f);
                _visualPool[note.VisualPoolIndex].size = new UDim2(0f, 0f, finalNoteScale, finalNoteScale);
                _visualPool[note.VisualPoolIndex].position = new UDim2(0f, 0.5f, currentJudgeOffsetX + note.CurrentOffset + note.PositionOffset.X, note.PositionOffset.Y);
            }

            for (int i = 0; i < MaxFloatingText; i++)
            {
                if (_floatingTexts[i].Alpha > 0)
                {
                    _floatingTexts[i].Velocity += new Vector2(0f, 1500f * dt * GlobalScale);
                    _floatingTexts[i].PositionOffset += _floatingTexts[i].Velocity * dt;
                    _floatingTexts[i].Alpha -= dt * 2.5f;
                }
            }

            if (Combo > MaxComboReached) MaxComboReached = Combo;
        }

        public override void Draw(float dt, Vector2 parentSize, Vector2 parentOrigin)
        {
            if (this.alpha <= 0f) return;
            _lastParentSize = parentSize;
            _lastParentOrigin = parentOrigin;

            float currentJudgeOffsetX = BaseJudgementOffsetX * GlobalScale;
            float splitOffset = 25f * GlobalScale;
            float splitPointX = parentSize.X * 0.52f;
            float thickness = 4f * GlobalScale;
            float centerY = parentOrigin.Y + parentSize.Y * 0.5f;

            // 1. Draw Curved Hold Note Bodies (thick glowing ribbons)
            for (int i = _firstActiveIndex; i < _nextSpawnIndex; i++)
            {
                ref var note = ref _notes[i];
                if (!note.IsActive || !note.IsHold || (note.IsProcessed && !note.IsHolding)) continue;

                // Calculate parent-relative X coordinates
                float xHead = currentJudgeOffsetX;
                if (!note.IsHolding)
                {
                    xHead += note.CurrentOffset;
                }

                float bodyWidth = 0f;
                if (note.IsHolding)
                {
                    double remainingMs = (note.TargetTimeMs + note.DurationMs) - _lastAudioTimeMs;
                    bodyWidth = (float)(Math.Max(0.0, remainingMs) * ScrollSpeed * note.VelocityMultiplier * GlobalScale);
                }
                else
                {
                    bodyWidth = (float)(note.DurationMs * ScrollSpeed * note.VelocityMultiplier * GlobalScale);
                }

                float xTail = xHead + bodyWidth;

                // Clamp to visible playfield boundaries
                float xStart = Math.Clamp(xHead, 0f, parentSize.X);
                float xEnd = Math.Clamp(xTail, 0f, parentSize.X);

                if (xStart >= xEnd) continue;

                int segments = 50;
                float bodyThickness = 24f * GlobalScale;
                Color bodyColor = new Color(255, 120, 50, (byte)(140 * note.Alpha * this.alpha));

                for (int s = 0; s <= segments; s++)
                {
                    float localX = xStart + (s / (float)segments) * (xEnd - xStart);
                    float x = parentOrigin.X + localX;

                    float y = 0f;
                    if (localX <= splitPointX)
                    {
                        y = centerY + splitOffset * _splitProgress;
                    }
                    else
                    {
                        float t = (localX - splitPointX) / (parentSize.X - splitPointX);
                        float startY = centerY + splitOffset * _splitProgress;
                        float endY = centerY + parentSize.Y * CurvatureExponent;
                        y = startY + (endY - startY) * (t * t);
                    }

                    int idx = s * 2;
                    _holdCurveVertices[idx] = new ArtFrame.ArtTypes.ArtVertex(new Vector3(x, y - bodyThickness * 0.5f, 0f), bodyColor);
                    _holdCurveVertices[idx + 1] = new ArtFrame.ArtTypes.ArtVertex(new Vector3(x, y + bodyThickness * 0.5f, 0f), bodyColor);
                }

                GraphicsHelper.DrawTriangleStrip(_holdCurveVertices, (segments + 1) * 2);
            }

            // 2. Draw Retained-mode TriangleStrip curves (glowing wire down the center)
            GraphicsHelper.DrawTriangleStrip(_topCurveVertices, _topCurveVertices.Length);
            GraphicsHelper.DrawTriangleStrip(_bottomCurveVertices, _bottomCurveVertices.Length);

            // Manually Draw Floating Texts
            for (int i = 0; i < MaxFloatingText; i++)
            {
                if (_floatingTexts[i].Alpha > 0f)
                {
                    ref var fText = ref _floatingTexts[i];
                    float realAlpha = Math.Max(0f, Math.Min(1f, fText.Alpha)) * this.alpha;
                    Color finalCol = new Color(fText.Color.R, fText.Color.G, fText.Color.B, (byte)(realAlpha * 255));

                    float posX = parentOrigin.X + fText.StartX + fText.PositionOffset.X;
                    float posY = (ScreenHeight/2) + fText.PositionY + fText.PositionOffset.Y;

                    FontHelper.DrawTextPro(_fontName, fText.Text, new Vector2(posX, posY), new Vector2(0.5f, 0.5f), 0f, fText.Scale * 12f, finalCol);
                }
            }

            base.Draw(dt, parentSize, parentOrigin);
        }

        private void RebuildCurveVertices()
        {
            float splitOffset = 25f * GlobalScale;
            float thickness = 4f * GlobalScale;
            float centerY = _lastParentOrigin.Y + _lastParentSize.Y * 0.5f;

            byte trackAlpha = (byte)(180 * this.alpha);
            if (_isKiaiActive) trackAlpha = (byte)((180 + 50 * (_kiaiScale - 1.0f) / 0.06f) * this.alpha);

            Color topTrackColor = !_isHoldMode ? new Color(50, 180, 255, trackAlpha) : new Color(220, 220, 220, (byte)(trackAlpha * 0.6f));
            Color bottomTrackColor = _isHoldMode ? new Color(255, 120, 50, trackAlpha) : new Color(220, 220, 220, (byte)(trackAlpha * 0.6f));

            float splitPointX = _lastParentSize.X * 0.52f;
            int segments = 240;

            for (int i = 0; i <= segments; i++)
            {
                float localX = (i / (float)segments) * _lastParentSize.X;
                float x = _lastParentOrigin.X + localX;

                // 1. TOP CURVE
                float topY = 0f;
                if (localX <= splitPointX)
                {
                    topY = centerY - splitOffset * _splitProgress;
                }
                else
                {
                    float t = (localX - splitPointX) / (_lastParentSize.X - splitPointX);
                    float startY = centerY - splitOffset * _splitProgress;
                    float endY = centerY - _lastParentSize.Y * CurvatureExponent;
                    topY = startY + (endY - startY) * (t * t);
                }

                int topIdx = i * 2;
                _topCurveVertices[topIdx] = new ArtFrame.ArtTypes.ArtVertex(new Vector3(x, topY - thickness * 0.5f, 0f), topTrackColor);
                _topCurveVertices[topIdx + 1] = new ArtFrame.ArtTypes.ArtVertex(new Vector3(x, topY + thickness * 0.5f, 0f), topTrackColor);

                // 2. BOTTOM CURVE
                float bottomY = 0f;
                if (localX <= splitPointX)
                {
                    bottomY = centerY + splitOffset * _splitProgress;
                }
                else
                {
                    float t = (localX - splitPointX) / (_lastParentSize.X - splitPointX);
                    float startY = centerY + splitOffset * _splitProgress;
                    float endY = centerY + _lastParentSize.Y * CurvatureExponent;
                    bottomY = startY + (endY - startY) * (t * t);
                }

                int bottomIdx = i * 2;
                _bottomCurveVertices[bottomIdx] = new ArtFrame.ArtTypes.ArtVertex(new Vector3(x, bottomY - thickness * 0.5f, 0f), bottomTrackColor);
                _bottomCurveVertices[bottomIdx + 1] = new ArtFrame.ArtTypes.ArtVertex(new Vector3(x, bottomY + thickness * 0.5f, 0f), bottomTrackColor);
            }
        }

        // --- Optimized Subroutines ---
        private void SpawnFloatingText(string text, Color color, Vector2 velocity, float startX, float startY, float scale = 1.8f)
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

        private void AddHitError(float errorMs)
        {
            errorMs = Math.Clamp(errorMs, -Window50, Window50);

            _hitErrors[_hitErrorCount % _hitErrors.Length] = errorMs;
            _hitErrorCount++;

            int count = Math.Min(_hitErrorCount, 20);
            double sum = 0;
            for (int i = 0; i < count; i++) sum += _hitErrors[(_hitErrorCount - 1 - i) % _hitErrors.Length];
            _rollingAverageError = sum / count;

            int stdCount = Math.Min(_hitErrorCount, _hitErrors.Length);
            if (stdCount > 1)
            {
                double mean = 0;
                for (int i = 0; i < stdCount; i++) mean += _hitErrors[i];
                mean /= stdCount;

                double variance = 0;
                for (int i = 0; i < stdCount; i++) variance += (_hitErrors[i] - mean) * (_hitErrors[i] - mean);
                variance /= stdCount;

                _urText.text = $"UR: {(Math.Sqrt(variance) * 10.0):F2}";
            }

            for (int i = 0; i < MaxHitTicks; i++)
            {
                if (_hitTicks[i].Alpha <= 0f)
                {
                    _hitTicks[i].LocalError = errorMs;
                    _hitTicks[i].Alpha = 1.0f;

                    float absErr = Math.Abs(errorMs);
                    Color tCol = new Color(255, 150, 50);
                    if (absErr <= Window300) tCol = new Color(50, 200, 255);
                    else if (absErr <= Window100) tCol = new Color(100, 255, 100);

                    //_hitTickVisuals[i].color = tCol;
                    break;
                }
            }
        }

        private void UpdateHitErrorBar(float dt)
        {
            float barW = 320f * GlobalScale;
            float barH = 8f * GlobalScale;

            _hitErrorBarBg.size = new UDim2(0f, 0f, barW, barH);
            _hitErrorBarBg.position = new UDim2(0.5f, 0f, 0.85f, 0f);
            _hitErrorBarBg.alpha = this.alpha;

            _hitErrorBarOk.size = new UDim2(1f, 0f, 1f, 0f);
            _hitErrorBarGood.size = new UDim2(Window100 / Window50, 0f, 1f, 0f);
            _hitErrorBarPerfect.size = new UDim2(Window300 / Window50, 0f, 1f, 0f);

            _avgIndicator.size = new UDim2(0f, 0f, 2f * GlobalScale, 18f * GlobalScale);
            _avgIndicator.position = new UDim2(0.5f, 0.5f, (float)(_rollingAverageError / Window50) * (barW * 0.5f), 0f);
            _avgIndicator.alpha = _hitErrorCount > 0 ? this.alpha : 0f;

            _urText.position = new UDim2(0.5f, 0f, 0f, -8f * GlobalScale);
            _urText.scale = 0.9f * GlobalScale;
            _urText.alpha = this.alpha;

            for (int i = 0; i < MaxHitTicks; i++)
            {
                if (_hitTicks[i].Alpha > 0)
                {
                    _hitTicks[i].Alpha -= dt * 0.5f;
                    //_hitTickVisuals[i].alpha = Math.Max(0, _hitTicks[i].Alpha) * this.alpha;
                    //_hitTickVisuals[i].size = new UDim2(0f, 0f, 2f * GlobalScale, 16f * GlobalScale);
                    //_hitTickVisuals[i].position = new UDim2(0.5f, 0.5f, (_hitTicks[i].LocalError / Window50) * (barW * 0.5f), 0f);
                }
            }
        }

        private int RentVisualNode()
        {
            for (int i = 0; i < MaxVisualPool; i++)
            {
                if (!_visualPoolInUse[i]) { _visualPoolInUse[i] = true; return i; }
            }
            return 0; // Fallback
        }

        private void ReturnVisualNode(int idx)
        {
            if (idx >= 0 && idx < MaxVisualPool) { _visualPoolInUse[idx] = false; _visualPool[idx].alpha = 0f; _visualPool[idx].position = new UDim2(1.5f, 0f, 0f, 0f); }
        }

        // --- High Performance Data Structures ---
        private struct GameplayNote
        {
            public double TargetTimeMs;
            public float CurrentOffset;
            public int HitSoundMask;
            public bool IsHold;
            public double DurationMs;
            public double PreemptTimeMs;
            public double VelocityMultiplier;
            public Color Color;

            public bool IsActive;
            public bool IsProcessed;
            public bool IsHit;
            public bool IsHolding;
            public double HoldLastTickTime;

            public int VisualPoolIndex;

            public Vector2 PositionOffset;
            public Vector2 Velocity;
            public float Alpha;
        }

        private struct HitErrorTick
        {
            public float LocalError;
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