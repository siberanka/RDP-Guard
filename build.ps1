$ErrorActionPreference = 'Stop'

$csc = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    throw 'C# derleyicisi bulunamadi.'
}

$framework = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$outDir = Join-Path $PSScriptRoot 'bin\Release'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$manifest = Join-Path $PSScriptRoot 'app.manifest'
$icon = Join-Path $PSScriptRoot 'assets\rdpguard.ico'
$outFile = Join-Path $outDir 'RDPGuard.exe'

$sources = Get-ChildItem -Path (Join-Path $PSScriptRoot 'src') -Filter '*.cs' | Sort-Object FullName | ForEach-Object { $_.FullName }
$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Xml.dll',
    'System.Xml.Linq.dll'
) | ForEach-Object { '/reference:' + (Join-Path $framework $_) }

& $csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    /langversion:latest `
    "/win32manifest:$manifest" `
    "/win32icon:$icon" `
    "/out:$outFile" `
    @refs `
    @sources

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Write-Host "Build hazir: $((Resolve-Path '.\bin\Release\RDPGuard.exe').Path)"
