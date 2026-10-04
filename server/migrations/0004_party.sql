CREATE TABLE `party_character_cards` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`slot` integer NOT NULL,
	`skill_id` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`, `slot`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_character_cards_slot_check" CHECK("party_character_cards"."slot" >= 0 AND "party_character_cards"."slot" < 4)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `party_character_cards_user_character_skill` ON `party_character_cards` (`user_id`,`character_id`,`skill_id`);--> statement-breakpoint
CREATE TABLE `party_characters` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`level` integer NOT NULL,
	`acquired_at` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_characters_level_check" CHECK("party_characters"."level" >= 1)
);
--> statement-breakpoint
CREATE TABLE `party_level_ups` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`request_id` text NOT NULL,
	`character_id` text NOT NULL,
	`from_level` integer NOT NULL,
	`to_level` integer NOT NULL,
	`runes` integer NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_level_ups_level_order_check" CHECK("party_level_ups"."to_level" > "party_level_ups"."from_level" AND "party_level_ups"."from_level" >= 1),
	CONSTRAINT "party_level_ups_runes_check" CHECK("party_level_ups"."runes" > 0),
	CONSTRAINT "party_level_ups_balance_after_check" CHECK("party_level_ups"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `party_level_ups_user_request` ON `party_level_ups` (`user_id`,`request_id`);--> statement-breakpoint
CREATE INDEX `party_level_ups_user_created` ON `party_level_ups` (`user_id`,`created_at`);--> statement-breakpoint
CREATE TABLE `party_profiles` (
	`user_id` text PRIMARY KEY NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action
);
--> statement-breakpoint
CREATE TABLE `party_slots` (
	`user_id` text NOT NULL,
	`slot` integer NOT NULL,
	`character_id` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `slot`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_slots_slot_check" CHECK("party_slots"."slot" >= 0 AND "party_slots"."slot" < 4)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `party_slots_user_character` ON `party_slots` (`user_id`,`character_id`);