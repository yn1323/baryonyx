CREATE TABLE `adventure_records` (
	`user_id` text NOT NULL,
	`destination_id` text NOT NULL,
	`best_floor` integer NOT NULL,
	`clears` integer DEFAULT 0 NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `destination_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_records_best_floor_check" CHECK("adventure_records"."best_floor" >= 1),
	CONSTRAINT "adventure_records_clears_check" CHECK("adventure_records"."clears" >= 0)
);
--> statement-breakpoint
CREATE TABLE `adventure_revives` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`run_id` text NOT NULL,
	`request_id` text NOT NULL,
	`room_id` text NOT NULL,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`run_id`) REFERENCES `adventure_runs`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_revives_runes_check" CHECK("adventure_revives"."runes" > 0),
	CONSTRAINT "adventure_revives_balance_after_check" CHECK("adventure_revives"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `adventure_revives_user_request` ON `adventure_revives` (`user_id`,`request_id`);--> statement-breakpoint
CREATE TABLE `adventure_rewards` (
	`run_id` text NOT NULL,
	`room_id` text NOT NULL,
	`bonus_id` text NOT NULL,
	`rank` text NOT NULL,
	`outcome` text NOT NULL,
	`created_at` text NOT NULL,
	PRIMARY KEY(`run_id`, `room_id`),
	FOREIGN KEY (`run_id`) REFERENCES `adventure_runs`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_rewards_rank_check" CHECK("adventure_rewards"."rank" IN ('E', 'D', 'C', 'B', 'A', 'S')),
	CONSTRAINT "adventure_rewards_outcome_check" CHECK("adventure_rewards"."outcome" IN ('added', 'updated', 'discarded'))
);
--> statement-breakpoint
CREATE TABLE `adventure_runs` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`destination_id` text NOT NULL,
	`room_id` text NOT NULL,
	`room_cleared` integer NOT NULL,
	`route` text NOT NULL,
	`revives` integer DEFAULT 0 NOT NULL,
	`status` text NOT NULL,
	`started_at` text NOT NULL,
	`updated_at` text NOT NULL,
	`ended_at` text,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_runs_status_check" CHECK("adventure_runs"."status" IN ('active', 'cleared', 'defeated', 'retreated')),
	CONSTRAINT "adventure_runs_revives_check" CHECK("adventure_runs"."revives" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `adventure_runs_user_active` ON `adventure_runs` (`user_id`) WHERE "adventure_runs"."status" = 'active';--> statement-breakpoint
CREATE INDEX `adventure_runs_user_started` ON `adventure_runs` (`user_id`,`started_at`);