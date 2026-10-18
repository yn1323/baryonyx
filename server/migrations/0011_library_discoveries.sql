CREATE TABLE `library_discoveries` (
	`user_id` text NOT NULL,
	`kind` text NOT NULL,
	`entry_id` text NOT NULL,
	`discovered_at` text NOT NULL,
	`defeated` integer DEFAULT 0 NOT NULL,
	PRIMARY KEY(`user_id`, `kind`, `entry_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "library_discoveries_kind_check" CHECK("library_discoveries"."kind" IN ('character', 'weapon', 'armor', 'accessory', 'skill', 'item', 'enemy')),
	CONSTRAINT "library_discoveries_defeated_check" CHECK("library_discoveries"."defeated" >= 0)
);
