// shared/config/database.ts
import { DataSource } from "typeorm";
import { env } from "./env.js";
import { PersonaTypeOrmEntity } from "../../modules/personas/infrastructure/persona.typeorm-entity.js";
import { EmployeeTypeOrmEntity } from "../../modules/employees/infrastructure/employee.typeorm-entity.js";
import { EmployeeDependentTypeOrmEntity } from "../../modules/employee-dependents/infrastructure/employee-dependent.typeorm-entity.js";
import { EmployeePositionTypeOrmEntity } from "../../modules/employee-positions/infrastructure/employee-position.typeorm-entity.js";
import { CreatePersonasTable1700000000000 } from "../../../migrations/1700000000000-CreatePersonasTable.js";
import { CreateEmployeesTable1700000000001 } from "../../../migrations/1700000000001-CreateEmployeesTable.js";
import { AddEquipoTrabajoToEmployees1700000000002 } from "../../../migrations/1700000000002-AddEquipoTrabajoToEmployees.js";
import { AddHabeaDataToEmployees1700000000003 } from "../../../migrations/1700000000003-AddHabeaDataToEmployees.js";
import { CreateEmployeeDependentsTable1700000000004 } from "../../../migrations/1700000000004-CreateEmployeeDependentsTable.js";
import { AddSeguracionSocialToEmployees1700000000005 } from "../../../migrations/1700000000005-AddSeguracionSocialToEmployees.js";
import { CreateEmployeePositionsTable1700000000006 } from "../../../migrations/1700000000006-CreateEmployeePositionsTable.js";

export const AppDataSource = new DataSource({
  type: "postgres",
  url: env.DATABASE_URL,
  entities: [
    PersonaTypeOrmEntity,
    EmployeeTypeOrmEntity,
    EmployeeDependentTypeOrmEntity,
    EmployeePositionTypeOrmEntity,
  ],
  migrations: [
    CreatePersonasTable1700000000000,
    CreateEmployeesTable1700000000001,
    AddEquipoTrabajoToEmployees1700000000002,
    AddHabeaDataToEmployees1700000000003,
    CreateEmployeeDependentsTable1700000000004,
    AddSeguracionSocialToEmployees1700000000005,
    CreateEmployeePositionsTable1700000000006,
  ],
  logging: env.NODE_ENV === "development",
  ssl:
    env.NODE_ENV === "production" &&
    !env.DATABASE_URL.includes("sslmode=disable")
      ? { rejectUnauthorized: false }
      : false,
  extra: {
    max: 10,
    idleTimeoutMillis: 30_000,
    connectionTimeoutMillis: 5_000,
  },
});
