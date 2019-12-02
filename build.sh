#!/bin/sh

nuget install Newtonsoft.Json -Version 12.0.3 -OutputDirectory packages
nuget install TiledLib -Version 2.3.0 -OutputDirectory packages

dotnet build DeBroglie.Console
dotnet build DeBroglie.MagicaVoxel
dotnet build DeBroglie.Test
dotnet build DeBroglie.Tiled
dotnet build DeBroglie.Tiled.Test
dotnet build DeBroglie
