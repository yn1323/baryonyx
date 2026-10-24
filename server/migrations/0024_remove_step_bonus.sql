DROP TABLE `step_bonus_slots`;--> statement-breakpoint
DROP TABLE `step_bonus_holdings`;--> statement-breakpoint
DROP TABLE `step_bonus_profiles`;--> statement-breakpoint
PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_adventure_drops` (
	`run_id` text NOT NULL,
	`room_id` text NOT NULL,
	`seq` integer NOT NULL,
	`grant_id` text NOT NULL,
	`category` text NOT NULL,
	`entry_id` text NOT NULL,
	`multiplier_percent` integer,
	`created_at` text NOT NULL,
	PRIMARY KEY(`run_id`, `room_id`, `seq`),
	FOREIGN KEY (`run_id`) REFERENCES `adventure_runs`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_drops_category_check" CHECK("__new_adventure_drops"."category" IN ('item', 'accessory', 'weapon', 'armor', 'skill', 'material')),
	CONSTRAINT "adventure_drops_multiplier_check" CHECK(("__new_adventure_drops"."category" IN ('item', 'material') AND "__new_adventure_drops"."multiplier_percent" IS NULL) OR ("__new_adventure_drops"."category" NOT IN ('item', 'material') AND "__new_adventure_drops"."multiplier_percent" >= 100))
);
--> statement-breakpoint
INSERT INTO `__new_adventure_drops`("run_id", "room_id", "seq", "grant_id", "category", "entry_id", "multiplier_percent", "created_at") SELECT "run_id", "room_id", "seq", "grant_id", "category", "entry_id", "multiplier_percent", "created_at" FROM `adventure_drops` WHERE "category" <> 'bonus';--> statement-breakpoint
DROP TABLE `adventure_drops`;--> statement-breakpoint
ALTER TABLE `__new_adventure_drops` RENAME TO `adventure_drops`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;