CREATE TABLE `workshop_purchases` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`request_id` text NOT NULL,
	`category` text NOT NULL,
	`entry_id` text NOT NULL,
	`item_id` text NOT NULL,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "workshop_purchases_category_check" CHECK("workshop_purchases"."category" IN ('skill', 'weapon', 'armor', 'accessory')),
	CONSTRAINT "workshop_purchases_runes_check" CHECK("workshop_purchases"."runes" > 0),
	CONSTRAINT "workshop_purchases_balance_after_check" CHECK("workshop_purchases"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `workshop_purchases_user_request` ON `workshop_purchases` (`user_id`,`request_id`);