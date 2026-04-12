# Keycloak Admin Integration

This document describes the current Keycloak admin split in the Boxty codebase.

## Current Ownership

`ServerBase` owns the reusable Keycloak integration layer:

- `IKeycloakService`
- Keycloak configuration and service registration
- Generic API infrastructure used by modules

`ServerBase` no longer owns user, tenant, or role management workflows.

Those orchestration concerns now live in the UserManagement module in the demo server:

- `BoxtyDemo/Boxty/ServerApp/Modules/UserManagement/Infrastructure/Commands`
- `BoxtyDemo/Boxty/ServerApp/Modules/UserManagement/Infrastructure/Queries`
- `BoxtyDemo/Boxty/ServerApp/Modules/UserManagement/Endpoints/KeycloakUserManagementEndpoints.cs`
- `BoxtyDemo/Boxty/ServerApp/Modules/UserManagement/Infrastructure/UserManagementModule.cs`

This keeps `ServerBase` generic and makes the identity/admin surface easier to move into a dedicated service later.

## Configuration

Ensure `appsettings.json` contains the Keycloak admin client configuration:

```json
{
  "KeycloakAdminClient": {
    "KeycloakUrl": "https://your-keycloak-instance.com",
    "Realm": "your-realm",
    "ClientId": "admin-cli",
    "ClientSecret": "your-client-secret"
  }
}
```

## ServerBase Service Surface

`IKeycloakService` exposes the low-level admin operations used by modules.

### User Operations

- `GetUserByIdAsync(string userId)`
- `GetUsersAsync(string search, int max)`
- `PostUsersAsync(UserRepresentation user)`
- `DeleteUserAsync(string userId)`
- `ResetUserPasswordAsync(string userId, CredentialRepresentation credential)`
- `UpdateUserAsync(string userId, UserRepresentation user)`

### Role Operations

- `GetAllRolesAsync()`
- `GetRoleByNameAsync(string roleName)`
- `GetUserRolesAsync(string userId)`
- `CreateRoleAsync(RoleRepresentation role)`
- `DeleteRoleAsync(string roleName)`
- `PostUserRoleMappingAsync(string userId, ICollection<RoleRepresentation> roles)`
- `DeleteUserRoleMappingAsync(string userId, ICollection<RoleRepresentation> roles)`

### Organization Operations

- `GetOrganizationsAsync(string? name)`
- `GetOrganizationByIdAsync(string orgId)`
- `PostOrganizationAsync(OrganizationRepresentation org)`
- `PostOrganizationMemberAsync(string orgId, string userId)`
- `DeleteOrganizationAsync(string orgId)`

## Module-Level Admin Workflows

The demo UserManagement module composes those low-level calls into application workflows.

### Commands

- `CreateSubjectCommand<T, TDto, TContext>`
- `DeleteSubjectCommand<T, TDto, TContext>`
- `CreateTenantCommand<T, TDto, TContext>`
- `DeleteTenantCommand<T, TDto, TContext>`
- `ResetPasswordCommand<T, TDto, TContext>`
- `AddUserRoleCommand`
- `RemoveUserRoleCommand`
- `UpdateUserRolesCommand`
- `CreateRoleCommand`
- `DeleteRoleCommand`

### Queries

- `GetUserRolesQuery`
- `GetAllRolesQuery`

### Endpoint Bases

- `KeycloakSubjectEndpoints<T, TDto, TContext>`
- `KeycloakTenantEndpoints<T, TDto, TContext>`
- `KeycloakRoleEndpoints<T, TContext>`

## Registration Example

Module-owned commands and queries are registered inside the module rather than globally in `ServerBase`.

```csharp
services.AddScoped(typeof(ICreateSubjectCommand<,,>), typeof(CreateSubjectCommand<,,>));
services.AddScoped(typeof(ICreateTenantCommand<,,>), typeof(CreateTenantCommand<,,>));
services.AddScoped(typeof(IResetPasswordCommand<,,>), typeof(ResetPasswordCommand<,,>));
services.AddScoped(typeof(IDeleteTenantCommand<,,>), typeof(DeleteTenantCommand<,,>));
services.AddScoped(typeof(IDeleteSubjectCommand<,,>), typeof(DeleteSubjectCommand<,,>));
services.AddScoped<IAddUserRoleCommand, AddUserRoleCommand>();
services.AddScoped<IRemoveUserRoleCommand, RemoveUserRoleCommand>();
services.AddScoped<IUpdateUserRolesCommand, UpdateUserRolesCommand>();
services.AddScoped<ICreateRoleCommand, CreateRoleCommand>();
services.AddScoped<IDeleteRoleCommand, DeleteRoleCommand>();
services.AddScoped<IGetUserRolesQuery, GetUserRolesQuery>();
services.AddScoped<IGetAllRolesQuery, GetAllRolesQuery>();
```

## Architectural Guidance

Keep these boundaries when adding more Keycloak-backed functionality:

- Put transport/client concerns in `ServerBase` only when they are generic and reusable.
- Put user, tenant, role, invitation, password-reset, and org-specific workflows in the owning module.
- Keep module endpoint behavior close to module commands and queries.
- Avoid reintroducing Keycloak admin orchestration into `ServerBase` unless every module genuinely needs it.

## Notes

1. The moved UserManagement workflows still rely on `IKeycloakService`; they did not replace the underlying Keycloak client abstraction.
2. This structure is intended to support future service extraction without forcing every `ServerBase` consumer to inherit identity-admin behavior.
3. Role names are normalized by the module commands where appropriate.
