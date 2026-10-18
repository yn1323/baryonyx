CREATE TABLE `combat_revealed_weaknesses` (
	`user_id` text NOT NULL,
	`enemy_id` text NOT NULL,
	`element` text NOT NULL,
	`revealed_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `enemy_id`, `element`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "combat_revealed_weaknesses_element_check" CHECK("combat_revealed_weaknesses"."element" IN ('slash', 'fire', 'ice', 'thunder', 'blunt', 'pierce'))
);
