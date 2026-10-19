PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_adventure_rewards` (
	`run_id` text NOT NULL,
	`room_id` text NOT NULL,
	`bonus_id` text NOT NULL,
	`multiplier_percent` integer NOT NULL,
	`outcome` text NOT NULL,
	`created_at` text NOT NULL,
	PRIMARY KEY(`run_id`, `room_id`),
	FOREIGN KEY (`run_id`) REFERENCES `adventure_runs`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_rewards_multiplier_check" CHECK("__new_adventure_rewards"."multiplier_percent" >= 100),
	CONSTRAINT "adventure_rewards_outcome_check" CHECK("__new_adventure_rewards"."outcome" IN ('added', 'updated', 'discarded'))
);
--> statement-breakpoint
INSERT INTO `__new_adventure_rewards`("run_id", "room_id", "bonus_id", "multiplier_percent", "outcome", "created_at") SELECT "run_id", "room_id", "bonus_id", CASE "rank" WHEN 'S' THEN 147 WHEN 'A' THEN 138 WHEN 'B' THEN 126 WHEN 'C' THEN 112 WHEN 'D' THEN 105 ELSE 100 END, "outcome", "created_at" FROM `adventure_rewards`;--> statement-breakpoint
DROP TABLE `adventure_rewards`;--> statement-breakpoint
ALTER TABLE `__new_adventure_rewards` RENAME TO `adventure_rewards`;--> statement-breakpoint
CREATE TABLE `__new_step_bonus_holdings` (
	`user_id` text NOT NULL,
	`bonus_id` text NOT NULL,
	`multiplier_percent` integer NOT NULL,
	`acquired_at` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `bonus_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "step_bonus_holdings_multiplier_check" CHECK("__new_step_bonus_holdings"."multiplier_percent" >= 100)
);
--> statement-breakpoint
INSERT INTO `__new_step_bonus_holdings`("user_id", "bonus_id", "multiplier_percent", "acquired_at", "updated_at") SELECT "user_id", "bonus_id", CASE "rank" WHEN 'S' THEN 147 WHEN 'A' THEN 138 WHEN 'B' THEN 126 WHEN 'C' THEN 112 WHEN 'D' THEN 105 ELSE 100 END, "acquired_at", "updated_at" FROM `step_bonus_holdings`;--> statement-breakpoint
DROP TABLE `step_bonus_holdings`;--> statement-breakpoint
ALTER TABLE `__new_step_bonus_holdings` RENAME TO `step_bonus_holdings`;--> statement-breakpoint
CREATE TABLE `__new_equipment_items` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`equipment_id` text NOT NULL,
	`multiplier_percent` integer NOT NULL,
	`acquired_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "equipment_items_multiplier_check" CHECK("__new_equipment_items"."multiplier_percent" >= 100)
);
--> statement-breakpoint
INSERT INTO `__new_equipment_items`("id", "user_id", "equipment_id", "multiplier_percent", "acquired_at") SELECT "id", "user_id", "equipment_id", 100, "acquired_at" FROM `equipment_items`;--> statement-breakpoint
DROP TABLE `equipment_items`;--> statement-breakpoint
ALTER TABLE `__new_equipment_items` RENAME TO `equipment_items`;--> statement-breakpoint
CREATE UNIQUE INDEX `equipment_items_user_item` ON `equipment_items` (`user_id`,`id`);--> statement-breakpoint
PRAGMA defer_foreign_keys = off;
