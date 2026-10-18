// 弱点を記録できる敵の種類と属性。敵の定義（名前・弱点）の正本はクライアントの
// client/Assets/Baryonyx/Features/Combat/Runtime/CombatEnemies.cs で、IDだけをここにそろえる。
export const ENEMY_IDS = [
  "moss-slime",
  "moss-wolf",
  "forest-guardian",
] as const;

export const ELEMENTS = [
  "slash",
  "fire",
  "ice",
  "thunder",
  "blunt",
  "pierce",
] as const;

export type EnemyId = (typeof ENEMY_IDS)[number];
export type Element = (typeof ELEMENTS)[number];
