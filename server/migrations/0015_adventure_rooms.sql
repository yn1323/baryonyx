CREATE TABLE `adventure_merchant_goods` (
	`run_id` text NOT NULL,
	`room_id` text NOT NULL,
	`idx` integer NOT NULL,
	`category` text NOT NULL,
	`entry_id` text NOT NULL,
	`multiplier_percent` integer NOT NULL,
	`price` integer NOT NULL,
	`created_at` text NOT NULL,
	PRIMARY KEY(`run_id`, `room_id`, `idx`),
	FOREIGN KEY (`run_id`) REFERENCES `adventure_runs`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_merchant_goods_category_check" CHECK("adventure_merchant_goods"."category" IN ('skill', 'weapon', 'armor', 'accessory')),
	CONSTRAINT "adventure_merchant_goods_multiplier_check" CHECK("adventure_merchant_goods"."multiplier_percent" >= 100),
	CONSTRAINT "adventure_merchant_goods_price_check" CHECK("adventure_merchant_goods"."price" > 0)
);
--> statement-breakpoint
CREATE TABLE `adventure_purchases` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`run_id` text NOT NULL,
	`request_id` text NOT NULL,
	`room_id` text NOT NULL,
	`idx` integer NOT NULL,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`run_id`) REFERENCES `adventure_runs`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "adventure_purchases_runes_check" CHECK("adventure_purchases"."runes" > 0),
	CONSTRAINT "adventure_purchases_balance_after_check" CHECK("adventure_purchases"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `adventure_purchases_user_request` ON `adventure_purchases` (`user_id`,`request_id`);--> statement-breakpoint
CREATE UNIQUE INDEX `adventure_purchases_goods` ON `adventure_purchases` (`run_id`,`room_id`,`idx`);--> statement-breakpoint
ALTER TABLE `adventure_runs` ADD `mimic` integer;--> statement-breakpoint
ALTER TABLE `adventure_runs` ADD `trial` text;--> statement-breakpoint
ALTER TABLE `adventure_runs` ADD `door_steps` integer;