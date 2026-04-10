#!/bin/bash
# Pack all Boxty projects to local nupkgs folder for development

set -e

# Get the script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )"
NUPKGS_DIR="$SCRIPT_DIR/nupkgs"
DEMO_NUPKGS_DIR="$SCRIPT_DIR/../BoxtyDemo/nupkgs"

# Create nupkgs directory if it doesn't exist
mkdir -p "$NUPKGS_DIR"

echo "🔨 Building and packing Boxty packages..."
echo "📦 Output directory: $NUPKGS_DIR"
echo ""

# Pack SharedBase
echo "📦 Packing Boxty.SharedBase..."
dotnet pack "$SCRIPT_DIR/SharedBase/Boxty.SharedBase.csproj" \
    --configuration Debug \
    --output "$NUPKGS_DIR" \
    /p:IncludeSymbols=true \
    /p:SymbolPackageFormat=snupkg

# Pack ServerBase
echo "📦 Packing Boxty.ServerBase..."
dotnet pack "$SCRIPT_DIR/ServerBase/Boxty.ServerBase.csproj" \
    --configuration Debug \
    --output "$NUPKGS_DIR" \
    /p:IncludeSymbols=true \
    /p:SymbolPackageFormat=snupkg

# Pack ClientBase
echo "📦 Packing Boxty.ClientBase..."
dotnet pack "$SCRIPT_DIR/ClientBase/Boxty.ClientBase.csproj" \
    --configuration Debug \
    --output "$NUPKGS_DIR" \
    /p:IncludeSymbols=true \
    /p:SymbolPackageFormat=snupkg

echo ""
echo "✅ All packages packed successfully!"
echo "📍 Packages are in: $NUPKGS_DIR"

if [ -d "$DEMO_NUPKGS_DIR" ]; then
    echo "📦 Syncing packages to demo feed: $DEMO_NUPKGS_DIR"
    cp -f "$NUPKGS_DIR"/*.nupkg "$DEMO_NUPKGS_DIR"/ 2>/dev/null || true
    cp -f "$NUPKGS_DIR"/*.snupkg "$DEMO_NUPKGS_DIR"/ 2>/dev/null || true
fi

echo ""
echo "To use in your demo project:"
echo "1. Add this to your demo project's NuGet.config:"
echo "   <add key=\"Boxty-Local\" value=\"$NUPKGS_DIR\" />"
echo "2. Run: dotnet restore"
echo "3. If packages don't update, clear cache: dotnet nuget locals all --clear"
