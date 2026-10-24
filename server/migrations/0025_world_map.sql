CREATE TABLE `world_map_positions` (
	`user_id` text PRIMARY KEY NOT NULL,
	`square_id` text NOT NULL,
	`updated_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE TABLE `world_map_squares` (
	`user_id` text NOT NULL,
	`square_id` text NOT NULL,
	`cleared_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `square_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
