import { Hono } from "hono";
import { createHealthApi } from "./features/health/routes.js";

const app = new Hono();

app.get("/health", (c) => c.json({ status: "ok" }));

app.route("/v1", createHealthApi());

export default app;
