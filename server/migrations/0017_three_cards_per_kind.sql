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
	CONSTRAINT "party_character_cards_slot_check" CHECK("__new_party_character_cards"."slot" >= 0 AND "__new_party_character_cards"."slot" < 3),
	CONSTRAINT "party_character_cards_content_check" CHECK(("__new_party_character_cards"."card_id" IS NULL) <> ("__new_party_character_cards"."item_id" IS NULL))
);
--> statement-breakpoint
INSERT INTO `__new_party_character_cards`("user_id", "character_id", "slot", "card_id", "item_id", "updated_at") SELECT "user_id", "character_id", "slot", "card_id", "item_id", "updated_at" FROM `party_character_cards`;--> statement-breakpoint
DROP TABLE `party_character_cards`;--> statement-breakpoint
ALTER TABLE `__new_party_character_cards` RENAME TO `party_character_cards`;--> statement-breakpoint
PRAGMA foreign_keys=ON;--> statement-breakpoint
CREATE UNIQUE INDEX `party_character_cards_user_card` ON `party_character_cards` (`user_id`,`card_id`);--> statement-breakpoint
CREATE TABLE `__new_party_unique_skills` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`unique_index` integer NOT NULL,
	`set_stage` integer NOT NULL,
	`acquired_mask` integer NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`, `unique_index`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_unique_skills_index_check" CHECK("__new_party_unique_skills"."unique_index" >= 0 AND "__new_party_unique_skills"."unique_index" < 3),
	CONSTRAINT "party_unique_skills_stage_check" CHECK("__new_party_unique_skills"."set_stage" >= 0 AND "__new_party_unique_skills"."set_stage" < 7),
	CONSTRAINT "party_unique_skills_mask_check" CHECK("__new_party_unique_skills"."acquired_mask" >= 1 AND "__new_party_unique_skills"."acquired_mask" < 128 AND ("__new_party_unique_skills"."acquired_mask" & 1) = 1),
	CONSTRAINT "party_unique_skills_set_acquired_check" CHECK((("__new_party_unique_skills"."acquired_mask" >> "__new_party_unique_skills"."set_stage") & 1) = 1)
);
--> statement-breakpoint
INSERT INTO `__new_party_unique_skills`("user_id", "character_id", "unique_index", "set_stage", "acquired_mask", "updated_at") SELECT "user_id", "character_id", "unique_index", "set_stage", "acquired_mask", "updated_at" FROM `party_unique_skills`;--> statement-breakpoint
DROP TABLE `party_unique_skills`;--> statement-breakpoint
ALTER TABLE `__new_party_unique_skills` RENAME TO `party_unique_skills`;