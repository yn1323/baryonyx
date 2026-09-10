import { defineConfig } from "drizzle-kit";

export default defineConfig({
  schema: "./src/features/**/db-schema.ts",
  out: "./migrations",
  dialect: "sqlite",
  migrations: { prefix: "index" },
});
