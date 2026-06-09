import "reflect-metadata";
import { AppDataSource } from "./database.js";

async function revertMigration() {
  await AppDataSource.initialize();
  console.log("📦 Conexión a la base de datos establecida");

  await AppDataSource.undoLastMigration({ transaction: "each" });
  console.log("↩️  Última migración revertida");

  await AppDataSource.destroy();
}

revertMigration().catch((err) => {
  console.error("❌ Error al revertir migración:", err);
  process.exit(1);
});
