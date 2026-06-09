# Build context: monorepo root
FROM node:22-alpine AS base
WORKDIR /app
COPY package.json package-lock.json ./
COPY backend/package.json ./backend/package.json
RUN npm ci --ignore-scripts

FROM base AS builder
WORKDIR /app/backend
COPY backend/tsconfig*.json ./
COPY backend/src ./src
COPY backend/migrations ./migrations
RUN npm run build

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production
COPY package.json package-lock.json ./
COPY backend/package.json ./backend/package.json
RUN npm ci --omit=dev --ignore-scripts && npm cache clean --force
WORKDIR /app/backend
COPY --from=builder /app/backend/dist ./dist
EXPOSE 3030
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s \
  CMD wget -qO- http://localhost:3030/health || exit 1
CMD ["node", "dist/src/server.js"]
