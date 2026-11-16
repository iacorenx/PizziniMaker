[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$solution = Join-Path $root "PizziniMaker.slnx"
$project = Join-Path $root "src\PizziniMaker.App\PizziniMaker.App.csproj"
$artifacts = Join-Path $root "artifacts"
$output = Join-Path $artifacts "portable\PizziniMaker"
$zip = Join-Path $artifacts "PizziniMaker-$Runtime.zip"

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

New-Item -ItemType Directory -Path (Join-Path $output "Dati") -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $output "Log") -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $root "docs\GUIDA_UTENTE.md") -Destination (Join-Path $output "GUIDA_UTENTE.md") -Force

if (Test-Path -LiteralPath $zip) {
    Remove-Item -LiteralPath $zip -Force
}
Compress-Archive -LiteralPath $output -DestinationPath $zip -CompressionLevel Optimal

$exe = Join-Path $output "PizziniMaker.exe"
$exeInfo = Get-Item -LiteralPath $exe
$zipInfo = Get-Item -LiteralPath $zip
$hash = Get-FileHash -LiteralPath $zip -Algorithm SHA256

Write-Host ""
Write-Host "Pacchetto completato: $zip"
Write-Host ("Eseguibile: {0:N2} MB" -f ($exeInfo.Length / 1MB))
Write-Host ("ZIP: {0:N2} MB" -f ($zipInfo.Length / 1MB))
Write-Host "SHA256: $($hash.Hash)"
