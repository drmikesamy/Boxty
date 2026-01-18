# ServerBase Test Suite

## Test Coverage Summary

This test suite provides comprehensive coverage for the Boxty ServerBase library focusing on CRUD operations.

### ✅ All Tests Passing (45/45) 🎉

All tests pass successfully using the **MockQueryable.Moq** library for proper EF Core async operation mocking!

#### Command Tests (25 tests)

1. **CreateCommand** (8 tests)
   - Null validation for dto, user, dbContext parameters
   - FluentValidation integration
   - Entity creation and DbSet.Add invocation
   - Mapper invocation for entity creation
   - SaveChangesWithAuditAsync invocation
   - skipAuth parameter functionality

2. **UpdateCommand** (8 tests)
   - Null validation for dto, user, dbContext parameters
   - FluentValidation failures  
   - KeyNotFoundException when entity doesn't exist
   - UnauthorizedAccessException on authorization failure
   - Successful update flow
   - Entity mapping and SaveChangesWithAuditAsync invocation

3. **DeleteCommand** (9 tests)
   - False return when entity doesn't exist
   - UnauthorizedAccessException on authorization failure
   - Successful deletion with DbSet.Remove
   - SaveChangesWithAuditAsync invocation
   - Multiple GUID scenarios (Theory tests)

#### Query Tests (20 tests)

4. **GetByIdQuery** (8 tests)
   - Null return when entity doesn't exist
   - UnauthorizedAccessException when authorization fails
   - Successful DTO return with authorization
   - Tenant ID filtering
   - Subject ID filtering
   - Authorization check before returning DTO
   - Entity-to-DTO mapping after authorization
   - Multiple GUID scenarios (Theory tests)

5. **GetAllQuery** (11 tests)
   - Empty list return when no entities exist
   - Return all authorized entities
   - Filter out unauthorized entities
   - Return empty list when all entities unauthorized
   - Tenant ID filtering
   - Subject ID filtering
   - Combined tenant and subject filtering
   - Map each authorized entity
   - Authorization check for each entity
   - Handle large number of entities (100 entities)

### Running Tests

```bash
# Run all tests (45 passing)
dotnet test Boxty.ServerBase.Tests

# Run only command tests
dotnet test Boxty.ServerBase.Tests --filter FullyQualifiedName~Commands

# Run only query tests  
dotnet test Boxty.ServerBase.Tests --filter FullyQualifiedName~Queries

# Verbose output
dotnet test Boxty.ServerBase.Tests --verbosity normal
```

### Test Infrastructure

- **Framework**: xUnit 3.1.4
- **Mocking**: Moq 4.20.72
- **Mock EF Core**: MockQueryable.Moq 10.0.1
- **Assertions**: FluentAssertions 8.8.0
- **Target**: .NET 10.0

### Key Implementation Detail

The query tests use **MockQueryable.Moq** library to properly mock EF Core async operations:

```csharp
private void SetupMockDbSet(IQueryable<TestEntity> entities)
{
    var mock = entities.ToList().BuildMockDbSet();
    _mockDbContext.Setup(db => db.Set<TestEntity>()).Returns(mock.Object);
}
```

This approach:
- ✅ Supports `ToListAsync()`, `SingleOrDefaultAsync()`, and other EF Core async methods
- ✅ Handles LINQ query transformations (Where, AsNoTracking, Include)
- ✅ Properly implements `IAsyncQueryProvider` and `IAsyncEnumerable<T>`
- ✅ Works with complex query scenarios including filtering and authorization

### Next Steps

1. **Additional Commands**: Add tests for CreateTenantCommand, CreateSubjectCommand, ResetPasswordCommand, SendEmailCommand, UploadCommand
2. **Additional Queries**: Add tests for GetByIdsQuery, GetByPredicateQuery, GetPagedQuery, SearchQuery
3. **Integration Tests**: Create integration test project for end-to-end scenarios
4. **E2E Tests**: Create end-to-end tests using WebApplicationFactory

### Test Results

```
Test summary: total: 45, failed: 0, succeeded: 45, skipped: 0, duration: 0.8s
```

All CRUD operations are fully tested and validated!
