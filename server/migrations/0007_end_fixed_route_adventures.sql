-- 決まった6部屋の道で進行中だった冒険を、自分からやめた扱いで終える。
-- 0006で道を乱数の種から作る形に変え、以前の部屋（moss-hall など）は新しい道にないため、続きから再開できない。
-- 最深到達は記録しない（終えた時点の階を新しい列から求められないため）。手に入れた報酬は持ち物に残っている。
UPDATE `adventure_runs`
SET `status` = 'retreated', `ended_at` = `updated_at`
WHERE `status` = 'active';
