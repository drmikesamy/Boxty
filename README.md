# Boxty

A comprehensive .NET framework providing base components for building multi-tenant applications with Blazor and ASP.NET Core.

## Packages

### Boxty.ClientBase
Client-side Blazor components and services including:
- Reusable UI components (CrudGrid, CrudForm, DocumentBrowser, etc.)
- Authentication helpers and message handlers
- Document upload services
- Global state management
- Frontend generic CRUD services

### Boxty.ServerBase
Server-side infrastructure for building APIs with:
- Multi-tenant and subject-based authorization with OAuth2
- CRUD command and query patterns
- Keycloak integration
- Document handling endpoints
- Entity base classes with tenant isolation
- JWT authentication helpers
- Database context abstractions

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
builder.Services.AddScoped<ILazyLookupService<TenantDto>, LazyLookupService<TenantDto>>();
builder.Services.AddScoped<ILazyLookupService<SubjectDto>, LazyLookupService<SubjectDto>>();
builder.Services.AddScoped<ILazyLookupService<TenantDocumentDto>, LazyLookupService<TenantDocumentDto>>();
builder.Services.AddScoped<ILazyLookupService<SubjectDocumentDto>, LazyLookupService<SubjectDocumentDto>>();
builder.Services.AddScoped<ILazyLookupService<TenantNoteDto>, LazyLookupService<TenantNoteDto>>();
builder.Services.AddScoped<ILazyLookupService<SubjectNoteDto>, LazyLookupService<SubjectNoteDto>>();
builder.Services.AddScoped<IDocumentUploadService, DocumentUploadService>();
builder.Services.AddScoped<IAuthHelperService, AuthHelperService>();
builder.Services.AddScoped<ILocalBackupService, LocalBackupService>();
builder.Services.AddScoped<GlobalStateService>();
```

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
