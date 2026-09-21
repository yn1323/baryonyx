CREATE TABLE `exercise_reward_days` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`source_id` text NOT NULL,
	`day` text NOT NULL,
	`zone` text NOT NULL,
	`activity_type` text NOT NULL,
	`metric_type` text NOT NULL,
	`has_value` integer NOT NULL,
	`observed_value` integer,
	`credited_through_value` integer NOT NULL,
	`credited_runes` integer NOT NULL,
	`rule_version` text NOT NULL,
	`last_observed_at` text,
	`updated_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`source_id`) REFERENCES `health_sources`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "exercise_reward_days_has_value_check" CHECK("exercise_reward_days"."has_value" IN (0, 1)),
	CONSTRAINT "exercise_reward_days_observed_value_check" CHECK("exercise_reward_days"."observed_value" >= 0),
	CONSTRAINT "exercise_reward_days_credited_value_check" CHECK("exercise_reward_days"."credited_through_value" >= 0),
	CONSTRAINT "exercise_reward_days_credited_runes_check" CHECK("exercise_reward_days"."credited_runes" >= 0),
	CONSTRAINT "exercise_reward_days_value_consistency_check" CHECK(("exercise_reward_days"."has_value" = 1 AND "exercise_reward_days"."observed_value" IS NOT NULL) OR ("exercise_reward_days"."has_value" = 0 AND "exercise_reward_days"."observed_value" IS NULL))
);
--> statement-breakpoint
CREATE UNIQUE INDEX `exercise_reward_days_key` ON `exercise_reward_days` (`user_id`,`source_id`,`day`,`zone`,`activity_type`,`metric_type`);--> statement-breakpoint
CREATE INDEX `exercise_reward_days_user_day` ON `exercise_reward_days` (`user_id`,`day`);--> statement-breakpoint
CREATE TABLE `rune_claims` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`source_id` text NOT NULL,
	`request_id` text NOT NULL,
	`granted_runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`source_id`) REFERENCES `health_sources`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "rune_claims_granted_runes_check" CHECK("rune_claims"."granted_runes" >= 0),
	CONSTRAINT "rune_claims_balance_after_check" CHECK("rune_claims"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `rune_claims_request_id` ON `rune_claims` (`request_id`);--> statement-breakpoint
CREATE INDEX `rune_claims_user_created` ON `rune_claims` (`user_id`,`created_at`);--> statement-breakpoint
CREATE TABLE `rune_ledger` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`claim_id` text NOT NULL,
	`reward_day_id` text NOT NULL,
	`delta` integer NOT NULL,
	`reason` text NOT NULL,
	`from_value` integer NOT NULL,
	`to_value` integer NOT NULL,
	`rule_version` text NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`claim_id`) REFERENCES `rune_claims`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`reward_day_id`) REFERENCES `exercise_reward_days`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "rune_ledger_delta_check" CHECK("rune_ledger"."delta" > 0),
	CONSTRAINT "rune_ledger_value_order_check" CHECK("rune_ledger"."to_value" > "rune_ledger"."from_value")
);
--> statement-breakpoint
CREATE UNIQUE INDEX `rune_ledger_claim_day` ON `rune_ledger` (`claim_id`,`reward_day_id`);--> statement-breakpoint
CREATE INDEX `rune_ledger_user_created` ON `rune_ledger` (`user_id`,`created_at`);--> statement-breakpoint
CREATE TABLE `rune_wallets` (
	`user_id` text PRIMARY KEY NOT NULL,
	`balance` integer DEFAULT 0 NOT NULL,
	`updated_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
