PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_enhancement_records` (
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
	CONSTRAINT "enhancement_records_kind_check" CHECK("__new_enhancement_records"."kind" IN ('polish', 'awaken', 'combine')),
	CONSTRAINT "enhancement_records_category_check" CHECK("__new_enhancement_records"."category" IN ('skill', 'weapon', 'armor', 'accessory', 'unique', 'material')),
	CONSTRAINT "enhancement_records_material_count_check" CHECK("__new_enhancement_records"."material_count" > 0),
	CONSTRAINT "enhancement_records_runes_check" CHECK("__new_enhancement_records"."runes" > 0),
	CONSTRAINT "enhancement_records_balance_after_check" CHECK("__new_enhancement_records"."balance_after" >= 0)
);
--> statement-breakpoint
INSERT INTO `__new_enhancement_records`("id", "user_id", "request_id", "kind", "category", "target", "material_id", "material_count", "before", "after", "runes", "balance_after", "created_at") SELECT "id", "user_id", "request_id", "kind", "category", "target", "material_id", "material_count", "before", "after", "runes", "balance_after", "created_at" FROM `enhancement_records`;--> statement-breakpoint
DROP TABLE `enhancement_records`;--> statement-breakpoint
ALTER TABLE `__new_enhancement_records` RENAME TO `enhancement_records`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;--> statement-breakpoint
CREATE UNIQUE INDEX `enhancement_records_user_request` ON `enhancement_records` (`user_id`,`request_id`);