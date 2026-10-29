CREATE TABLE `world_atlas_squares` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`request_id` text NOT NULL,
	`map_key` text NOT NULL,
	`square_id` text NOT NULL,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "world_atlas_squares_runes_check" CHECK("world_atlas_squares"."runes" > 0),
	CONSTRAINT "world_atlas_squares_balance_after_check" CHECK("world_atlas_squares"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `world_atlas_squares_user_request` ON `world_atlas_squares` (`user_id`,`request_id`);--> statement-breakpoint
CREATE UNIQUE INDEX `world_atlas_squares_user_square` ON `world_atlas_squares` (`user_id`,`map_key`,`square_id`);