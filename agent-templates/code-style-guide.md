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

## Backend (.NET 10 + Clean Architecture + SOLID)

Referencia: `backend/CLAUDE.md` y `docs/decisions/ADR-001-clean-architecture-solid.md`.

```csharp
// ✅ SRP + DIP — Domain puro, sin EF Core ni ASP.NET
namespace Flit.Modules.Users.Domain;

public sealed class Employee
{
    public Guid Id { get; }
    public string FullName { get; }

    private Employee(Guid id, string fullName) => (Id, FullName) = (id, fullName);

    public static Employee Create(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new DomainException("El nombre es obligatorio.");
        return new Employee(Guid.NewGuid(), fullName.Trim());
    }
}
```

```csharp
// ✅ DIP — Application depende del puerto, no de EF Core
namespace Flit.Modules.Users.Application;

public interface ICreateEmployeeHandler
{
    Task<Guid> HandleAsync(CreateEmployeeCommand command, CancellationToken ct);
}

public sealed class CreateEmployeeHandler(IEmployeeRepository repository) : ICreateEmployeeHandler
{
    public async Task<Guid> HandleAsync(CreateEmployeeCommand command, CancellationToken ct)
    {
        var employee = Employee.Create(command.FullName);
        await repository.AddAsync(employee, ct);
        return employee.Id;
    }
}
```

```csharp
// ✅ Endpoint delgado — solo mapea HTTP y delega al handler (sin lógica de negocio)
app.MapPost("/api/v1/employees", async (
    CreateEmployeeRequest request,
    ICreateEmployeeHandler handler,
    CancellationToken ct) =>
{
    var id = await handler.HandleAsync(new CreateEmployeeCommand(request.FullName), ct);
    return Results.Created($"/api/v1/employees/{id}", new { id });
});
```

```csharp
// ❌ Prohibido — lógica de negocio en el endpoint
app.MapPost("/api/v1/employees", async (CreateEmployeeRequest req, FlitDbContext db) =>
{
    if (await db.Employees.AnyAsync(e => e.FullName == req.FullName)) // regla en endpoint
        return Results.Conflict();
    // ...
});

// ❌ Prohibido — Application acoplado a EF Core (viola DIP)
public class BadHandler(FlitDbContext db) { ... }

// ❌ Prohibido — SQL concatenado (SQL injection)
db.Database.ExecuteSqlRaw($"SELECT * FROM employees WHERE id = {id}");
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

### Backend (xUnit + NSubstitute)

```csharp
// ✅ Patrón AAA — test del handler, no del endpoint
public class CreateEmployeeHandlerTests
{
    [Fact]
    public async Task HandleAsync_WithValidName_ReturnsEmployeeId()
    {
        // Arrange
        var repository = Substitute.For<IEmployeeRepository>();
        var handler = new CreateEmployeeHandler(repository);
        var command = new CreateEmployeeCommand("Juan Pérez");

        // Act
        var id = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        id.Should().NotBeEmpty();
        await repository.Received(1).AddAsync(Arg.Any<Employee>(), Arg.Any<CancellationToken>());
    }
}
```

### Frontend (Vitest)

```typescript
// ✅ Patrón AAA
describe('PersonasList', () => {
  it('renders empty state when no data', () => {
    render(<PersonasList data={[]} isLoading={false} error={null} />)
    expect(screen.getByText(/no hay personas/i)).toBeInTheDocument()
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

## Configuración y variables de entorno

### Backend (.NET)

```csharp
// ✅ Configuración vía IOptions<T> o IConfiguration — nunca hardcodeada
public class EmployeeService(IOptions<SmtpOptions> smtpOptions) { ... }

// ✅ Variables de entorno con doble guion bajo
// ConnectionStrings__Core, Cors__AllowedOrigins
```

Archivo DEV: `backend/dotnet/src/Flit.Api/appsettings.Development.json`

### Frontend (Vite)

```typescript
// ✅ Solo variables VITE_* validadas con Zod en bordes
const apiUrl = import.meta.env.VITE_API_BASE_URL
```

## Logging

### Backend (Serilog)

```csharp
// ✅ Logging estructurado
_logger.LogInformation("Empleado creado {EmployeeId} en {DurationMs}ms", id, elapsed);

// ❌ Nunca logues secretos
_logger.LogDebug("Token: {Token}", token);  // ❌
```

### Frontend

```typescript
// ❌ No uses console.log en producción — usa herramientas de dev o reporting controlado
```
