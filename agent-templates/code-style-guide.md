# Code Style Guide — Equipo FLIT

## TypeScript (backend + frontend)

```typescript
// ✅ Estricto: no any, no as, no !
const id: string = getId() ?? ''

// ✅ Naming
class PersonaRepository {}       // PascalCase para clases
interface IPersonaRepository {}  // IPascalCase para interfaces
const findById = () => {}        // camelCase para funciones
const MAX_RETRIES = 3            // UPPER_SNAKE_CASE para constantes
```

```typescript
// ❌ Prohibido
const result: any = getData()    // no any
const id = (value as string)     // no as (usa type guards)
const name = config!.name        // no ! (non-null assertion)
console.log('debug')             // no console.log en producción — usa logger
```

## Backend (Node.js + Fastify + TypeORM)

```typescript
// ✅ Clean Architecture — dirección de dependencias
// domain ← application ← infrastructure → interfaces

// Domain: puro, sin imports externos
export class Persona {
  constructor(
    readonly id: string,
    readonly nombre: string,
    readonly documento: string,
  ) {}
  
  static create(nombre: string, documento: string): Persona {
    if (!nombre.trim()) throw new PersonaInvalidaError('nombre requerido')
    return new Persona(crypto.randomUUID(), nombre, documento)
  }
}

// Application: use case con inyección de dependencias
export class CreatePersonaUseCase {
  constructor(private readonly repo: IPersonaRepository) {}
  
  async execute(cmd: CreatePersonaCommand): Promise<PersonaId> {
    const persona = Persona.create(cmd.nombre, cmd.documento)
    await this.repo.save(persona)
    return persona.id
  }
}

// Interface: controller delgado — sin lógica de negocio
export const personasRoutes: FastifyPluginAsync = async (fastify) => {
  fastify.post<{ Body: CreatePersonaDto }>('/api/v1/personas', {
    schema: { body: createPersonaSchema },
  }, async (request, reply) => {
    const personaId = await container.cradle.createPersonaUseCase.execute(request.body)
    return reply.status(201).send({ id: personaId })
  })
}
```

```typescript
// ❌ Prohibido
// Lógica de negocio en controller
fastify.post('/personas', async (req, rep) => {
  const exists = await db.findOne({ documento: req.body.documento }) // lógica en controller
  if (exists) throw new Error('duplicado')
  // ...
})

// SQL con string concatenation
db.query("SELECT * FROM personas WHERE id = " + req.params.id)  // SQL injection

// Hardcoded credentials
const DB_PASSWORD = "secreto123"  // nunca
```

## Frontend (React + TypeScript)

```tsx
// ✅ Los 4 estados de UI — siempre todos
function PersonasList() {
  const { data, isLoading, error, refetch } = usePersonas()
  
  if (isLoading) return <LoadingSkeleton rows={5} />
  if (error) return <ErrorState error={error} onRetry={refetch} />
  if (!data?.length) return <EmptyState message="No hay personas registradas" />
  return <PersonasTable data={data} />
}

// ✅ Hooks con TanStack Query
function usePersonas() {
  return useQuery({
    queryKey: ['personas'],
    queryFn: () => personasApi.list(),
  })
}

// ✅ Accesibilidad WCAG 2.1 AA
<button
  onClick={handleSubmit}
  aria-label="Guardar persona"
  disabled={isLoading}
>
  {isLoading ? 'Guardando...' : 'Guardar'}
</button>
```

```tsx
// ❌ Prohibido
// Fetch directo en componente
function PersonasList() {
  const [data, setData] = useState([])
  useEffect(() => { fetch('/api/personas').then(r => r.json()).then(setData) }, []) // ❌
  return <div>{data.map(...)}</div>
}

// Variables de entorno sin VITE_
const API_URL = process.env.API_URL  // ❌ — usa import.meta.env.VITE_API_URL

// XSS
<div dangerouslySetInnerHTML={{__html: userContent}} />  // ❌ — falta DOMPurify
```

## Tests

```typescript
// ✅ Patrón AAA
describe('CreatePersonaUseCase', () => {
  it('should create a persona with valid data', async () => {
    // Arrange
    const mockRepo = createMockRepo()
    const useCase = new CreatePersonaUseCase(mockRepo)
    const cmd = { nombre: 'Juan Pérez', documento: '1234567890' }
    
    // Act
    const id = await useCase.execute(cmd)
    
    // Assert
    expect(id).toBeDefined()
    expect(mockRepo.save).toHaveBeenCalledOnce()
  })
  
  it('should throw when nombre is empty', async () => {
    // Arrange
    const mockRepo = createMockRepo()
    const useCase = new CreatePersonaUseCase(mockRepo)
    
    // Act + Assert
    await expect(useCase.execute({ nombre: '', documento: '123' }))
      .rejects.toThrow(PersonaInvalidaError)
  })
})
```

## Imports

```typescript
// ✅ Orden de imports (ESLint enforces this)
// 1. Node built-ins
import { randomUUID } from 'node:crypto'
// 2. External packages
import Fastify from 'fastify'
import { z } from 'zod'
// 3. Internal absolute (using @ alias)
import { CreatePersonaUseCase } from '@/modules/personas/application'
// 4. Relative
import { personasSchema } from './personas.dto'

// ❌ Prohibido
import { something } from '../../../shared/utils' // deep relative — usa @ alias
```

## Env vars

```typescript
// ✅ Siempre validados con Zod al inicio
const envSchema = z.object({
  DATABASE_URL: z.string().url(),
  PORT: z.coerce.number().default(3000),
  NODE_ENV: z.enum(['development', 'test', 'production']).default('development'),
})

export const env = envSchema.parse(process.env)  // throws at startup if invalid
```

## Logging (Pino)

```typescript
// ✅ Logging estructurado con contexto
request.log.info({ event: 'persona.created', personaId, durationMs }, 'Persona creada')

// ❌ Nunca logues secretos
logger.debug({ password: req.body.password })  // ❌
logger.info({ token: authHeader })              // ❌
```
