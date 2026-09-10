import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: ["src/**/*.test.ts", "tests/**/*.test.ts"],
    fileParallelism: true,
    maxWorkers: 3,
    sequence: { concurrent: false },
    hookTimeout: 30_000,
    testTimeout: 15_000,
  },
});
