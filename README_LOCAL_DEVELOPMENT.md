# Local Development & Debugging Guide

This guide explains how to develop and debug the Boxty NuGet packages with your demo project without publishing to a remote feed.

## Quick Setup

### For Your Demo Project

1. **Create a NuGet.Config file** in your demo project root:
   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <add key="Boxty-Local" value="/home/mike/Projects/Boxty/nupkgs" />
       <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
     </packageSources>
   </configuration>
   ```
   
   ⚠️ **Important**: Update the path to match where your Boxty folder is located.

2. **Reference the packages** in your demo project's `.csproj`:
   ```xml
   <ItemGroup>
     <PackageReference Include="Boxty.SharedBase" Version="1.0.*" />
     <PackageReference Include="Boxty.ServerBase" Version="1.0.*" />
     <PackageReference Include="Boxty.ClientBase" Version="1.0.*" />
   </ItemGroup>
   ```
   
   Using `1.0.*` allows automatic pickup of new patch versions.

## Development Workflow

### Step 1: Make Changes to Boxty Packages
Edit your code in `ServerBase`, `ClientBase`, or `SharedBase` projects.

### Step 2: Pack the Packages Locally

Run the pack script from the Boxty folder:

**Linux/Mac:**
```bash
./pack-local.sh
```

**Windows PowerShell:**
```powershell
.\pack-local.ps1
```

**Or manually:**
```bash
dotnet pack --configuration Debug --output ./nupkgs /p:IncludeSymbols=true
```

### Step 3: Update Demo Project

In your demo project directory:

```bash
# Clear the NuGet cache
dotnet nuget locals all --clear

# Restore packages (picks up new versions)
dotnet restore

# Build and run
dotnet build
dotnet run
```

### Step 4: Debug

You can debug into the Boxty packages because:
- Debug builds are used (`--configuration Debug`)
- Symbol packages (`.snupkg`) are included
- Source link information is embedded

**In VS Code:**
1. Set breakpoints in both your demo project AND the Boxty source files
2. Start debugging your demo project (F5)
3. VS Code will hit breakpoints in the Boxty source code

## Version Management

### Increment Version Before Packing

To avoid caching issues, increment the version in the `.csproj` files before packing:

**SharedBase/Boxty.SharedBase.csproj:**
```xml
<PropertyGroup>
  <Version>1.0.3</Version> <!-- Increment this -->
</PropertyGroup>
```

Do the same for `ServerBase` and `ClientBase`.

### Or Use Wildcard Versions

In your demo project, use wildcard versions to always get the latest:
```xml
<PackageReference Include="Boxty.ServerBase" Version="1.0.*" />
```

## Troubleshooting

### Problem: Demo project doesn't pick up new changes

**Solution:**
```bash
# 1. Clear all NuGet caches
dotnet nuget locals all --clear

# 2. Delete bin and obj folders in demo project
rm -rf bin obj

# 3. Restore and rebuild
dotnet restore --force
dotnet build --no-incremental
```

### Problem: "Package not found" error

**Solution:**
- Check that the path in `NuGet.Config` is correct and absolute
- Verify `.nupkg` files exist in the `nupkgs` folder
- Run `pack-local.sh` to create packages if missing

### Problem: Breakpoints don't hit in Boxty code

**Solution:**
- Ensure you're packing with `--configuration Debug`
- Check that symbol packages (`.snupkg`) are generated
- In VS Code, enable "Just My Code" debugging:
  ```json
  // .vscode/launch.json
  {
    "justMyCode": false
  }
  ```

### Problem: Old package version is cached

**Solution:**
```bash
# Find and remove specific package from cache
dotnet nuget locals all --list
# Then manually delete the Boxty.* folders from the cache locations

# Or clear everything
dotnet nuget locals all --clear
```

## Alternative: Direct Project References (Best for Debugging)

If you want the absolute best debugging experience, use project references instead of package references:

### Option 1: Add projects to demo solution

Add the Boxty projects to your demo solution:
```bash
dotnet sln add ../Boxty/SharedBase/Boxty.SharedBase.csproj
dotnet sln add ../Boxty/ServerBase/Boxty.ServerBase.csproj
dotnet sln add ../Boxty/ClientBase/Boxty.ClientBase.csproj
```

### Option 2: Use ProjectReference in demo .csproj

Replace `PackageReference` with `ProjectReference`:
```xml
<ItemGroup>
  <!-- <PackageReference Include="Boxty.ServerBase" Version="1.0.*" /> -->
  <ProjectReference Include="../Boxty/ServerBase/Boxty.ServerBase.csproj" />
  <ProjectReference Include="../Boxty/SharedBase/Boxty.SharedBase.csproj" />
  <ProjectReference Include="../Boxty/ClientBase/Boxty.ClientBase.csproj" />
</ItemGroup>
```

**Pros:**
- ✅ Changes are immediately reflected (no packing needed)
- ✅ Perfect debugging experience
- ✅ Faster development iteration

**Cons:**
- ❌ Not testing actual package deployment
- ❌ Need to switch back to PackageReference before release

## Scripts Reference

### pack-local.sh / pack-local.ps1
Packs all three Boxty projects with debug symbols to `nupkgs` folder.

### Quick Commands

```bash
# Pack packages
./pack-local.sh

# Update demo project (run in demo folder)
dotnet nuget locals all --clear && dotnet restore && dotnet build

# Watch for changes and auto-rebuild (in Boxty folder)
dotnet watch --project ServerBase/Boxty.ServerBase.csproj
```

## Best Practices

1. **Use semantic versioning**: Increment patch version (1.0.x) for bug fixes
2. **Clear cache frequently**: NuGet caching can be aggressive
3. **Use wildcards in demo**: `Version="1.0.*"` auto-picks latest
4. **Document breaking changes**: Update version appropriately (minor/major)
5. **Test as packages**: Before release, always test with actual packages, not project references

## Multi-Window Development

Since you have Boxty in one VS Code window and demo in another:

1. **In Boxty window**: Make changes, run `./pack-local.sh`
2. **In demo window**: Run `dotnet restore && dotnet build && dotnet run`
3. Set breakpoints in both windows
4. Start debugging from demo window - it will step into Boxty code

VS Code will handle cross-window debugging automatically! 🎉
