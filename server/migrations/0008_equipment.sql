CREATE TABLE `equipment_character_slots` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`slot` text NOT NULL,
	`item_id` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`, `slot`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`item_id`) REFERENCES `equipment_items`(`user_id`,`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "equipment_character_slots_slot_check" CHECK("equipment_character_slots"."slot" IN ('weapon', 'armor'))
);
--> statement-breakpoint
CREATE UNIQUE INDEX `equipment_character_slots_user_item` ON `equipment_character_slots` (`user_id`,`item_id`);--> statement-breakpoint
CREATE TABLE `equipment_items` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`equipment_id` text NOT NULL,
	`acquired_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE UNIQUE INDEX `equipment_items_user_item` ON `equipment_items` (`user_id`,`id`);--> statement-breakpoint
CREATE TABLE `equipment_profiles` (
	`user_id` text PRIMARY KEY NOT NULL,
	`grant_id` text NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
