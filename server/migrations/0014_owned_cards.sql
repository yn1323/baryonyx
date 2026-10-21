PRAGMA foreign_keys=OFF;--> statement-breakpoint
CREATE TABLE `__new_party_character_cards` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`slot` integer NOT NULL,
	`card_id` text,
	`item_id` text,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`, `slot`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`card_id`) REFERENCES `skill_cards`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_character_cards_slot_check" CHECK("__new_party_character_cards"."slot" >= 0 AND "__new_party_character_cards"."slot" < 2),
	CONSTRAINT "party_character_cards_content_check" CHECK(("__new_party_character_cards"."card_id" IS NULL) <> ("__new_party_character_cards"."item_id" IS NULL))
);
--> statement-breakpoint
DROP TABLE `party_character_cards`;--> statement-breakpoint
ALTER TABLE `__new_party_character_cards` RENAME TO `party_character_cards`;--> statement-breakpoint
PRAGMA foreign_keys=ON;--> statement-breakpoint
CREATE UNIQUE INDEX `party_character_cards_user_card` ON `party_character_cards` (`user_id`,`card_id`);--> statement-breakpoint
ALTER TABLE `party_profiles` ADD `grant_id` text DEFAULT '' NOT NULL;