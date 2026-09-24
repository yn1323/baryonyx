-- D1はトランザクション内でforeign_keysを切り替えられないため、参照の検査を確定時まで遅らせて表を作り直す。
PRAGMA defer_foreign_keys = on;--> statement-breakpoint
CREATE TABLE `__new_app_users` (
	`id` text PRIMARY KEY NOT NULL,
	`google_sub` text,
	`guest_secret_hash` text
);
--> statement-breakpoint
INSERT INTO `__new_app_users`("id", "google_sub") SELECT "id", "google_sub" FROM `app_users`;--> statement-breakpoint
DROP TABLE `app_users`;--> statement-breakpoint
ALTER TABLE `__new_app_users` RENAME TO `app_users`;--> statement-breakpoint
PRAGMA defer_foreign_keys = off;--> statement-breakpoint
CREATE UNIQUE INDEX `app_users_google_sub_unique` ON `app_users` (`google_sub`);--> statement-breakpoint
CREATE UNIQUE INDEX `app_users_guest_secret_hash_unique` ON `app_users` (`guest_secret_hash`);
