# Boxty

A comprehensive .NET framework providing base components for building multi-tenant applications with Blazor, ASP.NET Core and Keycloak.

## Packages

### Boxty.ClientBase
Client-side Blazor components and services including:
- Reusable UI components (CrudGrid, CrudForm, DocumentBrowser, etc.)
- Authentication helpers and message handlers
- Document upload services
- Global state management
- Frontend generic CRUD services
- Reusable authenticated server-stream (`text/event-stream`) client

### Boxty.ServerBase
Server-side infrastructure for building APIs with:
- Multi-tenant and subject-based authorization with OAuth2
- CRUD command and query patterns
- Keycloak integration
- Document handling endpoints
- Entity base classes with tenant isolation
- JWT authentication helpers
- Database context abstractions
- Reusable keyed in-memory event stream abstraction for realtime push scenarios

### Boxty.SharedBase
Shared models, DTOs, and interfaces used by both client and server:
- Data Transfer Objects (DTOs)
- Validation attributes
- Shared enums and helpers
- Common interfaces

## Features

- **Multi-tenancy Support**: Built-in tenant isolation at the entity and authorization level
- **Authentication & Authorization**: Integration with Keycloak for JWT-based auth
- **CRUD Operations**: Generic commands and queries for standard operations
- **Document Management**: Upload, storage, and retrieval of documents
- **Blazor Components**: Ready-to-use UI components for common scenarios
- **Type-safe**: Strongly typed throughout with C# generics

## Installation

Install the packages in the relevant inheriting projects (Client, Server, and Shared) via NuGet:

```bash
dotnet add package Boxty.ClientBase
dotnet add package Boxty.ServerBase
dotnet add package Boxty.SharedBase
```

## Usage

### Server Setup

[Click here for a demo repository](https://github.com/drmikesamy/Boxty.Demo)

```csharp
// Register your modules
var moduleTypes = new List<Type>
{
    typeof(AuthModule),
    typeof(UserManagementModule)
};

// Register your modules
...
var builder = WebApplication.CreateBuilder(args);

builder.Services.RegisterServices(builder.Configuration, ref moduleTypes, out var registeredModules);

var app = builder.Build();

app.ConfigureServicesAndMapEndpoints(builder.Environment.IsDevelopment() || builder.Environment.IsStaging(), registeredModules);
...
```

### Client Setup

```csharp
// Add your client services
builder.Services.AddScoped<ICrudService<TenantDto>, CrudService<TenantDto>>();
builder.Services.AddScoped<ICrudService<SubjectDto>, CrudService<SubjectDto>>();
builder.Services.AddScoped<ICrudService<TenantDocumentDto>, CrudService<TenantDocumentDto>>();
builder.Services.AddScoped<ICrudService<SubjectDocumentDto>, CrudService<SubjectDocumentDto>>();
builder.Services.AddScoped<ICrudService<TenantNoteDto>, CrudService<TenantNoteDto>>();
builder.Services.AddScoped<ICrudService<SubjectNoteDto>, CrudService<SubjectNoteDto>>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IAuthHelperService, AuthHelperService>();
builder.Services.AddScoped<ILocalBackupService, LocalBackupService>();
builder.Services.AddScoped<GlobalStateService>();
builder.Services.AddScoped<IServerEventStreamClient, ServerEventStreamClient>();
```

### Realtime Streaming Pattern (Reusable)

Boxty provides reusable primitives for module-level realtime updates without introducing custom auth flows:

- `Boxty.ServerBase.Interfaces.IKeyedEventStream<TKey,TEvent>`
- `Boxty.ServerBase.Services.InMemoryKeyedEventStream<TKey,TEvent>`
- `Boxty.ClientBase.Services.IServerEventStreamClient`
- `Boxty.ClientBase.Services.ServerEventStreamClient`

Server registration example:

```csharp
services.AddSingleton<IKeyedEventStream<Guid, MyEventDto>, InMemoryKeyedEventStream<Guid, MyEventDto>>();
```

Client consumption example:

```csharp
await serverEventStreamClient.StreamAsync<MyEventDto>("api/mymodule/events/stream", async evt =>
{
    // Update component state from streamed event
    await InvokeAsync(StateHasChanged);
}, cancellationToken);
```

This pattern works with standard Keycloak/JWT auth because requests flow through the normal authenticated `HttpClient` pipeline.

## Requirements

- .NET 8.0 or higher
- ASP.NET Core for server components
- Blazor for client components

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Author

Mike Samy (mike@boxty.org)
