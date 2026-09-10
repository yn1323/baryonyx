CREATE TABLE app_users (
  id TEXT PRIMARY KEY,
  google_sub TEXT NOT NULL UNIQUE
);
CREATE TABLE app_sessions (
  token_hash TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES app_users(id),
  expires_at INTEGER NOT NULL
);
CREATE INDEX app_sessions_expiry ON app_sessions(expires_at);
CREATE TABLE health_sources (
  id TEXT PRIMARY KEY,
  user_id TEXT NOT NULL REFERENCES app_users(id),
  provider TEXT NOT NULL CHECK(provider IN ('health_connect', 'healthkit')),
  revision INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE health_days (
  source_id TEXT NOT NULL REFERENCES health_sources(id),
  day TEXT NOT NULL,
  zone TEXT NOT NULL,
  start_at TEXT NOT NULL,
  end_at TEXT NOT NULL,
  has_value INTEGER NOT NULL CHECK(has_value IN (0, 1)),
  steps INTEGER CHECK(steps >= 0),
  last_known_steps INTEGER CHECK(last_known_steps >= 0),
  observed_at TEXT NOT NULL,
  received_at TEXT NOT NULL,
  revision INTEGER NOT NULL,
  PRIMARY KEY(source_id, day, zone),
  CHECK((has_value = 1 AND steps IS NOT NULL) OR (has_value = 0 AND steps IS NULL))
);
