ALTER TABLE `adventure_runs` ADD `seed` integer DEFAULT 0 NOT NULL;--> statement-breakpoint
ALTER TABLE `adventure_runs` ADD `floor` integer DEFAULT 1 NOT NULL;--> statement-breakpoint
ALTER TABLE `adventure_runs` ADD `room_kind` text DEFAULT 'start' NOT NULL;