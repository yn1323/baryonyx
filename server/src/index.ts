import { serve } from "@hono/node-server";
import app from "./app.js";

const server = serve(
  {
    fetch: app.fetch,
    hostname: "127.0.0.1",
    port: 3000,
  },
  (info) => {
    console.info(`Server listening on http://127.0.0.1:${info.port}`);
  },
);

process.once("SIGINT", () => server.close());
process.once("SIGTERM", () => server.close());
