PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_character_bonds` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`bond` integer NOT NULL,
	`tokens` integer DEFAULT 0 NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "character_bonds_bond_check" CHECK("__new_character_bonds"."bond" BETWEEN 0 AND 6),
	CONSTRAINT "character_bonds_tokens_check" CHECK("__new_character_bonds"."tokens" >= 0 AND "__new_character_bonds"."bond" + "__new_character_bonds"."tokens" <= 6)
);
--> statement-breakpoint
INSERT INTO `__new_character_bonds`("user_id", "character_id", "bond", "tokens", "updated_at") SELECT "user_id", "character_id", "bond", 0, "updated_at" FROM `character_bonds`;--> statement-breakpoint
DROP TABLE `character_bonds`;--> statement-breakpoint
ALTER TABLE `__new_character_bonds` RENAME TO `character_bonds`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;