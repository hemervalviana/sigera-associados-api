<#
.SYNOPSIS
    Remove o serviço do Windows da SigeraAssociados API.

.DESCRIPTION
    Execute como Administrador. Para e exclui o serviço e a regra de firewall.
    Os arquivos da pasta de instalação são mantidos.
#>
param(
    [string]$NomeServico = 'SigeraAssociadosApi',
    [int]$Porta = 443
)

$ErrorActionPreference = 'Stop'

$servico = Get-Service -Name $NomeServico -ErrorAction SilentlyContinue

if ($servico) {
    if ($servico.Status -ne 'Stopped') {
        Stop-Service -Name $NomeServico -Force
        $servico.WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    }
    sc.exe delete $NomeServico | Out-Null
    Write-Host "Serviço $NomeServico removido."
}
else {
    Write-Host "Serviço $NomeServico não encontrado."
}

$regra = "SigeraAssociados API ($Porta)"
if (Get-NetFirewallRule -DisplayName $regra -ErrorAction SilentlyContinue) {
    Remove-NetFirewallRule -DisplayName $regra
    Write-Host "Regra de firewall '$regra' removida."
}
