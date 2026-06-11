const express = require("express");
const { supabaseAdmin } = require("../supabase");
const { requireAuth } = require("../middleware/auth");

const router = express.Router();

/**
 * POST /scores/submit
 * Requires: Bearer token
 * Body: { beatmap_id, score, max_combo, hits_300, hits_200, hits_50, misses, gameplay_mode }
 */
router.post("/submit", requireAuth, async (req, res) => {
  const { beatmap_id, score, max_combo, hits_300, hits_200, hits_50, misses, gameplay_mode } = req.body;

  // Validate required fields
  if (beatmap_id == null || score == null) {
    return res.status(400).json({ error: "beatmap_id and score are required" });
  }

  // Validate types
  if (!Number.isInteger(beatmap_id) || !Number.isInteger(score)) {
    return res.status(400).json({ error: "beatmap_id and score must be integers" });
  }

  // Validate gameplay mode
  const validModes = ["taiko", "stack"];
  const mode = gameplay_mode ?? "taiko";
  if (!validModes.includes(mode)) {
    return res.status(400).json({ error: "gameplay_mode must be 'taiko' or 'stack'" });
  }

  try {
    const { data, error } = await supabaseAdmin
      .from("scores")
      .insert({
        player_id: req.user.id,
        beatmap_id,
        score,
        max_combo: max_combo ?? 0,
        hits_300: hits_300 ?? 0,
        hits_200: hits_200 ?? 0,
        hits_50: hits_50 ?? 0,
        misses: misses ?? 0,
        gameplay_mode: mode,
      })
      .select("id, submitted_at")
      .single();

    if (error) {
      console.error("Score insert error:", error);
      return res.status(500).json({ error: "Failed to save score" });
    }

    return res.status(201).json({
      message: "Score submitted",
      score_id: data.id,
      submitted_at: data.submitted_at,
    });
  } catch (err) {
    console.error("Score submit error:", err);
    return res.status(500).json({ error: "Internal server error" });
  }
});

/**
 * GET /scores/:beatmapId
 * Query params: ?mode=taiko|stack&limit=50
 * Returns top scores for a beatmap, sorted by score descending.
 */
router.get("/:beatmapId", async (req, res) => {
  const beatmapId = parseInt(req.params.beatmapId);
  const mode = req.query.mode ?? "taiko";
  const limit = Math.min(parseInt(req.query.limit) || 50, 100);

  if (isNaN(beatmapId)) {
    return res.status(400).json({ error: "Invalid beatmap ID" });
  }

  try {
    const { data, error } = await supabaseAdmin
      .from("scores")
      .select(`
        id,
        score,
        max_combo,
        hits_300,
        hits_200,
        hits_50,
        misses,
        gameplay_mode,
        submitted_at,
        players ( username )
      `)
      .eq("beatmap_id", beatmapId)
      .eq("gameplay_mode", mode)
      .order("score", { ascending: false })
      .limit(limit);

    if (error) {
      console.error("Leaderboard fetch error:", error);
      return res.status(500).json({ error: "Failed to fetch leaderboard" });
    }

    // Flatten the player join
    const leaderboard = data.map((row, index) => ({
      rank: index + 1,
      username: row.players?.username ?? "Unknown",
      score: row.score,
      max_combo: row.max_combo,
      hits_300: row.hits_300,
      hits_200: row.hits_200,
      hits_50: row.hits_50,
      misses: row.misses,
      submitted_at: row.submitted_at,
    }));

    return res.json({ beatmap_id: beatmapId, mode, leaderboard });
  } catch (err) {
    console.error("Leaderboard error:", err);
    return res.status(500).json({ error: "Internal server error" });
  }
});

/**
 * GET /scores/player/:playerId
 * Query params: ?limit=20
 * Returns recent scores for a specific player.
 */
router.get("/player/:playerId", async (req, res) => {
  const playerId = req.params.playerId;
  const limit = Math.min(parseInt(req.query.limit) || 20, 100);

  try {
    const { data, error } = await supabaseAdmin
      .from("scores")
      .select("id, beatmap_id, score, max_combo, hits_300, hits_200, hits_50, misses, gameplay_mode, submitted_at")
      .eq("player_id", playerId)
      .order("submitted_at", { ascending: false })
      .limit(limit);

    if (error) {
      console.error("Player scores error:", error);
      return res.status(500).json({ error: "Failed to fetch player scores" });
    }

    return res.json({ player_id: playerId, scores: data });
  } catch (err) {
    console.error("Player scores error:", err);
    return res.status(500).json({ error: "Internal server error" });
  }
});

module.exports = router;
