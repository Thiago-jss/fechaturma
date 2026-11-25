[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$path = Join-Path $env:ProgramFiles 'IIS Express/iisexpress.exe'
if (Test-Path $path) { exit 0 }
$msi = Join-Path $env:TEMP 'fechaturma-iisexpress.msi'
$url = 'https://download.microsoft.com/download/c/e/8/ce8d18f5-d4c0-45b5-b531-adecd637a1aa/iisexpress_amd64_en-US.msi'
$hash = '18304fe8a65e397c65fe77c6e73b0acb1556e8ed7ec9c94678dd42fa7ac1671f'
try {
    Invoke-WebRequest -UseBasicParsing -Uri $url -OutFile $msi
    if ((Get-FileHash $msi -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash) { throw 'Hash do instalador IIS Express não confere.' }
    if ((Get-AuthenticodeSignature $msi).Status -ne 'Valid') { throw 'Assinatura do instalador IIS Express inválida.' }
    $installer = Start-Process msiexec.exe -ArgumentList @('/i', ('"' + $msi + '"'), '/quiet', '/norestart') -PassThru -Wait
    if ($installer.ExitCode -notin 0,3010 -or !(Test-Path $path)) { throw "IIS Express não instalado: $($installer.ExitCode)." }
} finally {
    if (Test-Path $msi) { Remove-Item -LiteralPath $msi -Force }
}
