# Keycloak Admin Service - User and Role Management

This document explains how to use the Keycloak admin service principal to programmatically manage users, roles, and role assignments without manual Keycloak login.

## Overview

The Boxty ServerBase now includes comprehensive commands and queries for managing Keycloak users and roles using the admin client service principal configured in your `appsettings.json`.

## Configuration

Ensure your `appsettings.json` has the KeycloakAdminClient configuration:

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

## Available Services

### KeycloakService Methods

The `IKeycloakService` now includes:

#### User Operations
- `GetUserByIdAsync(string userId)` - Get user details
- `GetUsersAsync(string search, int max)` - Search for users
- `PostUsersAsync(UserRepresentation user)` - Create a new user
- `DeleteUserAsync(string userId)` - Delete a user
- `ResetUserPasswordAsync(string userId, CredentialRepresentation credential)` - Reset password
- `UpdateUserAsync(string userId, UserRepresentation user)` - Update user details

#### Role Operations
- `GetAllRolesAsync()` - Get all roles in the realm
- `GetRoleByNameAsync(string roleName)` - Get a specific role
- `GetUserRolesAsync(string userId)` - Get all roles assigned to a user
- `CreateRoleAsync(RoleRepresentation role)` - Create a new role
- `DeleteRoleAsync(string roleName)` - Delete a role
- `PostUserRoleMappingAsync(string userId, ICollection<RoleRepresentation> roles)` - Add roles to user
- `DeleteUserRoleMappingAsync(string userId, ICollection<RoleRepresentation> roles)` - Remove roles from user

#### Organization Operations
- `GetOrganizationsAsync(string? name)` - Get organizations
- `GetOrganizationByIdAsync(string orgId)` - Get specific organization
- `PostOrganizationAsync(OrganizationRepresentation org)` - Create organization
- `PostOrganizationMemberAsync(string orgId, string userId)` - Add user to organization
- `DeleteOrganizationAsync(string orgId)` - Delete organization

## Commands

### 1. AddUserRoleCommand

Add one or more roles to a user.

```csharp
public interface IAddUserRoleCommand
{
    Task<bool> Handle(Guid userId, List<string> roleNames, ClaimsPrincipal user);
}
```

**Example Usage:**
```csharp
var result = await _addUserRoleCommand.Handle(
    userId: Guid.Parse("user-id-here"),
    roleNames: new List<string> { "admin", "manager" },
    user: HttpContext.User
);
```

### 2. RemoveUserRoleCommand

Remove one or more roles from a user.

```csharp
public interface IRemoveUserRoleCommand
{
    Task<bool> Handle(Guid userId, List<string> roleNames, ClaimsPrincipal user);
}
```

**Example Usage:**
```csharp
var result = await _removeUserRoleCommand.Handle(
    userId: Guid.Parse("user-id-here"),
    roleNames: new List<string> { "manager" },
    user: HttpContext.User
);
```

### 3. UpdateUserRolesCommand

Completely replace a user's roles (removes all current roles and adds new ones).

```csharp
public interface IUpdateUserRolesCommand
{
    Task<bool> Handle(Guid userId, List<string> roleNames, ClaimsPrincipal user);
}
```

**Example Usage:**
```csharp
var result = await _updateUserRolesCommand.Handle(
    userId: Guid.Parse("user-id-here"),
    roleNames: new List<string> { "user", "viewer" }, // User will only have these roles
    user: HttpContext.User
);
```

### 4. ManageRoleCommand

Create or delete roles in Keycloak.

```csharp
public interface IManageRoleCommand
{
    Task<bool> CreateRole(string roleName, string? description, ClaimsPrincipal user);
    Task<bool> DeleteRole(string roleName, ClaimsPrincipal user);
}
```

**Example Usage:**
```csharp
// Create a role
await _manageRoleCommand.CreateRole(
    roleName: "custom-role",
    description: "Custom role for specific permissions",
    user: HttpContext.User
);

// Delete a role
await _manageRoleCommand.DeleteRole(
    roleName: "old-role",
    user: HttpContext.User
);
```

## Queries

### 1. GetUserRolesQuery

Get all roles assigned to a specific user.

```csharp
public interface IGetUserRolesQuery
{
    Task<ICollection<RoleRepresentation>> Handle(Guid userId, ClaimsPrincipal user);
}
```

**Example Usage:**
```csharp
var userRoles = await _getUserRolesQuery.Handle(
    userId: Guid.Parse("user-id-here"),
    user: HttpContext.User
);

foreach (var role in userRoles)
{
    Console.WriteLine($"Role: {role.Name}, Description: {role.Description}");
}
```

### 2. GetAllRolesQuery

Get all available roles in the realm.

```csharp
public interface IGetAllRolesQuery
{
    Task<ICollection<RoleRepresentation>> Handle(ClaimsPrincipal user);
}
```

**Example Usage:**
```csharp
var allRoles = await _getAllRolesQuery.Handle(user: HttpContext.User);

foreach (var role in allRoles)
{
    Console.WriteLine($"Role: {role.Name}");
}
```

## Endpoint Integration Example

Here's how to create API endpoints using these commands and queries:

```csharp
public class UserRoleEndpoints : BaseEndpoints<User, UserDto, YourDbContext>
{
    private readonly IAddUserRoleCommand _addUserRoleCommand;
    private readonly IRemoveUserRoleCommand _removeUserRoleCommand;
    private readonly IUpdateUserRolesCommand _updateUserRolesCommand;
    private readonly IGetUserRolesQuery _getUserRolesQuery;
    
    public UserRoleEndpoints(/* inject dependencies */)
    {
        // Constructor
    }
    
    public override void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/user-roles");
        
        // Get user roles
        group.MapGet("/{userId:guid}", async (Guid userId) =>
        {
            var roles = await _getUserRolesQuery.Handle(userId, HttpContext.User);
            return Results.Ok(roles);
        });
        
        // Add roles to user
        group.MapPost("/{userId:guid}/add", async (Guid userId, List<string> roleNames) =>
        {
            var result = await _addUserRoleCommand.Handle(userId, roleNames, HttpContext.User);
            return Results.Ok(result);
        });
        
        // Remove roles from user
        group.MapPost("/{userId:guid}/remove", async (Guid userId, List<string> roleNames) =>
        {
            var result = await _removeUserRoleCommand.Handle(userId, roleNames, HttpContext.User);
            return Results.Ok(result);
        });
        
        // Update user roles (replace all)
        group.MapPut("/{userId:guid}", async (Guid userId, List<string> roleNames) =>
        {
            var result = await _updateUserRolesCommand.Handle(userId, roleNames, HttpContext.User);
            return Results.Ok(result);
        });
    }
}

public class RoleEndpoints : BaseEndpoints<Role, RoleDto, YourDbContext>
{
    private readonly IManageRoleCommand _manageRoleCommand;
    private readonly IGetAllRolesQuery _getAllRolesQuery;
    
    public RoleEndpoints(/* inject dependencies */)
    {
        // Constructor
    }
    
    public override void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/roles");
        
        // Get all roles
        group.MapGet("/", async () =>
        {
            var roles = await _getAllRolesQuery.Handle(HttpContext.User);
            return Results.Ok(roles);
        });
        
        // Create role
        group.MapPost("/", async (CreateRoleRequest request) =>
        {
            var result = await _manageRoleCommand.CreateRole(
                request.RoleName,
                request.Description,
                HttpContext.User
            );
            return Results.Ok(result);
        });
        
        // Delete role
        group.MapDelete("/{roleName}", async (string roleName) =>
        {
            var result = await _manageRoleCommand.DeleteRole(roleName, HttpContext.User);
            return Results.Ok(result);
        });
    }
}
```

## Common Workflows

### 1. Create a User with Roles

```csharp
// Use existing CreateSubjectCommand (already includes user creation and role assignment)
var userId = await _createSubjectCommand.Handle(userDto, HttpContext.User);

// Or manually:
var userRep = new UserRepresentation
{
    FirstName = "John",
    LastName = "Doe",
    Username = "john.doe@example.com",
    Email = "john.doe@example.com",
    Enabled = true,
    Credentials = new List<CredentialRepresentation>
    {
        new CredentialRepresentation
        {
            Type = "password",
            Value = "temporary-password",
            Temporary = true
        }
    }
};

await _keycloakService.PostUsersAsync(userRep);
var users = await _keycloakService.GetUsersAsync("john.doe@example.com", 1);
var newUserId = Guid.Parse(users.First().Id);

// Add roles
await _addUserRoleCommand.Handle(newUserId, new List<string> { "user" }, HttpContext.User);
```

### 2. Change User Roles

```csharp
// Option A: Update all roles at once (replace)
await _updateUserRolesCommand.Handle(
    userId,
    new List<string> { "admin", "manager" },
    HttpContext.User
);

// Option B: Add specific roles
await _addUserRoleCommand.Handle(
    userId,
    new List<string> { "admin" },
    HttpContext.User
);

// Option C: Remove specific roles
await _removeUserRoleCommand.Handle(
    userId,
    new List<string> { "manager" },
    HttpContext.User
);
```

### 3. Manage Roles

```csharp
// Create a new role
await _manageRoleCommand.CreateRole(
    "custom-viewer",
    "Can view custom reports",
    HttpContext.User
);

// Delete a role
await _manageRoleCommand.DeleteRole("deprecated-role", HttpContext.User);

// Get all roles to display in UI
var allRoles = await _getAllRolesQuery.Handle(HttpContext.User);
```

### 4. Audit User Roles

```csharp
// Get a user's current roles
var userId = Guid.Parse("user-id");
var currentRoles = await _getUserRolesQuery.Handle(userId, HttpContext.User);

Console.WriteLine($"User {userId} has the following roles:");
foreach (var role in currentRoles)
{
    Console.WriteLine($"  - {role.Name}: {role.Description}");
}
```

## Error Handling

All commands and queries include error handling and will throw:

- `UnauthorizedAccessException` - When user is not authenticated or doesn't have permission
- `InvalidOperationException` - When the operation fails (e.g., role not found, user doesn't exist)

Example:
```csharp
try
{
    await _addUserRoleCommand.Handle(userId, roleNames, HttpContext.User);
}
catch (UnauthorizedAccessException ex)
{
    // Handle authorization failure
    return Results.Unauthorized();
}
catch (InvalidOperationException ex)
{
    // Handle operation failure (role not found, etc.)
    return Results.BadRequest(ex.Message);
}
```

## Authorization

The commands include basic authorization checks. You should enhance these based on your requirements:

```csharp
private void ValidateAuthorization(ClaimsPrincipal user)
{
    if (!user.Identity?.IsAuthenticated ?? true)
    {
        throw new UnauthorizedAccessException("User must be authenticated.");
    }
    
    // Add your custom checks:
    // - Check if user has specific role (e.g., "admin")
    // - Check if user belongs to specific organization
    // - Check custom permissions
}
```

## Notes

1. **Service Principal Setup**: Ensure your Keycloak admin client service principal has the necessary permissions in Keycloak (realm management, user management, etc.)

2. **Role Names**: Role names are automatically converted to lowercase when creating or searching for roles.

3. **Thread Safety**: All Keycloak API clients are created and disposed per request using `using` statements.

4. **Existing Users**: When creating users with `CreateSubjectCommand`, it automatically handles user creation, organization assignment, and role assignment in one transaction.

5. **Idempotency**: Some operations (like adding a role a user already has) may throw exceptions. Handle these appropriately in your code.
