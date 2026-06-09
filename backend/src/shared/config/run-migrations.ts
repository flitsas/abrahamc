import "reflect-metadata";
import { AppDataSource } from "./database.js";

async function runMigrations() {
  await AppDataSource.initialize();
  console.log("📦 Conexión a la base de datos establecida");

  const pending = await AppDataSource.showMigrations();
  if (!pending) {
    console.log("✅ No hay migraciones pendientes");
    await AppDataSource.destroy();
    return;
  }

  const ran = await AppDataSource.runMigrations({ transaction: "each" });
  console.log(`✅ ${ran.length} migración(es) ejecutada(s):`);
  ran.forEach((m) => console.log(`   • ${m.name}`));

  await AppDataSource.destroy();
}

runMigrations().catch((err) => {
  console.error("❌ Error al ejecutar migraciones:", err);
  process.exit(1);
});
