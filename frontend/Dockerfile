# Build context: monorepo root

FROM node:24-alpine AS builder

WORKDIR /app

RUN corepack enable && corepack prepare pnpm@10.20.0 --activate

COPY package.json pnpm-lock.yaml pnpm-workspace.yaml .npmrc ./

COPY frontend/package.json ./frontend/package.json

RUN pnpm install --frozen-lockfile && pnpm rebuild esbuild

COPY frontend/ ./frontend/

WORKDIR /app/frontend

ARG VITE_API_BASE_URL=/api/v1

ENV VITE_API_BASE_URL=$VITE_API_BASE_URL

RUN pnpm run build



FROM nginx:1.27-alpine AS runtime

COPY --from=builder /app/frontend/dist /usr/share/nginx/html

COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s \
  CMD wget -qO- http://localhost/ || exit 1

