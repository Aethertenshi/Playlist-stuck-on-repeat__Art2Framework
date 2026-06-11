const { createClient } = require("@supabase/supabase-js");
const WebSocket = require("ws");

// Admin client — bypasses RLS, used for server-side inserts/queries
const supabaseAdmin = createClient(
  process.env.SUPABASE_URL,
  process.env.SUPABASE_SERVICE_ROLE_KEY,
  { realtime: { transport: WebSocket } }
);

// Auth client — uses anon key, used for verifying user JWTs
const supabaseAuth = createClient(
  process.env.SUPABASE_URL,
  process.env.SUPABASE_ANON_KEY,
  { realtime: { transport: WebSocket } }
);

module.exports = { supabaseAdmin, supabaseAuth };
