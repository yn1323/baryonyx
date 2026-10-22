CREATE TABLE `party_unique_skills` (
	`user_id` text NOT NULL,
	`character_id` text NOT NULL,
	`unique_index` integer NOT NULL,
	`set_stage` integer NOT NULL,
	`acquired_mask` integer NOT NULL,
	`updated_at` text NOT NULL,
	PRIMARY KEY(`user_id`, `character_id`, `unique_index`),
	FOREIGN KEY (`user_id`) REFERENCES `app_users`(`id`) ON UPDATE no action ON DELETE no action,
	FOREIGN KEY (`user_id`,`character_id`) REFERENCES `party_characters`(`user_id`,`character_id`) ON UPDATE no action ON DELETE no action,
	CONSTRAINT "party_unique_skills_index_check" CHECK("party_unique_skills"."unique_index" >= 0 AND "party_unique_skills"."unique_index" < 2),
	CONSTRAINT "party_unique_skills_stage_check" CHECK("party_unique_skills"."set_stage" >= 0 AND "party_unique_skills"."set_stage" < 7),
	CONSTRAINT "party_unique_skills_mask_check" CHECK("party_unique_skills"."acquired_mask" >= 1 AND "party_unique_skills"."acquired_mask" < 128 AND ("party_unique_skills"."acquired_mask" & 1) = 1),
	CONSTRAINT "party_unique_skills_set_acquired_check" CHECK((("party_unique_skills"."acquired_mask" >> "party_unique_skills"."set_stage") & 1) = 1)
);
