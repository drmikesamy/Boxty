# Pack all Boxty projects to local nupkgs folder for development

$ErrorActionPreference = "Stop"

# Get the script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$NupkgsDir = Join-Path $ScriptDir "nupkgs"

# Create nupkgs directory if it doesn't exist
New-Item -ItemType Directory -Force -Path $NupkgsDir | Out-Null

Write-Host "🔨 Building and packing Boxty packages..." -ForegroundColor Cyan
Write-Host "📦 Output directory: $NupkgsDir" -ForegroundColor Cyan
Write-Host ""

# Pack SharedBase
Write-Host "📦 Packing Boxty.SharedBase..." -ForegroundColor Yellow
dotnet pack "$ScriptDir/SharedBase/Boxty.SharedBase.csproj" `
    --configuration Debug `
    --output $NupkgsDir `
    /p:IncludeSymbols=true `
    /p:SymbolPackageFormat=snupkg

# Pack ServerBase
Write-Host "📦 Packing Boxty.ServerBase..." -ForegroundColor Yellow
dotnet pack "$ScriptDir/ServerBase/Boxty.ServerBase.csproj" `
    --configuration Debug `
    --output $NupkgsDir `
    /p:IncludeSymbols=true `
    /p:SymbolPackageFormat=snupkg

# Pack ClientBase
Write-Host "📦 Packing Boxty.ClientBase..." -ForegroundColor Yellow
dotnet pack "$ScriptDir/ClientBase/Boxty.ClientBase.csproj" `
    --configuration Debug `
    --output $NupkgsDir `
    /p:IncludeSymbols=true `
    /p:SymbolPackageFormat=snupkg

Write-Host ""
Write-Host "✅ All packages packed successfully!" -ForegroundColor Green
Write-Host "📍 Packages are in: $NupkgsDir" -ForegroundColor Green
Write-Host ""
Write-Host "To use in your demo project:" -ForegroundColor Cyan
Write-Host "1. Add this to your demo project's NuGet.config:"
Write-Host "   <add key=`"Boxty-Local`" value=`"$NupkgsDir`" />" -ForegroundColor White
Write-Host "2. Run: dotnet restore"
Write-Host "3. If packages don't update, clear cache: dotnet nuget locals all --clear"
