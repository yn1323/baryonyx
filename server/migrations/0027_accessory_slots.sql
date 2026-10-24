PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_equipment_character_slots` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`slot` text NOT NULL,
	`item_id` text NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`, `slot`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`item_id`) REFERENCES `equipment_items`(`user_id`,`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "equipment_character_slots_slot_check" CHECK("__new_equipment_character_slots"."slot" IN ('weapon', 'armor', 'accessory1', 'accessory2', 'accessory3'))
);
--> statement-breakpoint
INSERT INTO `__new_equipment_character_slots`("user_id", "character_id", "slot", "item_id", "updated_at") SELECT "user_id", "character_id", "slot", "item_id", "updated_at" FROM `equipment_character_slots`;--> statement-breakpoint
DROP TABLE `equipment_character_slots`;--> statement-breakpoint
ALTER TABLE `__new_equipment_character_slots` RENAME TO `equipment_character_slots`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;--> statement-breakpoint
CREATE UNIQUE INDEX `equipment_character_slots_user_item` ON `equipment_character_slots` (`user_id`,`item_id`);