// app.ts
import Fastify, { type FastifyError } from "fastify";
import cors from "@fastify/cors";
import helmet from "@fastify/helmet";
import type { DataSource } from "typeorm";
import {
  serializerCompiler,
  validatorCompiler,
  ZodTypeProvider,
} from "fastify-type-provider-zod";
import { env } from "./shared/config/env.js";
import { personasRoutes } from "./modules/personas/interfaces/personas.routes.js";
import { employeesRoutes } from "./modules/employees/interfaces/employees.routes.js";
import { employeeDependentsRoutes } from "./modules/employee-dependents/interfaces/employee-dependents.routes.js";
import { employeePositionsRoutes } from "./modules/employee-positions/interfaces/employee-positions.routes.js";

declare module "fastify" {
  interface FastifyInstance {
    db: DataSource;
  }
}

export async function buildApp(dataSource: DataSource) {
  const app = Fastify({
    logger: {
      level: env.LOG_LEVEL,
      ...(env.NODE_ENV === "development"
        ? {
            transport: { target: "pino-pretty", options: { colorize: true } },
          }
        : {}),
    },
  }).withTypeProvider<ZodTypeProvider>();

  app.setValidatorCompiler(validatorCompiler);
  app.setSerializerCompiler(serializerCompiler);

  // Plugins
  await app.register(cors, { origin: env.CORS_ORIGIN });
  await app.register(helmet, {
    contentSecurityPolicy: env.NODE_ENV !== "development",
  });

  // Decorate with database connection
  app.decorate("db", dataSource);

  // Health check
  app.get("/health", async () => ({ status: "ok", env: env.NODE_ENV }));

  // Routes
  await app.register(personasRoutes, { prefix: "/api/v1" });
  await app.register(employeesRoutes, { prefix: "/api/v1" });
  await app.register(employeeDependentsRoutes, { prefix: "/api/v1" });
  await app.register(employeePositionsRoutes, { prefix: "/api/v1" });

  // Global error handler
  app.setErrorHandler((error: FastifyError, request, reply) => {
    const log = request.log ?? app.log;

    if (error.validation) {
      return reply.status(400).send({
        error: "VALIDATION_ERROR",
        message: "Datos de entrada inválidos",
        details: error.validation,
      });
    }

    log.error({ err: error, reqId: request.id }, "Unhandled error");

    return reply.status(500).send({
      error: "INTERNAL_SERVER_ERROR",
      message:
        env.NODE_ENV === "production"
          ? "Error interno del servidor"
          : error.message,
    });
  });

  return app;
}
