PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_skill_cards` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`skill_id` text NOT NULL,
	`multiplier_percent` integer NOT NULL,
	`acquired_at` text NOT NULL,
	`awakened_branch` integer,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "skill_cards_multiplier_check" CHECK("__new_skill_cards"."multiplier_percent" >= 100),
	CONSTRAINT "skill_cards_awakened_branch_check" CHECK("__new_skill_cards"."awakened_branch" IS NULL OR "__new_skill_cards"."awakened_branch" IN (1, 2))
);
--> statement-breakpoint
INSERT INTO `__new_skill_cards`("id", "user_id", "skill_id", "multiplier_percent", "acquired_at", "awakened_branch") SELECT "id", "user_id", "skill_id", "multiplier_percent", "acquired_at", NULL FROM `skill_cards`;--> statement-breakpoint
DROP TABLE `skill_cards`;--> statement-breakpoint
ALTER TABLE `__new_skill_cards` RENAME TO `skill_cards`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;--> statement-breakpoint
CREATE INDEX `skill_cards_user` ON `skill_cards` (`user_id`,`acquired_at`);