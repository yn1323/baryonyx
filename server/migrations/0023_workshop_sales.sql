CREATE TABLE `workshop_sales` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`request_id` text NOT NULL,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`goods` text NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "workshop_sales_runes_check" CHECK("workshop_sales"."runes" > 0),
	CONSTRAINT "workshop_sales_balance_after_check" CHECK("workshop_sales"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `workshop_sales_user_request` ON `workshop_sales` (`user_id`,`request_id`);