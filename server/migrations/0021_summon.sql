CREATE TABLE `character_bonds` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`bond` integer NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "character_bonds_bond_check" CHECK("character_bonds"."bond" BETWEEN 0 AND 6)
);
--> statement-breakpoint
CREATE TABLE `summon_pulls` (
	`id` text PRIMARY KEY NOT NULL,
	`user_id` text NOT NULL,
	`request_id` text NOT NULL,
	`count` integer NOT NULL,
	`runes` integer NOT NULL,
	`results` text NOT NULL,
	`balance_after` integer NOT NULL,
	`created_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "summon_pulls_count_check" CHECK("summon_pulls"."count" IN (1, 10)),
	CONSTRAINT "summon_pulls_runes_check" CHECK("summon_pulls"."runes" > 0),
	CONSTRAINT "summon_pulls_balance_after_check" CHECK("summon_pulls"."balance_after" >= 0)
);
--> statement-breakpoint
CREATE UNIQUE INDEX `summon_pulls_user_request` ON `summon_pulls` (`user_id`,`request_id`);--> statement-breakpoint
CREATE TABLE `summon_shards` (
	`user_id` text PRIMARY KEY NOT NULL,
	`shards` integer NOT NULL,
	`updated_at` text NOT NULL,
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "summon_shards_shards_check" CHECK("summon_shards"."shards" >= 0)
);
