CREATE TABLE `step_bonus_holdings` (
	`user_id` text NOT NULL,
	`bonus_id` text NOT NULL,
	`rank` text NOT NULL,
	`acquired_at` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `bonus_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "step_bonus_holdings_rank_check" CHECK("step_bonus_holdings"."rank" IN ('E', 'D', 'C', 'B', 'A', 'S'))
);
--> statement-breakpoint
CREATE TABLE `step_bonus_profiles` (
	`user_id` text PRIMARY KEY NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE TABLE `step_bonus_slots` (
	`user_id` text NOT NULL,
	`slot` integer NOT NULL,
	`bonus_id` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `slot`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`bonus_id`) REFERENCES `step_bonus_holdings`(`user_id`,`bonus_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "step_bonus_slots_slot_check" CHECK("step_bonus_slots"."slot" >= 0 AND "step_bonus_slots"."slot" < 5)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `step_bonus_slots_user_bonus` ON `step_bonus_slots` (`user_id`,`bonus_id`);