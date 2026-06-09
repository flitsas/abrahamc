# frontend/CLAUDE.md — Convenciones específicas del frontend FLIT

## Stack

React 19 + Vite 5 + TypeScript strict + TailwindCSS + TanStack Query 5 + Zod + Vitest + Playwright

## Arquitectura feature-sliced

```
src/features/<feature>/
  api/
    <feature>.schemas.ts    # Zod schemas: validan respuestas del backend
    <feature>.api.ts        # fetch functions + TanStack Query hooks
  components/               # UI específica del feature
  hooks/                    # Lógica reusable (si no es solo TanStack Query)
  pages/                    # Route-level components

src/shared/
  api/client.ts             # Axios instance con interceptors
  components/ui/            # Primitivos UI reutilizables
  lib/                      # Utilidades
  types/                    # Tipos globales
```

## Los 4 estados de UI (obligatorio en toda lista/query)

```tsx
if (isLoading) return <LoadingSkeleton />
if (error) return <ErrorState error={error} onRetry={refetch} />
if (!data?.length) return <EmptyState />
return <DataView data={data} />
```

## Reglas críticas

- NUNCA fetch directo en components — siempre via hooks TanStack Query
- NUNCA `process.env` — usa `import.meta.env.VITE_*`
- NUNCA `dangerouslySetInnerHTML` sin `DOMPurify.sanitize()` en la misma línea
- WCAG 2.1 AA: labels, aria-*, focus visible, contraste mínimo 4.5:1
- Tailwind utility-first; extrae componente solo si se repite ≥3 veces
- Zod parsea todas las respuestas del backend (defensa en profundidad)
