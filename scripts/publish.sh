#!/bin/sh

if [ -z "$1" ]; then
    echo "Error: Version parameter is required."
    echo "Usage: $0 <version>"
    exit 1
fi

VERSION="$1"

git tag "$VERSION"
git push --tags

# Publish the .NET project with the specified version, targeting Linux x64 architecture
dotnet publish Console/Console.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishTrimmed=true \
    -p:PublishReadyToRun=true \
    -p:TieredPGO=true \
    /p:Version="$VERSION" \
    /p:PublishSingleFile=true
