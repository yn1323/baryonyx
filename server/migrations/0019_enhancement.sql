CREATE TABLE `enhancement_materials` (
	`user_id` text NOT NULL,
	`material_id` text NOT NULL,
	`count` integer NOT NULL,
	`acquired_at` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `material_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "enhancement_materials_count_check" CHECK("enhancement_materials"."count" >= 0)
);
--> statement-breakpoint
CREATE TABLE `enhancement_records` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`request_id` text NOT NULL,
	`kind` text NOT NULL,
	`category` text NOT NULL,
	`target` text NOT NULL,
	`material_id` text NOT NULL,
	`material_count` integer NOT NULL,
	`before` integer NOT NULL,
	`after` integer NOT NULL,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "enhancement_records_kind_check" CHECK("enhancement_records"."kind" IN ('polish', 'awaken')),
	CONSTRAINT "enhancement_records_category_check" CHECK("enhancement_records"."category" IN ('skill', 'weapon', 'armor', 'accessory', 'unique')),
	CONSTRAINT "enhancement_records_material_count_check" CHECK("enhancement_records"."material_count" > 0),
	CONSTRAINT "enhancement_records_runes_check" CHECK("enhancement_records"."runes" > 0),
	CONSTRAINT "enhancement_records_balance_after_check" CHECK("enhancement_records"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `enhancement_records_user_request` ON `enhancement_records` (`user_id`,`request_id`);--> statement-breakpoint
PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_adventure_drops` (
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
	CONSTRAINT "adventure_drops_category_check" CHECK("__new_adventure_drops"."category" IN ('item', 'accessory', 'weapon', 'armor', 'skill', 'bonus', 'material')),
	CONSTRAINT "adventure_drops_multiplier_check" CHECK(("__new_adventure_drops"."category" IN ('item', 'material') AND "__new_adventure_drops"."multiplier_percent" IS NULL) OR ("__new_adventure_drops"."category" NOT IN ('item', 'material') AND "__new_adventure_drops"."multiplier_percent" >= 100)),
	CONSTRAINT "adventure_drops_outcome_check" CHECK("__new_adventure_drops"."outcome" IN ('added', 'updated', 'discarded'))
);
--> statement-breakpoint
INSERT INTO `__new_adventure_drops`("run_id", "room_id", "seq", "grant_id", "category", "entry_id", "multiplier_percent", "outcome", "created_at") SELECT "run_id", "room_id", "seq", "grant_id", "category", "entry_id", "multiplier_percent", "outcome", "created_at" FROM `adventure_drops`;--> statement-breakpoint
DROP TABLE `adventure_drops`;--> statement-breakpoint
ALTER TABLE `__new_adventure_drops` RENAME TO `adventure_drops`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;--> statement-breakpoint
ALTER TABLE `party_unique_skills` ADD `multiplier_percent` integer DEFAULT 100 NOT NULL;