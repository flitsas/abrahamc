// server.ts
import "reflect-metadata";
import { env } from "./shared/config/env.js";
import { AppDataSource } from "./shared/config/database.js";
import { buildApp } from "./app.js";

async function main() {
  try {
    await AppDataSource.initialize();
    console.log("✓ Database connected");

    const app = await buildApp(AppDataSource);

    await app.listen({ port: env.PORT, host: "0.0.0.0" });
    console.log(`✓ Server running on port ${env.PORT} [${env.NODE_ENV}]`);
  } catch (err) {
    console.error("✗ Failed to start server:", err);
    process.exit(1);
  }
}

main();
