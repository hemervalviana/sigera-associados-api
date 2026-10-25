<#
.SYNOPSIS
    Publica e instala a SigeraAssociados API como serviço do Windows (Kestrel).

.DESCRIPTION
    Execute no servidor, em um PowerShell **como Administrador**, a partir
    da pasta do projeto (onde está o SigeraAssociados.slnx).

    O que faz:
    1. Para o serviço, se já existir.
    2. Publica a API em Release na pasta de instalação.
    3. Cria (ou atualiza) o serviço com início automático.
    4. Grava as variáveis de ambiente do serviço (senhas, chave JWT etc.).
    5. Configura reinício automático em caso de falha.
    6. Libera a porta no firewall do Windows.
    7. Inicia o serviço e testa o /health.

    Pré-requisitos no servidor:
    - .NET SDK 10 (para publicar) ou o ASP.NET Core Runtime 10 (se publicar em outra máquina).
    - Certificado HTTPS instalado em Certificados do Computador Local > Pessoal,
      com o nome configurado em appsettings.Production.json (Kestrel).

.PARAMETER ConfigurarVariaveis
    Pede e grava as variáveis de ambiente do serviço. Use na primeira
    instalação ou quando alguma senha mudar. Sem ele, mantém as atuais.

.EXAMPLE
    .\scripts\instalar-servico-windows.ps1 -ConfigurarVariaveis

.EXAMPLE
    .\scripts\instalar-servico-windows.ps1
    (atualiza a versão mantendo as variáveis)
#>
param(
    [string]$PastaInstalacao = 'C:\SigeraAssociados\api',
    [string]$NomeServico = 'SigeraAssociadosApi',
    [int]$Porta = 443,
    [switch]$ConfigurarVariaveis
)

$ErrorActionPreference = 'Stop'

$identidade = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $identidade.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host 'Execute este script em um PowerShell como Administrador.' -ForegroundColor Red
    exit 1
}

$raiz = Split-Path -Parent $PSScriptRoot
$projetoApi = Join-Path $raiz 'src\SigeraAssociados.Api'
$executavel = Join-Path $PastaInstalacao 'SigeraAssociados.Api.exe'
$chaveRegistro = "HKLM:\SYSTEM\CurrentControlSet\Services\$NomeServico"

function Ler-Texto([string]$rotulo, [string]$padrao = '') {
    $sufixo = if ($padrao) { " [$padrao]" } else { '' }
    $valor = Read-Host "$rotulo$sufixo"
    if ([string]::IsNullOrWhiteSpace($valor)) { return $padrao }
    return $valor.Trim()
}

function Ler-Segredo([string]$rotulo) {
    $seguro = Read-Host $rotulo -AsSecureString
    $ponteiro = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($seguro)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ponteiro) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ponteiro) }
}

# 1. Para o serviço, se existir.
$servico = Get-Service -Name $NomeServico -ErrorAction SilentlyContinue
if ($servico -and $servico.Status -ne 'Stopped') {
    Write-Host "Parando o serviço $NomeServico..."
    Stop-Service -Name $NomeServico -Force
    $servico.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
}

# 2. Publica.
Write-Host "Publicando em $PastaInstalacao..."
dotnet publish $projetoApi -c Release -o $PastaInstalacao
if ($LASTEXITCODE -ne 0) { throw 'Falha no dotnet publish.' }

# Nunca leva a URL temporária do túnel para produção.
$urlLocal = Join-Path $PastaInstalacao 'urlpublica.local.json'
if (Test-Path $urlLocal) { Remove-Item $urlLocal -Force }

# 3. Cria o serviço, se ainda não existir.
if (-not $servico) {
    Write-Host "Criando o serviço $NomeServico..."
    New-Service -Name $NomeServico `
        -BinaryPathName "`"$executavel`"" `
        -DisplayName 'SigeraAssociados API' `
        -Description 'API REST de consulta de associados (PROASP, SONASP, ABSP, GSP).' `
        -StartupType Automatic | Out-Null
    $ConfigurarVariaveis = $true
}

# 4. Variáveis de ambiente do serviço (ficam no registro do serviço).
if ($ConfigurarVariaveis) {
    Write-Host ''
    Write-Host 'Variáveis de ambiente do serviço' -ForegroundColor Cyan
    Write-Host 'Deixe em branco para usar o valor entre colchetes.'

    $servidorSql = Ler-Texto 'Servidor SQL (nome ou IP)'
    $senhaSql = Ler-Segredo 'Senha do usr_sisgera_api'
    $urlPublica = Ler-Texto 'URL pública HTTPS da API (ex.: https://api.suaempresa.com.br)'
    $gmailUsuario = Ler-Texto 'Conta de envio de e-mail'
    $gmailSenha = Ler-Segredo 'Senha de app do e-mail'

    $chaveJwt = Ler-Segredo 'Chave JWT de produção (em branco = gerar uma nova)'
    if ([string]::IsNullOrWhiteSpace($chaveJwt)) {
        $bytes = New-Object byte[] 64
        [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
        $chaveJwt = [Convert]::ToBase64String($bytes)
        Write-Host 'Chave JWT nova gerada.' -ForegroundColor Yellow
    }

    $variaveis = @(
        'ASPNETCORE_ENVIRONMENT=Production',
        "ConnectionSettings__Server=$servidorSql",
        "ConnectionSettings__Password=$senhaSql",
        "IdentityConnectionSettings__Server=$servidorSql",
        "IdentityConnectionSettings__Password=$senhaSql",
        "Jwt__SigningKey=$chaveJwt",
        'Email__Gmail__Host=smtp.gmail.com',
        'Email__Gmail__Port=587',
        "Email__Gmail__User=$gmailUsuario",
        "Email__Gmail__Password=$gmailSenha",
        "UrlPublica__BaseUrl=$urlPublica"
    )

    New-ItemProperty -Path $chaveRegistro -Name 'Environment' -PropertyType MultiString -Value $variaveis -Force | Out-Null
    Write-Host 'Variáveis gravadas no serviço.' -ForegroundColor Green
}

# 5. Reinício automático: 3 tentativas (5 s, 5 s, 30 s); zera após 1 dia.
sc.exe failure $NomeServico reset= 86400 actions= restart/5000/restart/5000/restart/30000 | Out-Null

# 6. Firewall.
$regra = "SigeraAssociados API ($Porta)"
if (-not (Get-NetFirewallRule -DisplayName $regra -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $regra -Direction Inbound -Protocol TCP -LocalPort $Porta -Action Allow | Out-Null
    Write-Host "Porta $Porta liberada no firewall."
}

# 7. Inicia e testa.
Write-Host "Iniciando o serviço $NomeServico..."
Start-Service -Name $NomeServico
(Get-Service -Name $NomeServico).WaitForStatus('Running', [TimeSpan]::FromSeconds(30))
Start-Sleep -Seconds 3

# (Variáveis do PowerShell não diferenciam maiúsculas: não reutilizar $Porta.)
$sufixoPorta = if ($Porta -eq 443) { '' } else { ":$Porta" }
$saude = curl.exe -s -k "https://localhost$sufixoPorta/health"
$pronto = curl.exe -s -k "https://localhost$sufixoPorta/health/pronto"

Write-Host ''
Write-Host "/health        : $saude"
Write-Host "/health/pronto : $pronto"
Write-Host ''
Write-Host 'Se algo falhar, veja o Visualizador de Eventos > Logs do Windows > Aplicativo.' -ForegroundColor Yellow
