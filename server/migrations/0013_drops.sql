CREATE TABLE `adventure_drops` (
	`run_id` text NOT NULL,
	`room_id` text NOT NULL,
	`seq` integer NOT NULL,
	`grant_id` text NOT NULL,
	`category` text NOT NULL,
	`entry_id` text NOT NULL,
	`multiplier_percent` integer,
	`outcome` text NOT NULL,
	`created_at` text NOT NULL,
	PRIMARY KEY(`run_id`, `room_id`, `seq`),
	FOREIGN KEY (`run_id`) REFERENCES `adventure_runs`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_drops_category_check" CHECK("adventure_drops"."category" IN ('item', 'accessory', 'weapon', 'armor', 'skill', 'bonus')),
	CONSTRAINT "adventure_drops_multiplier_check" CHECK(("adventure_drops"."category" = 'item' AND "adventure_drops"."multiplier_percent" IS NULL) OR ("adventure_drops"."category" <> 'item' AND "adventure_drops"."multiplier_percent" >= 100)),
	CONSTRAINT "adventure_drops_outcome_check" CHECK("adventure_drops"."outcome" IN ('added', 'updated', 'discarded'))
);
--> statement-breakpoint
CREATE TABLE `item_holdings` (
	`user_id` text NOT NULL,
	`item_id` text NOT NULL,
	`count` integer NOT NULL,
	`acquired_at` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `item_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "item_holdings_count_check" CHECK("item_holdings"."count" >= 1)
);
--> statement-breakpoint
CREATE TABLE `skill_cards` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`skill_id` text NOT NULL,
	`multiplier_percent` integer NOT NULL,
	`acquired_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "skill_cards_multiplier_check" CHECK("skill_cards"."multiplier_percent" >= 100)
);
--> statement-breakpoint
CREATE INDEX `skill_cards_user` ON `skill_cards` (`user_id`,`acquired_at`);--> statement-breakpoint
DROP TABLE `adventure_rewards`;