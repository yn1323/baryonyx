CREATE TABLE `workshop_crafts` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`request_id` text NOT NULL,
	`category` text NOT NULL,
	`entry_id` text NOT NULL,
	`item_id` text NOT NULL,
	`multiplier_percent` integer,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`materials` text NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "workshop_crafts_category_check" CHECK("workshop_crafts"."category" IN ('skill', 'weapon', 'armor', 'accessory', 'item')),
	CONSTRAINT "workshop_crafts_multiplier_check" CHECK("workshop_crafts"."multiplier_percent" IS NULL OR "workshop_crafts"."multiplier_percent" >= 100),
	CONSTRAINT "workshop_crafts_runes_check" CHECK("workshop_crafts"."runes" > 0),
	CONSTRAINT "workshop_crafts_balance_after_check" CHECK("workshop_crafts"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `workshop_crafts_user_request` ON `workshop_crafts` (`user_id`,`request_id`);