// 0以上1未満の乱数。テストでは固定の値に差し替える。
export type Random = () => number;

// 重みに従って1つ選ぶ。重みの合計に対する割合で選ばれる。
export function pickWeighted<T>(
  choices: readonly T[],
  weightOf: (choice: T) => number,
  random: Random,
): T {
  const total = choices.reduce((sum, choice) => sum + weightOf(choice), 0);
  let roll = random() * total;
  for (const choice of choices) {
    const weight = weightOf(choice);
    if (roll < weight) return choice;
    roll -= weight;
  }
  return choices[choices.length - 1];
}
