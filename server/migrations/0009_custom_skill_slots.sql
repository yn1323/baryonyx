PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_party_character_cards` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`slot` integer NOT NULL,
	`skill_id` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`, `slot`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_character_cards_slot_check" CHECK("__new_party_character_cards"."slot" >= 0 AND "__new_party_character_cards"."slot" < 2)
);
--> statement-breakpoint
INSERT INTO `__new_party_character_cards`("user_id", "character_id", "slot", "skill_id", "updated_at") SELECT "user_id", "character_id", "slot", "skill_id", "updated_at" FROM `party_character_cards` WHERE "slot" < 2;--> statement-breakpoint
DROP TABLE `party_character_cards`;--> statement-breakpoint
ALTER TABLE `__new_party_character_cards` RENAME TO `party_character_cards`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;--> statement-breakpoint
CREATE UNIQUE INDEX `party_character_cards_user_character_skill` ON `party_character_cards` (`user_id`,`character_id`,`skill_id`);