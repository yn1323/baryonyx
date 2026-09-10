CREATE TABLE `app_sessions` (
	`token_hash` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`expires_at` integer NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE INDEX `app_sessions_expiry` ON `app_sessions` (`expires_at`);--> statement-breakpoint
CREATE TABLE `app_users` (
	`id` text PRIMARY KEY NOT NULL,
	`google_sub` text NOT NULL
);
--> statement-breakpoint
CREATE UNIQUE INDEX `app_users_google_sub_unique` ON `app_users` (`google_sub`);--> statement-breakpoint
CREATE TABLE `health_days` (
	`source_id` text NOT NULL,
	`day` text NOT NULL,
	`zone` text NOT NULL,
	`start_at` text NOT NULL,
	`end_at` text NOT NULL,
	`has_value` integer NOT NULL,
	`steps` integer,
	`last_known_steps` integer,
	`observed_at` text NOT NULL,
	`received_at` text NOT NULL,
	`revision` integer NOT NULL,
	`last_known_observed_at` text,
	PRIMARY KEY(`source_id`, `day`, `zone`),
	FOREIGN KEY (`source_id`) REFERENCES `health_sources`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "health_days_has_value_check" CHECK("health_days"."has_value" IN (0, 1)),
	CONSTRAINT "health_days_steps_check" CHECK("health_days"."steps" >= 0),
	CONSTRAINT "health_days_last_known_steps_check" CHECK("health_days"."last_known_steps" >= 0),
	CONSTRAINT "health_days_value_check" CHECK(("health_days"."has_value" = 1 AND "health_days"."steps" IS NOT NULL) OR ("health_days"."has_value" = 0 AND "health_days"."steps" IS NULL))
);
--> statement-breakpoint
CREATE TABLE `health_sources` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`provider` text NOT NULL,
	`revision` integer DEFAULT 0 NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "health_sources_provider_check" CHECK("health_sources"."provider" IN ('health_connect', 'healthkit'))
);
