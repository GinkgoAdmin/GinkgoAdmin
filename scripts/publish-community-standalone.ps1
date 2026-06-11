# GinkgoAdmin community standalone publish script
# Usage: .\scripts\publish-community-standalone.ps1 [-Runtime linux-x64] [-SkipWeb]

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "",
    [string]$OutputDir = "dist/publish",
    [switch]$SkipWeb,
    [switch]$SkipDbSanitize
)

$ErrorActionPreference = "Stop"

function Find-RepoRoot {
    $dir = (Get-Location).Path
    for ($i = 0; $i -lt 8; $i++) {
        $api = Join-Path $dir "src\Server\Ginkgo.Api\Ginkgo.Api.csproj"
        if (Test-Path -LiteralPath $api) { return $dir }
        $parent = Split-Path -LiteralPath $dir -Parent
        if (-not $parent -or $parent -eq $dir) { break }
        $dir = $parent
    }
    throw "Repo root not found. Need src/Server/Ginkgo.Api/Ginkgo.Api.csproj"
}

function Copy-DirContents {
    param(
        [string]$Source,
        [string]$Destination,
        [string[]]$ExcludeNames = @()
    )
    if (-not (Test-Path -LiteralPath $Source)) { return }
    if (-not (Test-Path -LiteralPath $Destination)) {
        New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    }
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        if ($ExcludeNames -contains $_.Name) { return }
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $Destination $_.Name) -Recurse -Force
    }
}

function Get-CsprojAssemblyName {
    param([string]$CsprojPath)
    try {
        [xml]$xml = Get-Content -LiteralPath $CsprojPath -Raw -Encoding UTF8
        $node = $xml.Project.PropertyGroup.AssemblyName | Where-Object { $_ } | Select-Object -First 1
        if ($node) { return [string]$node }
    }
    catch { }
    return [System.IO.Path]::GetFileNameWithoutExtension($CsprojPath)
}

function Read-ModuleJsonField {
    param(
        [string]$JsonPath,
        [string]$FieldName,
        [string]$Default = ""
    )
    if (-not (Test-Path -LiteralPath $JsonPath)) { return $Default }
    try {
        $obj = Get-Content -LiteralPath $JsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($null -ne $obj.PSObject.Properties[$FieldName]) {
            $val = $obj.$FieldName
            if ($val) { return [string]$val }
        }
    }
    catch { }
    return $Default
}

function Test-IsAlcPrivateAssembly {
    param([string]$AssemblyFileName)
    # 与 AssemblyIsolatedLoadContext 中 isExcluded 前缀保持一致：
    # 这些程序集不会从宿主 Default 上下文共享，必须从模块目录加载模块编译时的版本。
    $name = [System.IO.Path]::GetFileNameWithoutExtension($AssemblyFileName)
    return (
        $name.StartsWith("System.ClientModel", [StringComparison]::OrdinalIgnoreCase) -or
        $name.StartsWith("System.Memory.Data", [StringComparison]::OrdinalIgnoreCase) -or
        $name.StartsWith("System.Net.ServerSentEvents", [StringComparison]::OrdinalIgnoreCase) -or
        $name.StartsWith("Microsoft.Extensions.AI", [StringComparison]::OrdinalIgnoreCase) -or
        $name.StartsWith("OpenAI", [StringComparison]::OrdinalIgnoreCase) -or
        $name.StartsWith("Azure.", [StringComparison]::OrdinalIgnoreCase)
    )
}

function Copy-ModuleDlls {
    param(
        [string]$BuildDir,
        [string]$EntryDll,
        [string]$TargetDir,
        [string[]]$HostDllNamesLower
    )
    $dllFilter = "*.dll"
    Get-ChildItem -LiteralPath $BuildDir -Filter $dllFilter -ErrorAction SilentlyContinue | ForEach-Object {
        $isEntry = ($_.Name -eq $EntryDll)
        $isHost = $HostDllNamesLower -contains $_.Name.ToLowerInvariant()
        $isAlcPrivate = Test-IsAlcPrivateAssembly -AssemblyFileName $_.Name
        if ($isEntry -or $isAlcPrivate -or -not $isHost) {
            Copy-Item -LiteralPath $_.FullName -Destination $TargetDir -Force
        }
    }
}

function Copy-OptionalSubDir {
    param(
        [string]$SourceSubDir,
        [string]$TargetDir,
        [string]$SubName
    )
    if (-not (Test-Path -LiteralPath $SourceSubDir)) { return }
    $out = Join-Path $TargetDir $SubName
    if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $SourceSubDir "*") -Destination $out -Recurse -Force
}

function Resolve-ModuleVersion {
    param(
        [string]$ManifestPath,
        [string]$EntryDllPath,
        [string]$Default = "1.0.0"
    )
    $version = Read-ModuleJsonField -JsonPath $ManifestPath -FieldName "version" -Default $Default
    if (-not (Test-Path -LiteralPath $EntryDllPath)) { return $version }
    try {
        $asmVer = [System.Reflection.AssemblyName]::GetAssemblyName($EntryDllPath).Version
        if ($asmVer -and $asmVer.Build -ge 0) {
            return "$($asmVer.Major).$($asmVer.Minor).$($asmVer.Build)"
        }
    }
    catch { }
    return $version
}

# 写入生产环境标准 manifest：modules/{id}/{version}/module.json
# entryAssembly 相对版本目录，例如 server/Ginkgo.Module.X.dll 或 server/bin/Ginkgo.Module.X.dll
function Write-VersionRootModuleManifest {
    param(
        [string]$VersionDir,
        [string]$SourceManifestPath,
        [string]$ModId,
        [string]$Version,
        [string]$EntryAssemblyFromVersionRoot,
        [bool]$HasWeb
    )

    $manifestObj = [ordered]@{
        id        = $ModId
        version   = $Version
        hasClient = $HasWeb
        server    = @{ entryAssembly = $EntryAssemblyFromVersionRoot }
    }

    if (Test-Path -LiteralPath $SourceManifestPath) {
        try {
            $src = Get-Content -LiteralPath $SourceManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            foreach ($field in @("name", "publisher", "description", "homepage", "author", "title", "tablePrefix", "SupportedClients", "dependencies", "minAppVersion")) {
                if ($null -ne $src.PSObject.Properties[$field] -and $src.$field) {
                    $manifestObj[$field] = $src.$field
                }
            }
            if (-not $manifestObj.name) { $manifestObj.name = $ModId }
        }
        catch { }
    }
    if (-not $manifestObj.name) { $manifestObj.name = $ModId }

    $outManifest = Join-Path $VersionDir "module.json"
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($outManifest, ($manifestObj | ConvertTo-Json -Depth 6), $utf8NoBom)
}

function Copy-CompiledModulePackage {
    param(
        [string]$ModDirFullName,
        [string]$ModName,
        [string]$ServerDir,
        [string]$ManifestPath,
        [string]$ModulesOutputDir
    )

    $modId = Read-ModuleJsonField -JsonPath $ManifestPath -FieldName "id" -Default $modName
    $modVersion = Read-ModuleJsonField -JsonPath $ManifestPath -FieldName "version" -Default "0.0.0"
    $versionDir = Join-Path $ModulesOutputDir (Join-Path $modId $modVersion)
    $targetServerDir = Join-Path $versionDir "server"

    if (Test-Path -LiteralPath $versionDir) {
        Remove-Item -LiteralPath $versionDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $targetServerDir -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $ServerDir "*") -Destination $targetServerDir -Recurse -Force

    foreach ($rootFile in @("install-manifest.json", "README.md", "LICENSE", "CHANGELOG.md")) {
        $rootPath = Join-Path $ModDirFullName $rootFile
        if (Test-Path -LiteralPath $rootPath) {
            Copy-Item -LiteralPath $rootPath -Destination $versionDir -Force
        }
    }

    Copy-OptionalSubDir -SourceSubDir (Join-Path $ModDirFullName "web") -TargetDir $versionDir -SubName "web"
    $hasWeb = Test-Path -LiteralPath (Join-Path $versionDir "web")

    $entryAssembly = ""
    try {
        $srcManifest = Get-Content -LiteralPath $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($srcManifest.server -and $srcManifest.server.entryAssembly) {
            $entryAssembly = [string]$srcManifest.server.entryAssembly
        }
    }
    catch { }

    if ([string]::IsNullOrWhiteSpace($entryAssembly)) {
        $entryAssembly = "bin/$modId.dll"
    }
    $entryAssembly = $entryAssembly.TrimStart('/', '\').Replace('\', '/')
    if ($entryAssembly.StartsWith("server/", [StringComparison]::OrdinalIgnoreCase)) {
        $versionRootEntry = $entryAssembly
    }
    else {
        $versionRootEntry = "server/$entryAssembly"
    }

    Write-VersionRootModuleManifest `
        -VersionDir $versionDir `
        -SourceManifestPath $ManifestPath `
        -ModId $modId `
        -Version $modVersion `
        -EntryAssemblyFromVersionRoot $versionRootEntry `
        -HasWeb $hasWeb

    # 避免 ScanProductionModules 递归扫描到 server/module.json 后重复加载同一模块
    $serverManifest = Join-Path $targetServerDir "module.json"
    if (Test-Path -LiteralPath $serverManifest) {
        Remove-Item -LiteralPath $serverManifest -Force
    }

    return @{
        ModId   = $modId
        Version = $modVersion
    }
}

function Sanitize-DbJson {
    param([string]$DbJsonPath)
    if (-not (Test-Path -LiteralPath $DbJsonPath)) { return }
    $dbContent = Get-Content -LiteralPath $DbJsonPath -Raw -Encoding UTF8
    $dbContent = $dbContent -replace '(?i)(User ID|Uid)=[^;]+', '${1}=GinkgoAdmin'
    $dbContent = $dbContent -replace '(?i)(Password|Pwd)=[^;]+', '${1}=GinkgoAdmin'
    $dbContent = $dbContent -replace '(?i)Database=[^;]+', 'Database=GinkgoAdmin'
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($DbJsonPath, $dbContent, $utf8NoBom)
}

# --- main ---

$repoRoot = Find-RepoRoot
Set-Location -LiteralPath $repoRoot

$apiCsproj = Join-Path $repoRoot "src\Server\Ginkgo.Api\Ginkgo.Api.csproj"
$moduleRoot = Join-Path $repoRoot "src\Module"
$webDir = Join-Path $repoRoot "web"

if ([System.IO.Path]::IsPathRooted($OutputDir)) {
    $publishDir = $OutputDir
}
else {
    $publishDir = Join-Path $repoRoot $OutputDir
}

$resourceSrc = $null
$candidates = @(
    (Join-Path $repoRoot "resource"),
    (Join-Path $repoRoot "src\Server\Ginkgo.Api\resource")
)
foreach ($candidate in $candidates) {
    if (Test-Path -LiteralPath $candidate) {
        $resourceSrc = $candidate
        break
    }
}

if (Test-Path -LiteralPath $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " GinkgoAdmin publish (community standalone)" -ForegroundColor Cyan
Write-Host " Output: $publishDir" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

Write-Host ""
Write-Host "[1/4] dotnet publish API..." -ForegroundColor Yellow

$publishArgs = @(
    "publish", $apiCsproj,
    "-c", $Configuration,
    "-o", $publishDir,
    "/p:DisableCompiledModuleCollection=true"
)
if ($Runtime) {
    $publishArgs += @("-r", $Runtime, "--self-contained", "false")
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed, exit code $LASTEXITCODE"
}
Write-Host "  API published." -ForegroundColor Green

$hostDllNamesLower = @(
    Get-ChildItem -LiteralPath $publishDir -Filter "*.dll" | ForEach-Object { $_.Name.ToLowerInvariant() }
)

Write-Host ""
Write-Host "[2/4] Pack modules to modules/ ..." -ForegroundColor Yellow

$modulesOutputDir = Join-Path $publishDir "modules"
New-Item -ItemType Directory -Path $modulesOutputDir -Force | Out-Null

$packedSource = 0
$packedCompiled = 0
$failed = @()

if (-not (Test-Path -LiteralPath $moduleRoot)) {
    Write-Host "  src/Module not found, skip." -ForegroundColor DarkYellow
}
else {
    foreach ($modDir in (Get-ChildItem -LiteralPath $moduleRoot -Directory)) {
        $modName = $modDir.Name
        if ($modName -notlike "Ginkgo.Module.*") { continue }

        $serverDir = Join-Path $modDir.FullName "server"
        if (-not (Test-Path -LiteralPath $serverDir)) {
            Write-Host "  skip $modName (no server/)" -ForegroundColor DarkGray
            continue
        }

        $csproj = Get-ChildItem -LiteralPath $serverDir -Filter "*.csproj" -ErrorAction SilentlyContinue | Select-Object -First 1
        $manifestPath = Join-Path $serverDir "module.json"

        if (-not $csproj) {
            $binDir = Join-Path $serverDir "bin"
            $hasDll = $false
            if (Test-Path -LiteralPath $binDir) {
                $firstDll = Get-ChildItem -LiteralPath $binDir -Filter "*.dll" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
                $hasDll = ($null -ne $firstDll)
            }
            if (-not $hasDll -or -not (Test-Path -LiteralPath $manifestPath)) {
                Write-Host "  skip $modName (not a compiled DLL package)" -ForegroundColor DarkGray
                continue
            }

            $modId = Read-ModuleJsonField -JsonPath $manifestPath -FieldName "id" -Default $modName
            $modVersion = Read-ModuleJsonField -JsonPath $manifestPath -FieldName "version" -Default "0.0.0"

            $info = Copy-CompiledModulePackage `
                -ModDirFullName $modDir.FullName `
                -ModName $modName `
                -ServerDir $serverDir `
                -ManifestPath $manifestPath `
                -ModulesOutputDir $modulesOutputDir

            Write-Host "  [compiled] $($info.ModId) v$($info.Version) -> modules/$($info.ModId)/$($info.Version)/" -ForegroundColor Green
            $packedCompiled++
            continue
        }

        Write-Host "  [source] build $modName ..." -ForegroundColor Gray
        & dotnet build $csproj.FullName -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            $failed += $modName
            Write-Host "  FAILED build: $modName" -ForegroundColor Red
            continue
        }

        $assemblyName = Get-CsprojAssemblyName -CsprojPath $csproj.FullName
        $entryDll = "$assemblyName.dll"
        $buildDir = Join-Path $serverDir ("bin\{0}\net8.0" -f $Configuration)
        $entryDllPath = Join-Path $buildDir $entryDll

        if (-not (Test-Path -LiteralPath $entryDllPath)) {
            $failed += $modName
            Write-Host "  FAILED missing: $entryDllPath" -ForegroundColor Red
            continue
        }

        $modId = Read-ModuleJsonField -JsonPath $manifestPath -FieldName "id" -Default $modName
        $version = Resolve-ModuleVersion -ManifestPath $manifestPath -EntryDllPath $entryDllPath -Default "1.0.0"
        $versionDir = Join-Path $modulesOutputDir (Join-Path $modId $version)
        $targetServerDir = Join-Path $versionDir "server"

        if (Test-Path -LiteralPath $versionDir) {
            Remove-Item -LiteralPath $versionDir -Recurse -Force
        }
        New-Item -ItemType Directory -Path $targetServerDir -Force | Out-Null

        Copy-ModuleDlls -BuildDir $buildDir -EntryDll $entryDll -TargetDir $targetServerDir -HostDllNamesLower $hostDllNamesLower
        Copy-OptionalSubDir -SourceSubDir (Join-Path $serverDir "sql") -TargetDir $targetServerDir -SubName "sql"
        Copy-OptionalSubDir -SourceSubDir (Join-Path $serverDir "config") -TargetDir $targetServerDir -SubName "config"
        Copy-OptionalSubDir -SourceSubDir (Join-Path $serverDir "data") -TargetDir $targetServerDir -SubName "data"
        Copy-OptionalSubDir -SourceSubDir (Join-Path $modDir.FullName "web") -TargetDir $versionDir -SubName "web"

        $installJson = Join-Path $serverDir "install.json"
        if (Test-Path -LiteralPath $installJson) {
            Copy-Item -LiteralPath $installJson -Destination $targetServerDir -Force
        }

        $hasWeb = Test-Path -LiteralPath (Join-Path $versionDir "web")
        $versionRootEntry = "server/$entryDll"
        Write-VersionRootModuleManifest `
            -VersionDir $versionDir `
            -SourceManifestPath $manifestPath `
            -ModId $modId `
            -Version $version `
            -EntryAssemblyFromVersionRoot $versionRootEntry `
            -HasWeb $hasWeb

        Write-Host "  [source] $modId v$version -> modules/$modId/$version/" -ForegroundColor Green
        $packedSource++
    }
}

Write-Host ""
Write-Host "  modules: source=$packedSource compiled=$packedCompiled" -ForegroundColor Cyan
if ($failed.Count -gt 0) {
    Write-Host "  failed: $($failed -join ', ')" -ForegroundColor Red
}

$manifestCount = @(
    Get-ChildItem -LiteralPath $modulesOutputDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        Get-ChildItem -LiteralPath $_.FullName -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            Join-Path $_.FullName "module.json"
        }
    } | Where-Object { Test-Path -LiteralPath $_ }
).Count

if ($manifestCount -eq 0) {
    Write-Host ""
    Write-Host "  WARN: no modules/{id}/{version}/module.json under modules/" -ForegroundColor Red
    Write-Host "  Install plugins first under src/Module/Ginkgo.Module.*/server/" -ForegroundColor Yellow
}
else {
    Write-Host ""
    Write-Host "  production layout: modules/{id}/{version}/module.json + server/ + web/" -ForegroundColor DarkCyan
}

Write-Host ""
Write-Host "[3/4] Copy resource/ ..." -ForegroundColor Yellow

if ($resourceSrc) {
    $resourceDst = Join-Path $publishDir "resource"
    if (-not (Test-Path -LiteralPath $resourceDst)) {
        New-Item -ItemType Directory -Path $resourceDst -Force | Out-Null
    }
    Copy-DirContents -Source $resourceSrc -Destination $resourceDst

    $rootResource = Join-Path $repoRoot "resource"
    if ((Test-Path -LiteralPath $rootResource) -and ($rootResource -ne $resourceSrc)) {
        Copy-DirContents -Source $rootResource -Destination $resourceDst
    }

    if (-not $SkipDbSanitize) {
        Sanitize-DbJson -DbJsonPath (Join-Path $resourceDst "db.json")
        Write-Host "  db.json sanitized (use -SkipDbSanitize to keep values)." -ForegroundColor DarkCyan
    }

    Write-Host "  resource/ copied." -ForegroundColor Green
}
else {
    Write-Host "  resource/ not found, skip." -ForegroundColor DarkYellow
}

if ($SkipWeb) {
    Write-Host ""
    Write-Host "[4/4] Skip WEB (-SkipWeb)." -ForegroundColor DarkYellow
}
elseif (-not (Test-Path -LiteralPath (Join-Path $webDir "package.json"))) {
    Write-Host ""
    Write-Host "[4/4] web/package.json not found, skip WEB." -ForegroundColor DarkYellow
}
else {
    Write-Host ""
    Write-Host "[4/4] Build WEB ..." -ForegroundColor Yellow

    $pluginDepsScript = Join-Path $webDir "scripts\install-plugin-deps.cjs"
    Push-Location -LiteralPath $webDir
    try {
        if (-not (Test-Path -LiteralPath "node_modules")) {
            Write-Host "  npm install ..." -ForegroundColor Gray
            & npm install
            if ($LASTEXITCODE -ne 0) { throw "npm install failed" }
        }

        if (Test-Path -LiteralPath $pluginDepsScript) {
            Write-Host "  install plugin npm deps ..." -ForegroundColor Gray
            & node $pluginDepsScript
        }

        Write-Host "  npm run build ..." -ForegroundColor Gray
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "npm run build failed" }

        $webDist = Join-Path $webDir "dist"
        $wwwrootDir = Join-Path $publishDir "wwwroot"
        if (Test-Path -LiteralPath $webDist) {
            if (Test-Path -LiteralPath $wwwrootDir) {
                Remove-Item -LiteralPath $wwwrootDir -Recurse -Force
            }
            Copy-Item -LiteralPath $webDist -Destination $wwwrootDir -Recurse -Force
            Write-Host "  WEB copied to wwwroot/" -ForegroundColor Green
        }
        else {
            Write-Host "  web/dist not found." -ForegroundColor Red
        }
    }
    finally {
        Pop-Location
    }
}

Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host " Done" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Output: $publishDir" -ForegroundColor Green
Write-Host "  API + modules/ + resource/ + wwwroot/" -ForegroundColor Gray
Write-Host ""
Write-Host "Deploy:" -ForegroundColor Yellow
Write-Host "  1. Upload entire output folder to server (keep modules/{id}/{version}/ layout)" -ForegroundColor Yellow
Write-Host "  2. Edit resource/db.json on server" -ForegroundColor Yellow
Write-Host "  3. dotnet Ginkgo.Api.dll in publish folder" -ForegroundColor Yellow
Write-Host "  4. Check log: [Modules] Module registered: ..." -ForegroundColor Yellow
Write-Host "  5. First deploy: run module install SQL (install.json SqlScripts) if menus missing" -ForegroundColor Yellow
Write-Host ""
Write-Host "Module layout example:" -ForegroundColor DarkGray
Write-Host "  modules/Ginkgo.Module.Evaluate/0.1.0/module.json" -ForegroundColor DarkGray
Write-Host "  modules/Ginkgo.Module.Evaluate/0.1.0/server/Ginkgo.Module.Evaluate.dll" -ForegroundColor DarkGray
Write-Host "  modules/Ginkgo.Module.Evaluate/0.1.0/server/install.json" -ForegroundColor DarkGray
Write-Host "  modules/Ginkgo.Module.Evaluate/0.1.0/server/sql/install.sql" -ForegroundColor DarkGray
Write-Host ""

if ($manifestCount -eq 0) {
    exit 1
}
