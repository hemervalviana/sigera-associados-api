# SigeraAssociados API — Implantação em produção

Guia para levar a API do computador de desenvolvimento para um servidor Windows, onde ela roda no Kestrel como serviço do Windows (seção 8).

## 1. Como a configuração funciona em produção

A API lê a configuração nesta ordem (a última vence):

1. `appsettings.json`: valores comuns.
2. `appsettings.Production.json`: valores de produção, **sem senhas** (versionado no Git).
3. **Variáveis de ambiente** do servidor: senhas, chaves e endereços. Os User Secrets **não** são usados fora de Development.

O ambiente é definido pela variável `ASPNETCORE_ENVIRONMENT=Production`. Em produção, o Swagger fica desligado e o HSTS ligado.

## 2. Variáveis de ambiente obrigatórias

O separador de seção é `__` (dois sublinhados).

| Variável | Exemplo / observação |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionSettings__Server` | Nome ou IP do SQL Server das 4 bases |
| `ConnectionSettings__Password` | Senha do `usr_sisgera_api` |
| `IdentityConnectionSettings__Server` | Servidor do `SigeraIdentity` |
| `IdentityConnectionSettings__Password` | Senha do `usr_sisgera_api` |
| `Jwt__SigningKey` | Chave nova, gerada só para produção (64 bytes em Base64) |
| `Email__Gmail__Host` | `smtp.gmail.com` (ou o provedor de produção) |
| `Email__Gmail__Port` | `587` |
| `Email__Gmail__User` | Conta de envio |
| `Email__Gmail__Password` | Senha de app |
| `UrlPublica__BaseUrl` | Endereço público HTTPS da API, ex.: `https://api.suaempresa.com.br` |

Opcionais: `Jwt__AccessTokenMinutes` (15), `Jwt__RefreshTokenDias` (7), `ConnectionSettings__CompatibilityLevel` (100).

Gerar a chave JWT de produção (nunca reutilize a de desenvolvimento):

```powershell
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
[Convert]::ToBase64String($bytes)
```

## 3. Certificado do SQL Server

Em produção, `TrustServerCertificate` é `false`: o SQL Server precisa de um certificado confiável para o servidor da API. Se o SQL usa certificado autoassinado, a conexão falha com erro de certificado. Nesse caso, instale um certificado válido no SQL Server (recomendado) ou, provisoriamente, defina `ConnectionSettings__TrustServerCertificate=true` e `IdentityConnectionSettings__TrustServerCertificate=true`.

## 4. Banco de dados

1. Login da API: `scripts\criar-login-usr_sisgera_api.sql` (como `sa`).
2. Índice de nome: `scripts\criar-indice-nome-t000pess.sql` (como `sa` ou DBA).
3. Tabelas do `SigeraIdentity`: gere o script SQL das migrations e entregue ao DBA:

```powershell
dotnet ef migrations script --idempotent `
    --project src\SigeraAssociados.Infrastructure `
    --startup-project src\SigeraAssociados.Api `
    --context SigeraIdentityDbContext `
    --output scripts\sigeraidentity-migrations.sql
```

   O script é idempotente: aplica só as migrations que faltam.

4. Primeiro administrador: o seed só roda em Development. Em produção, crie a conta pela API de um ambiente de desenvolvimento apontado para o banco de produção, ou peça ao DBA para copiar o usuário administrador.

## 5. Publicar

```powershell
dotnet publish src\SigeraAssociados.Api -c Release -o publish
```

A pasta `publish` contém tudo o que vai para o servidor.

## 6. Conferir depois de subir

| Verificação | Esperado |
|---|---|
| `GET /health` | **200**, `"status": "Healthy"` (a API está no ar) |
| `GET /health/pronto` | **200** com `SigeraIdentity`, `PROASP`, `SONASP`, `ABSP` e `GSP` em `Healthy`. Se algum banco falhar: **503**, e o motivo fica no log |
| `POST /api/auth/login` | **200** com `accessToken` e `refreshToken` |
| `GET /swagger` | **404** (desligado em produção) |
| Coleção do Postman, pastas 07 e 08 | Tudo PASS, com `baseUrl` apontando para o servidor |

Use `/health` no monitoramento de "processo vivo" e `/health/pronto` para saber se a API consegue atender (bancos acessíveis).

## 7. Checklist

- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Variáveis de ambiente da seção 2 configuradas
- [ ] Chave JWT nova, só de produção
- [ ] `appsettings.Production.json` com o servidor SQL correto, ou as variáveis `*__Server`
- [ ] Login `usr_sisgera_api` criado e com `CHECK_POLICY = ON`
- [ ] Migrations aplicadas no `SigeraIdentity`
- [ ] Certificado do SQL Server confiável (ou decisão registrada)
- [ ] HTTPS com certificado válido no endereço público
- [ ] `/health` e `/health/pronto` respondendo 200
- [ ] Primeiro administrador criado
- [ ] Rate limiting conferido atrás do proxy (IP real do cliente)

## 8. Servidor: serviço do Windows com Kestrel

Decisão (24/10/2026): a API roda no **Kestrel**, como em desenvolvimento, instalada como **serviço do Windows**. Assim ela inicia com o servidor, sem ninguém logado, e o Windows a reinicia se cair. Não há IIS.

| | Desenvolvimento | Produção |
|---|---|---|
| Como inicia | `dotnet run` (janela aberta) | Serviço `SigeraAssociadosApi`, início automático |
| Executável | Compilado na hora | `C:\SigeraAssociados\api\SigeraAssociados.Api.exe` (publicado) |
| Ambiente | `Development` | `Production` |
| Configuração secreta | User Secrets | Variáveis de ambiente do serviço |
| HTTPS | Certificado de desenvolvimento, porta 7001 | Certificado real do servidor, porta 443 |
| Logs | Console | Visualizador de Eventos > Aplicativo |
| Swagger | Ligado | Desligado |

### 8.1 Pré-requisitos no servidor

1. Windows Server com acesso ao SQL Server.
2. **.NET SDK 10** (para publicar no próprio servidor) ou, se publicar em outra máquina, o **ASP.NET Core Runtime 10** (Hosting Bundle).
3. **Certificado HTTPS** do endereço público (ex.: `api.suaempresa.com.br`) instalado em *Certificados do Computador Local > Pessoal*.
4. Em `src\SigeraAssociados.Api\appsettings.Production.json`, preencha:
   - `ConnectionSettings:Server` e `IdentityConnectionSettings:Server` (ou informe no script);
   - `Kestrel:Endpoints:Https:Certificate:Subject` com o nome do certificado (ex.: `api.suaempresa.com.br`).

### 8.2 Instalar (primeira vez)

Em um PowerShell **como Administrador**, na pasta do projeto:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\scripts\instalar-servico-windows.ps1 -ConfigurarVariaveis
```

O script pede: servidor SQL, senha do `usr_sisgera_api`, URL pública, conta e senha de e-mail e a chave JWT (em branco gera uma nova). Depois publica, cria o serviço, grava as variáveis, configura o reinício automático, libera a porta 443 no firewall, inicia e mostra o resultado do `/health` e do `/health/pronto`.

### 8.3 Atualizar para uma nova versão

```powershell
git pull
.\scripts\instalar-servico-windows.ps1
```

Sem `-ConfigurarVariaveis`, as senhas e chaves já gravadas são mantidas. Se houver migration nova, aplique antes (seção 4).

### 8.4 Operação

| Ação | Comando |
|---|---|
| Ver estado | `Get-Service SigeraAssociadosApi` |
| Parar / iniciar / reiniciar | `Stop-Service` / `Start-Service` / `Restart-Service SigeraAssociadosApi` |
| Ver erros | Visualizador de Eventos > Logs do Windows > Aplicativo |
| Trocar uma senha | `.\scripts\instalar-servico-windows.ps1 -ConfigurarVariaveis` |
| Remover | `.\scripts\remover-servico-windows.ps1` |

### 8.5 Observações

- As variáveis ficam no registro do serviço (`HKLM\SYSTEM\CurrentControlSet\Services\SigeraAssociadosApi`, valor `Environment`), que só administradores leem.
- O serviço roda como `LocalSystem`, que consegue ler o certificado do computador local.
- Sem proxy na frente, o Kestrel recebe o IP real do cliente, e o limite de tentativas funciona por IP.
- O túnel do cloudflared é só para desenvolvimento. Em produção, o link dos e-mails usa `UrlPublica__BaseUrl`.
