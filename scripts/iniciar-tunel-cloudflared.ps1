<#
.SYNOPSIS
    Abre um cloudflared quick tunnel para a API local e grava a URL pública
    em src\SigeraAssociados.Api\urlpublica.local.json (chave UrlPublica:BaseUrl).

.DESCRIPTION
    O botão "Confirmar e-mail" do e-mail de cadastro usa essa URL.
    A API recarrega o arquivo sozinha: pode abrir o túnel com a API já rodando.
    A URL do quick tunnel muda a cada execução; ao encerrar (Ctrl+C),
    o túnel é fechado e o arquivo é apagado.

    Pré-requisito: winget install --id Cloudflare.cloudflared

.PARAMETER Porta
    Porta HTTP local da API (launchSettings: 5092).

.EXAMPLE
    .\scripts\iniciar-tunel-cloudflared.ps1
#>
param(
    [int]$Porta = 5092,
    [int]$TempoLimiteSegundos = 60
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$projetoApi = Join-Path $raiz 'src\SigeraAssociados.Api'
$arquivoUrl = Join-Path $projetoApi 'urlpublica.local.json'
$chave = 'UrlPublica:BaseUrl'

if (-not (Get-Command cloudflared -ErrorAction SilentlyContinue)) {
    Write-Host 'cloudflared não encontrado. Instale com:' -ForegroundColor Red
    Write-Host '  winget install --id Cloudflare.cloudflared'
    Write-Host 'Depois feche e abra o PowerShell.'
    exit 1
}

# Remove a URL antiga dos User Secrets (versão anterior do script),
# para não sobrar um endereço que já deixou de existir.
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    dotnet user-secrets remove $chave --project $projetoApi 2>$null | Out-Null
}

# Confere se a API está respondendo na porta local.
try {
    Invoke-WebRequest -Uri "http://localhost:$Porta/swagger/index.html" -UseBasicParsing -TimeoutSec 5 -MaximumRedirection 0 -ErrorAction Stop | Out-Null
}
catch {
    if (-not $_.Exception.Response) {
        Write-Host "Aviso: a API não respondeu em http://localhost:$Porta. Suba a API antes de clicar no link do e-mail." -ForegroundColor Yellow
    }
}

# O cloudflared escreve o log (com a URL gerada) na saída de erro.
$log = Join-Path $env:TEMP "sigera-cloudflared-$PID.log"
if (Test-Path $log) { Remove-Item $log -Force }

Write-Host "Iniciando túnel para http://localhost:$Porta ..."

$processo = Start-Process -FilePath 'cloudflared' `
    -ArgumentList @('tunnel', '--no-autoupdate', '--url', "http://localhost:$Porta") `
    -RedirectStandardError $log `
    -NoNewWindow `
    -PassThru

$url = $null
$limite = (Get-Date).AddSeconds($TempoLimiteSegundos)

while (-not $url -and (Get-Date) -lt $limite -and -not $processo.HasExited) {
    Start-Sleep -Milliseconds 500

    if (Test-Path $log) {
        $conteudo = Get-Content $log -Raw -ErrorAction SilentlyContinue
        $achado = [regex]::Match([string]$conteudo, 'https://[a-z0-9-]+\.trycloudflare\.com')

        if ($achado.Success) {
            $url = $achado.Value
        }
    }
}

if (-not $url) {
    Write-Host 'Não foi possível obter a URL do túnel. Últimas linhas do log:' -ForegroundColor Red
    if (Test-Path $log) { Get-Content $log -Tail 20 }
    if (-not $processo.HasExited) { Stop-Process -Id $processo.Id -Force }
    exit 1
}

# Grava a URL no arquivo que a API recarrega automaticamente.
$json = @{ UrlPublica = @{ BaseUrl = $url } } | ConvertTo-Json -Depth 3
[System.IO.File]::WriteAllText($arquivoUrl, $json, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ''
Write-Host "URL pública : $url" -ForegroundColor Green
Write-Host "Gravada em  : $arquivoUrl"
Write-Host "Teste       : $url/swagger"
Write-Host "Log         : $log"
Write-Host ''
Write-Host 'Agora reenvie a confirmação (Postman 05.5) ou crie um usuário (05.2).'
Write-Host 'Mantenha esta janela aberta até clicar no link do e-mail.'
Write-Host 'Ctrl+C encerra o túnel.' -ForegroundColor Yellow

try {
    Wait-Process -Id $processo.Id
}
finally {
    if (-not $processo.HasExited) {
        Stop-Process -Id $processo.Id -Force
    }

    # Sem o arquivo, a API volta a enviar só o token.
    if (Test-Path $arquivoUrl) { Remove-Item $arquivoUrl -Force }
    Write-Host 'Túnel encerrado e URL pública removida.'
}
