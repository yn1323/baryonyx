ALTER TABLE health_days ADD COLUMN last_known_observed_at TEXT;
UPDATE health_days SET last_known_observed_at = observed_at WHERE has_value = 1;
