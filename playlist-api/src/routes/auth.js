const express = require("express");
const { supabaseAdmin } = require("../supabase");

const router = express.Router();

/**
 * POST /auth/register
 * Body: { email, password, username }
 * Creates a Supabase Auth user + inserts into the players table.
 */
router.post("/register", async (req, res) => {
  const { email, password, username } = req.body;

  if (!email || !password || !username) {
    return res.status(400).json({ error: "email, password, and username are required" });
  }

  if (username.length < 3 || username.length > 20) {
    return res.status(400).json({ error: "Username must be 3-20 characters" });
  }

  try {
    // 1. Create auth user
    const { data: authData, error: authError } = await supabaseAdmin.auth.admin.createUser({
      email,
      password,
      email_confirm: true, // auto-confirm for game clients
    });

    if (authError) {
      return res.status(400).json({ error: authError.message });
    }

    // 2. Insert into players table
    const { error: playerError } = await supabaseAdmin
      .from("players")
      .insert({ id: authData.user.id, username });

    if (playerError) {
      // Rollback: delete the auth user if player insert fails
      await supabaseAdmin.auth.admin.deleteUser(authData.user.id);
      return res.status(400).json({ error: playerError.message });
    }

    return res.status(201).json({
      message: "Account created",
      user_id: authData.user.id,
      username,
    });
  } catch (err) {
    console.error("Register error:", err);
    return res.status(500).json({ error: "Internal server error" });
  }
});

/**
 * POST /auth/login
 * Body: { email, password }
 * Returns a JWT access token + user info.
 */
router.post("/login", async (req, res) => {
  const { email, password } = req.body;

  if (!email || !password) {
    return res.status(400).json({ error: "email and password are required" });
  }

  try {
    const { data, error } = await supabaseAdmin.auth.signInWithPassword({
      email,
      password,
    });

    if (error) {
      return res.status(401).json({ error: "Invalid email or password" });
    }

    // Fetch the player's username
    const { data: player } = await supabaseAdmin
      .from("players")
      .select("username")
      .eq("id", data.user.id)
      .single();

    return res.json({
      token: data.session.access_token,
      refresh_token: data.session.refresh_token,
      user_id: data.user.id,
      username: player?.username ?? "Unknown",
      expires_in: data.session.expires_in,
    });
  } catch (err) {
    console.error("Login error:", err);
    return res.status(500).json({ error: "Internal server error" });
  }
});

/**
 * POST /auth/refresh
 * Body: { refresh_token }
 * Returns a fresh access token.
 */
router.post("/refresh", async (req, res) => {
  const { refresh_token } = req.body;

  if (!refresh_token) {
    return res.status(400).json({ error: "refresh_token is required" });
  }

  try {
    const { data, error } = await supabaseAdmin.auth.refreshSession({
      refresh_token,
    });

    if (error) {
      return res.status(401).json({ error: "Invalid or expired refresh token" });
    }

    return res.json({
      token: data.session.access_token,
      refresh_token: data.session.refresh_token,
      expires_in: data.session.expires_in,
    });
  } catch (err) {
    console.error("Refresh error:", err);
    return res.status(500).json({ error: "Internal server error" });
  }
});

module.exports = router;
