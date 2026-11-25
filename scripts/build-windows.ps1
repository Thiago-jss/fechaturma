[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

function Run([scriptblock]$Command) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "Comando de build falhou ($LASTEXITCODE)." }
}

Run { dotnet build src/FechaTurma.Web -c Release -p:RestoreLockedMode=true }
Run { dotnet build src/FechaTurma.Desktop -c Release -p:RestoreLockedMode=true }
Run { npm ci --prefix legacy-ui --ignore-scripts }
Run { npm ci --prefix frontend }
Run { npm run build --prefix frontend }

$site = Join-Path $root 'artifacts/site'
if (Test-Path $site) { Remove-Item $site -Recurse -Force }
New-Item -ItemType Directory -Path $site, "$site/bin", "$site/painel" -Force | Out-Null
$web = Join-Path $root 'src/FechaTurma.Web'
Copy-Item -Path "$web/Global.asax", "$web/Importacao.aspx", "$web/Web.config", "$web/Views", "$web/assets" -Destination $site -Recurse
Copy-Item "$web/bin/Release/net48/*.dll" "$site/bin"
Copy-Item 'legacy-ui/node_modules/jquery/dist/jquery.min.js' "$site/assets"
Copy-Item 'legacy-ui/node_modules/jquery/LICENSE.txt' "$site/assets/jquery-LICENSE.txt"

$index = @(Get-ChildItem -Path 'frontend/dist' -Filter index.html -File -Recurse)
if ($index.Count -ne 1) { throw 'O build do painel precisa produzir um único index.html.' }
Copy-Item "$($index[0].DirectoryName)/*" "$site/painel" -Recurse
Write-Host 'Site preparado em artifacts/site.'
