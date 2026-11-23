[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    # Crea anche la release GitHub (tag vX.Y.Z) con ZIP e .sha256: usarlo solo quando si vuole davvero pubblicare.
    [switch]$Release
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $root "PizziniMaker.slnx"
$project = Join-Path $root "src\PizziniMaker.App\PizziniMaker.App.csproj"
$artifacts = Join-Path $root "artifacts"
$output = Join-Path $artifacts "portable\PizziniMaker"

[xml]$projectXml = Get-Content -LiteralPath $project -Raw
$version = ($projectXml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ } | Select-Object -First 1)
if (-not $version) {
    throw "Versione non trovata in $project."
}
$zipName = "PizziniMaker-$version-$Runtime.zip"
$zip = Join-Path $artifacts $zipName
$checksum = "$zip.sha256"

$dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
if (-not (Test-Path -LiteralPath $dotnet)) {
    $command = Get-Command dotnet -ErrorAction Stop
    $dotnet = $command.Source
}

function Invoke-DotNet {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    & $dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Comando dotnet non riuscito con codice $LASTEXITCODE."
    }
}

Write-Host "PizziniMaker $version"
Write-Host "Esecuzione test..."
Invoke-DotNet test $solution --configuration $Configuration --nologo --verbosity minimal

Write-Host "Pubblicazione self-contained..."
if (Test-Path -LiteralPath $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}
New-Item -ItemType Directory -Path $output -Force | Out-Null

$publishArguments = @(
    "publish", $project,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", "true",
    "--output", $output,
    "--nologo",
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:PublishReadyToRun=true",
    "-p:DebugType=none",
    "-p:DebugSymbols=false"
)
Invoke-DotNet -Arguments $publishArguments

# Il pacchetto non deve mai contenere dati personali: solo cartelle vuote e documentazione.
foreach ($personal in @("Impostazioni.json", "StoricoSchede.json", "Backup", "NoteSpese")) {
    $path = Join-Path $output $personal
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Recurse -Force
    }
}
New-Item -ItemType Directory -Path (Join-Path $output "Dati") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $output "Log") -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root "docs\GUIDA_UTENTE.md") -Destination (Join-Path $output "GUIDA_UTENTE.md") -Force

Get-ChildItem -LiteralPath $artifacts -Filter "PizziniMaker-*$Runtime.zip*" -File | Remove-Item -Force
Compress-Archive -LiteralPath $output -DestinationPath $zip -CompressionLevel Optimal

$exe = Join-Path $output "PizziniMaker.exe"
$exeInfo = Get-Item -LiteralPath $exe
$zipInfo = Get-Item -LiteralPath $zip
$hash = Get-FileHash -LiteralPath $zip -Algorithm SHA256
# Formato "HASH  nomefile" (compatibile con sha256sum), letto dall'aggiornamento automatico.
[System.IO.File]::WriteAllText($checksum, "$($hash.Hash)  $zipName`n", (New-Object System.Text.UTF8Encoding($false)))

Write-Host ""
Write-Host "Pacchetto completato: $zip"
Write-Host "Impronta: $checksum"
Write-Host ("Eseguibile: {0:N2} MB" -f ($exeInfo.Length / 1MB))
Write-Host ("ZIP: {0:N2} MB" -f ($zipInfo.Length / 1MB))
Write-Host "SHA256: $($hash.Hash)"

if ($Release) {
    $gh = Get-Command gh -ErrorAction Stop
    $tag = "v$version"
    Write-Host "Creazione della release GitHub $tag..."
    & $gh.Source release create $tag $zip $checksum --title "PizziniMaker $version" --notes "Vedere README.md e docs/GUIDA_UTENTE.md per le novità della versione $version."
    if ($LASTEXITCODE -ne 0) {
        throw "Creazione della release non riuscita."
    }
}
