require("dotenv").config();

const express = require("express");
const cors = require("cors");
const helmet = require("helmet");
const rateLimit = require("express-rate-limit");

const authRoutes = require("./routes/auth");
const scoreRoutes = require("./routes/scores");

const app = express();
const PORT = process.env.PORT || 3000;

// ── Middleware ────────────────────────────────────────────────────────────────

// Security headers
app.use(helmet());

// CORS — allow requests from anywhere (game client is a desktop app, not a browser)
app.use(cors());

// Parse JSON bodies
app.use(express.json());

// Rate limiting — prevent spam
const scoreLimiter = rateLimit({
  windowMs: 60 * 1000, // 1 minute
  max: 10,             // max 10 score submissions per minute per IP
  message: { error: "Too many requests, slow down" },
});

const authLimiter = rateLimit({
  windowMs: 15 * 60 * 1000, // 15 minutes
  max: 20,                  // max 20 auth attempts per 15 min per IP
  message: { error: "Too many auth attempts, try again later" },
});

// ── Routes ───────────────────────────────────────────────────────────────────

app.use("/auth", authLimiter, authRoutes);
app.use("/scores", scoreLimiter, scoreRoutes);

// Health check
app.get("/", (req, res) => {
  res.json({ status: "ok", service: "playlist-api", version: "1.0.0" });
});

// 404 fallback
app.use((req, res) => {
  res.status(404).json({ error: "Not found" });
});

// Global error handler
app.use((err, req, res, next) => {
  console.error("Unhandled error:", err);
  res.status(500).json({ error: "Internal server error" });
});

// ── Start ────────────────────────────────────────────────────────────────────

app.listen(PORT, "0.0.0.0", () => {
  console.log(`🎵 playlist-api running on http://0.0.0.0:${PORT}`);
});
