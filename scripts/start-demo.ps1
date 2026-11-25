[CmdletBinding()]
param([switch]$Smoke, [switch]$UseExistingDatabase, [ValidateSet('fixture','oracle')][string]$Source = 'fixture')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
$site = Join-Path $root 'artifacts/site'
if (!(Test-Path "$site/Web.config")) { throw 'Execute scripts/build-windows.ps1 primeiro.' }
$iis = Join-Path $env:ProgramFiles 'IIS Express/iisexpress.exe'
if (!(Test-Path $iis)) { throw 'Instale o IIS Express x64 ou execute scripts/ensure-iis-express.ps1.' }

function Invoke-Sql([string]$ConnectionString, [string]$CommandText) {
    $connection = New-Object System.Data.SqlClient.SqlConnection($ConnectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        try {
            $command.CommandText = $CommandText
            $command.CommandTimeout = 30
            [void]$command.ExecuteNonQuery()
        } finally { $command.Dispose() }
    } finally { $connection.Dispose() }
}

function Find-LocalDb {
    $command = Get-Command sqllocaldb.exe -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }
    foreach ($version in '170','160','150','140') {
        $candidate = Join-Path $env:ProgramFiles "Microsoft SQL Server/$version/Tools/Binn/SqlLocalDB.exe"
        if (Test-Path $candidate) { return $candidate }
    }
    throw 'Instale o SQL Server Express LocalDB (Visual Studio Installer: SQL Server Express LocalDB).'
}

function SecretValue {
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $bytes = New-Object byte[] 24
        $generator.GetBytes($bytes)
        return [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
    } finally { $generator.Dispose() }
}

if (!$UseExistingDatabase) {
    $localdb = Find-LocalDb
    & $localdb start MSSQLLocalDB | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Não foi possível iniciar a instância LocalDB ($LASTEXITCODE)." }
    $database = if ($Smoke) { 'FechaTurmaSmoke_' + [Guid]::NewGuid().ToString('N') } else { 'FechaTurmaDemo' }
    $master = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;Connect Timeout=8;Database=master'
    Invoke-Sql $master "IF DB_ID(N'$database') IS NULL CREATE DATABASE [$database];"
    $env:FT_SQL_CONNECTION = 'Server=(localdb)\MSSQLLocalDB;Integrated Security=True;Connect Timeout=8;Database=' + $database
    Invoke-Sql $env:FT_SQL_CONNECTION (Get-Content 'database/sqlserver/001_closures.sql' -Raw)
} elseif (!$env:FT_SQL_CONNECTION) { throw 'Configure FT_SQL_CONNECTION para o banco já migrado.' }

$env:FT_ACADEMIC_SOURCE = $Source
if ($Source -eq 'oracle' -and !$env:FT_ORACLE_CONNECTION) { throw 'Configure FT_ORACLE_CONNECTION para uma origem aprovisionada.' }
if (!$Smoke -and ([string]::IsNullOrEmpty($env:FT_IMPORT_KEY) -or [string]::IsNullOrEmpty($env:FT_DESKTOP_KEY))) {
    throw 'Configure FT_IMPORT_KEY e FT_DESKTOP_KEY com chaves locais distintas de pelo menos 32 caracteres.'
}
if ($Smoke) {
    $env:FT_IMPORT_KEY = SecretValue
    $env:FT_DESKTOP_KEY = SecretValue
}
if ($env:FT_IMPORT_KEY.Length -lt 32 -or $env:FT_DESKTOP_KEY.Length -lt 32 -or $env:FT_IMPORT_KEY -ceq $env:FT_DESKTOP_KEY) {
    throw 'Chaves do laboratório inválidas.'
}

$argsForIis = @(('/path:"' + $site + '"'), '/port:5184', '/clr:v4.0')
if (!$Smoke) {
    Write-Host 'FechaTurma disponível em http://localhost:5184 (somente loopback).'
    & $iis @argsForIis
    exit $LASTEXITCODE
}

$stdout = Join-Path $root 'artifacts/iis.stdout.log'
$stderr = Join-Path $root 'artifacts/iis.stderr.log'
$server = Start-Process $iis -ArgumentList $argsForIis -RedirectStandardOutput $stdout -RedirectStandardError $stderr -PassThru
try {
    $healthy = $false
    for ($i = 0; $i -lt 40; $i++) {
        $server.Refresh()
        if ($server.HasExited) { throw "IIS Express encerrou com código $($server.ExitCode)." }
        try {
            $response = Invoke-RestMethod -Uri 'http://localhost:5184/api/v1/health' -TimeoutSec 3
            if ($response.status -eq 'ok') { $healthy = $true; break }
        } catch { Start-Sleep -Milliseconds 500 }
    }
    if (!$healthy) { throw 'IIS Express não respondeu /api/v1/health.' }
    & npm run test --prefix tests/browser
    if ($LASTEXITCODE -ne 0) { throw "O teste no navegador falhou ($LASTEXITCODE)." }
} finally {
    if (!$server.HasExited) { Stop-Process -Id $server.Id -Force }
}
