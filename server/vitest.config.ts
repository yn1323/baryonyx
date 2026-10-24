import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts", "tests/**/*.test.ts"],
    fileParallelism: true,
    maxWorkers: 3,
    sequence: { concurrent: false },
    // WindowsではMiniflareのD1への問い合わせが1回あたり15〜45msかかり（タイマーの刻み15.6msの倍数）、
    // 問い合わせの多いテストは1件で15秒を超える。
    hookTimeout: 120_000,
    testTimeout: 60_000,
  },
});
