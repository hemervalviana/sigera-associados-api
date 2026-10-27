# SigeraAssociados API

API REST em **C# / ASP.NET Core (.NET 10)** para consultar associados dos sistemas **PROASP, SONASP, ABSP e GSP**, com autenticação JWT, controle de acesso por sistema, filial e carteira do corretor, e gestão de usuários.

Arquitetura em camadas (Clean Architecture): Api → Application ← Infrastructure, com Entity Framework Core e SQL Server.

## Sumário

- [Estrutura](#estrutura)
- [Requisitos](#requisitos)
- [Configuração](#configuração)
- [Banco de dados](#banco-de-dados)
- [Executar](#executar)
- [Testes](#testes)
- [Regra do associado](#regra-do-associado)
- [Sistemas e permissões](#sistemas-e-permissões)
- [Respostas de erro](#respostas-de-erro)
- [Endpoints](#endpoints)
- [Confirmação de e-mail pelo túnel](#confirmação-de-e-mail-pelo-túnel)
- [Documentação](#documentação)

## Estrutura

```
SIGERA_ASSOCIADOS_API/
├── SigeraAssociados.slnx
├── src/
│   ├── SigeraAssociados.Api              Controllers, middleware, Swagger, rate limiting, e-mail
│   ├── SigeraAssociados.Application      DTOs, interfaces, regras de serviço, grupos
│   ├── SigeraAssociados.Domain           (reservado)
│   └── SigeraAssociados.Infrastructure   EF Core, Identity, repositórios, acessos, usuários
├── tests/
│   ├── SigeraAssociados.UnitTests        Regras de escopo, grupos, acessos, gestão de usuários
│   └── SigeraAssociados.IntegrationTests Endpoints, permissões por grupo, limites, confirmação
├── scripts/
│   ├── criar-login-usr_sisgera_api.sql   Login do SQL Server usado pela API
│   ├── criar-indice-nome-t000pess.sql    Índice de nome em T000PESS (busca por nome)
│   ├── iniciar-tunel-cloudflared.ps1     URL pública para o link de confirmação (dev)
│   ├── instalar-servico-windows.ps1      Publica e instala a API como serviço do Windows
│   └── remover-servico-windows.ps1       Remove o serviço do Windows
└── doc/                                   Pendências, fluxogramas, coleção do Postman
```

## Requisitos

- .NET SDK 10
- SQL Server com autenticação mista (SQL Server e Windows)
- Bancos de negócio: `PROASP_SIG`, `SONASP_SIG`, `ABSP_SIG`, `GSP_SIG`
- Banco de identidade: `SigeraIdentity`
- Opcional: `cloudflared` (link de confirmação por e-mail) e Postman

## Configuração

Nenhuma senha fica no repositório. O `appsettings.json` tem só valores públicos; os segredos ficam nos **User Secrets** do projeto Api.

```powershell
$p = "src\SigeraAssociados.Api"

# Bancos de negócio (usuário já vem do appsettings: usr_sisgera_api)
dotnet user-secrets set "ConnectionSettings:Password" "<senha>" --project $p

# Banco de identidade
dotnet user-secrets set "IdentityConnectionSettings:Server" "127.0.0.1" --project $p
dotnet user-secrets set "IdentityConnectionSettings:Port" "1433" --project $p
dotnet user-secrets set "IdentityConnectionSettings:Database" "SigeraIdentity" --project $p
dotnet user-secrets set "IdentityConnectionSettings:User" "usr_sisgera_api" --project $p
dotnet user-secrets set "IdentityConnectionSettings:Password" "<senha>" --project $p
dotnet user-secrets set "IdentityConnectionSettings:Encrypt" "true" --project $p
dotnet user-secrets set "IdentityConnectionSettings:TrustServerCertificate" "true" --project $p

# Chave JWT (64 bytes aleatórios em Base64)
$bytes = New-Object byte[] 64
[System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String($bytes)) --project $p

# E-mail (Gmail com senha de app)
dotnet user-secrets set "Email:Gmail:Host" "smtp.gmail.com" --project $p
dotnet user-secrets set "Email:Gmail:Port" "587" --project $p
dotnet user-secrets set "Email:Gmail:User" "<conta>@gmail.com" --project $p
dotnet user-secrets set "Email:Gmail:Password" "<senha de app>" --project $p

# Primeiro administrador (só em Development; desligue depois de criado)
dotnet user-secrets set "SeedAdmin:Habilitado" "true" --project $p
dotnet user-secrets set "SeedAdmin:Email" "<seu e-mail>" --project $p
dotnet user-secrets set "SeedAdmin:Senha" "<senha forte>" --project $p
```

| Chave | Onde | Descrição |
|---|---|---|
| `ConnectionSettings:*` | appsettings + secrets | Servidor e login das 4 bases de negócio |
| `IdentityConnectionSettings:*` | secrets | Banco de identidade (usuários, grupos, acessos) |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes` | appsettings | Emissor, público e validade do token (15 min) |
| `Jwt:SigningKey` | secrets | Chave de assinatura (mínimo 32 bytes, Base64) |
| `Email:Gmail:*` | secrets | SMTP para confirmação e recuperação de senha |
| `SeedAdmin:*` | secrets | Criação do primeiro administrador |
| `UrlPublica:BaseUrl` | `urlpublica.local.json` (gerado pelo script) | URL pública usada no link de confirmação |

## Banco de dados

1. Crie o login da API (execute como `sa`, depois de trocar a senha no script):

   ```powershell
   sqlcmd -S "127.0.0.1,1433" -U sa -C -i scripts\criar-login-usr_sisgera_api.sql
   ```

   Permissões: leitura nas 4 bases de negócio; leitura e escrita no `SigeraIdentity`.

2. Crie as tabelas do banco de identidade:

   ```powershell
   dotnet tool install --global dotnet-ef
   dotnet ef database update `
       --project src\SigeraAssociados.Infrastructure `
       --startup-project src\SigeraAssociados.Api `
       --context SigeraIdentityDbContext
   ```

As 4 bases de negócio são legadas e somente lidas: a API não cria nem altera tabelas nelas.

### Scripts SQL

Toda mudança de banco fica em um arquivo `.sql` na pasta `scripts`, pronto para executar e seguro para rodar mais de uma vez:

| Script | O que faz | Executar com |
|---|---|---|
| `criar-login-usr_sisgera_api.sql` | Cria o login da API (leitura nas 4 bases, escrita no SigeraIdentity) | `sa` |
| `criar-indice-nome-t000pess.sql` | Índice `IX_T000PESS_NM_PES` nas 4 bases, para a busca por nome | `sa` ou DBA |

```powershell
sqlcmd -S "127.0.0.1,1433" -U sa -C -i scripts\criar-indice-nome-t000pess.sql
```

## Executar

```powershell
dotnet restore .\SigeraAssociados.slnx
dotnet build .\SigeraAssociados.slnx
dotnet run --project src\SigeraAssociados.Api --launch-profile https
```

- Swagger (somente Development): https://localhost:7001/swagger
- Faça login em `POST /api/auth/login`, clique em **Authorize** e cole o `accessToken`.

## Produção

A API roda no Kestrel como **serviço do Windows** (`SigeraAssociadosApi`), com início automático e reinício em caso de falha. Instalação no servidor, como Administrador:

```powershell
.\scripts\instalar-servico-windows.ps1 -ConfigurarVariaveis
```

Passo a passo completo, variáveis de ambiente e checklist: [Implantação em produção](doc/Implanta%C3%A7%C3%A3o%20em%20produ%C3%A7%C3%A3o.md).

## Testes

```powershell
dotnet test .\SigeraAssociados.slnx
```

Os testes não precisam de banco: os unitários usam EF Core InMemory e os de integração usam autenticação simulada (`TesteAutenticacaoHandler`) e serviços falsos.

## Regra do associado

Todo associado é **pessoa física ou jurídica** e só é considerado pela API se tiver o cadastro completo:

| Tipo (`T000PESS.TP_PES`) | Tabelas obrigatórias |
|---|---|
| `F` (física, quase todos) | `T000PESS` + `T000CLIE` + `T000PFIS` |
| `J` (jurídica) | `T000PESS` + `T000CLIE` + `T000PJUR` |

Equivale à consulta:

```sql
select * from t000pess a
  inner join t000clie b on a.CD_EMP = b.CD_EMP and a.SQ_PES = b.SQ_PES_CLI
  inner join T000PFIS c on a.CD_EMP = c.CD_EMP and a.SQ_PES = c.SQ_PES_FIS
where a.TP_PES = 'F'
union all
select * from t000pess a
  inner join t000clie b on a.CD_EMP = b.CD_EMP and a.SQ_PES = b.SQ_PES_CLI
  inner join T000PJUR c on a.CD_EMP = c.CD_EMP and a.SQ_PES = c.SQ_PES_JUR
where a.TP_PES = 'J'
```

Na API a regra fica em `AssociadosRepository.FiltrarCadastroCompleto` (com `EXISTS`, para não duplicar linhas) e vale para listar, consultar por código e a busca paginada. A resposta traz `tipoPessoa` (`F` ou `J`).

## Sistemas e permissões

Cada requisição de negócio informa o sistema no cabeçalho `X-Sigera-Sistema`:

| Código | Sistema | Banco |
|---|---|---|
| 1 | PROASP | PROASP_SIG |
| 2 | SONASP | SONASP_SIG |
| 3 | ABSP | ABSP_SIG |
| 4 | GSP | GSP_SIG |

**Grupos:** Administradores, Gestores, Coordenadora Adm. & Operacional, Corretores, Financeiro, Associados e Atendimento.

| Quem | Pode |
|---|---|
| Administradores | Tudo: todos os dados, usuários, acessos e diagnóstico |
| Gestores | Criar e gerenciar usuários (exceto administradores) |
| Demais grupos | Consultar associados dentro dos acessos concedidos |

**Acessos** (tabela `UsuarioAcessos`, concedidos por administradores) se somam:

- todos os sistemas (`sistema: 0`, grava uma linha por sistema);
- sistema inteiro;
- filial do sistema (associados sem filial não aparecem);
- carteira do corretor (associados com contrato com o corretor), no sistema ou na filial.

Cada sistema é uma base com uma única empresa (CD_EMP), por isso a empresa não é informada na concessão: a API usa a empresa da base (`T009EMPS`).

Regras extras: ninguém altera a própria conta; o grupo Administradores não é concedido pelos endpoints; não há cadastro público.

**Limite de requisições (429):** autenticação 10/min por IP; criar usuário e reenviar confirmação 5 a cada 10 min; geral 300/min por usuário.

## Sessão: token de acesso e de renovação

- O login devolve `accessToken` (15 min) e `refreshToken` (7 dias, `Jwt:RefreshTokenDias`).
- `POST /api/auth/renovar` troca o `refreshToken` por um novo par de tokens, sem senha. O `refreshToken` é de uso único; reuso de um token já trocado encerra todas as sessões do usuário.
- O banco guarda só o hash SHA-256 do `refreshToken` (tabela `TokensRenovacao` no `SigeraIdentity`).
- A cada requisição, a API confere o carimbo de segurança gravado no token. Bloquear, trocar de grupo, redefinir a senha ou sair de todos os dispositivos troca o carimbo e o token antigo para de valer **na hora**.
- `POST /api/auth/logout` encerra a sessão atual na hora; com `"todosOsDispositivos": true`, encerra todas.

## Respostas de erro

Todo erro da API usa o mesmo formato, **ProblemDetails** (RFC 9457), com `Content-Type: application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Requisição inválida",
  "status": 400,
  "detail": "O tamanho da página deve estar entre 1 e 100.",
  "instance": "/api/associados/consulta",
  "codigo": "parametro_invalido",
  "traceId": "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01"
}
```

| Campo | Uso |
|---|---|
| `title` | Resumo em português do status (Requisição inválida, Não autenticado, Acesso negado...) |
| `detail` | Mensagem para o usuário |
| `codigo` | Identificador estável para o cliente tratar o erro (`parametro_invalido`, `validacao`, `sistema_invalido`, `sistema_sem_acesso`, `limite_excedido`, `erro_interno`...) |
| `traceId` | Localiza a requisição no log da API |
| `errors` | Só em `validacao`: campos inválidos e mensagens |
| extras | Ex.: `camposPermitidos` (ordenação), `tenteNovamenteEmSegundos` (429), `codigosErro` (cadastro) |

Falhas inesperadas viram **500** com `codigo: erro_interno`, sem detalhes internos, e ficam registradas no log com o `traceId`. Em Development a resposta também traz `excecao` e `mensagemExcecao`.

Implementação: `Api/Erros/RespostasErro.cs` (formato e títulos), `Api/Erros/TratadorExcecoes.cs` (exceções) e `Application/Erros/DadosInvalidosException.cs` (erros de entrada lançados pelos serviços).

## Endpoints

| Método | Rota | Quem |
|---|---|---|
| POST | `/api/auth/login` | Público |
| POST | `/api/auth/renovar` | Público (com refreshToken) |
| POST | `/api/auth/logout` | Usuário logado |
| POST / GET | `/api/auth/confirmar-email` | Público (GET = link do e-mail) |
| POST | `/api/auth/solicitar-recuperacao-senha` | Público |
| POST | `/api/auth/redefinir-senha` | Público |
| GET | `/api/auth/redefinir-senha` | Público (link do e-mail: abre o formulário de nova senha) |
| POST | `/api/auth/redefinir-senha/formulario` | Público (envio do formulário) |
| GET | `/api/conta` | Usuário logado |
| GET | `/api/associados` | Conforme acessos |
| GET | `/api/associados/consulta?cdFil=&nome=&cpf=&cdSitCli=` | Conforme acessos |
| GET | `/api/associados/{sqPesCli}` | Conforme acessos (cadastro completo) |
| POST | `/api/associados` | Conforme acessos (corretor: vinculado a ele) |
| PUT | `/api/associados/{sqPesCli}` | Conforme acessos (falecido: só Admin e Gestores) |
| DELETE | `/api/associados/{sqPesCli}` | Admin e Gestores (exclusão lógica) |
| GET | `/api/orgaos?busca=&cdFil=&uf=`, `/api/orgaos/{cdOrg}`, `/api/orgaos/grupos`, `/api/orgaos/suborgaos`, `/api/orgaos/lotacoes`, `/api/orgaos/cargos`, `/api/orgaos/situacoes` | Quem tem acesso ao sistema |
| POST / PUT / DELETE | `/api/orgaos`, `/api/orgaos/grupos`, `/api/orgaos/suborgaos`, `/api/orgaos/lotacoes`, `/api/orgaos/cargos`, `/api/orgaos/situacoes` (PUT e DELETE com `/{codigo}`) | Admin |
| GET / POST | `/api/associados/{sqPesCli}/contas` | Conforme acessos (contas bancárias do associado) |
| PUT / DELETE | `/api/associados/{sqPesCli}/contas/{sqConta}` | Conforme acessos; DELETE só Admin e Gestores |
| GET | `/api/cadastros/bancos?busca=` | Quem tem acesso ao sistema |
| POST / PUT / DELETE | `/api/cadastros/bancos` (PUT e DELETE com `/{cdBanco}`) | Admin |
| GET / POST | `/api/associados/{sqPesCli}/enderecos` | Conforme acessos (endereços do associado; um principal) |
| PUT / DELETE | `/api/associados/{sqPesCli}/enderecos/{sqEndereco}` | Conforme acessos; DELETE só Admin e Gestores |
| GET | `/api/cadastros/tipos-endereco` | Quem tem acesso ao sistema |
| POST / PUT / DELETE | `/api/cadastros/tipos-endereco` (PUT e DELETE com `/{codigo}`) | Admin |
| GET / POST | `/api/associados/{sqPesCli}/telefones` | Conforme acessos (telefones do associado) |
| PUT / DELETE | `/api/associados/{sqPesCli}/telefones/{sqTelefone}` | Conforme acessos; DELETE só Admin e Gestores |
| GET | `/api/cadastros/tipos-telefone` | Quem tem acesso ao sistema |
| POST / PUT / DELETE | `/api/cadastros/tipos-telefone` (PUT e DELETE com `/{codigo}`) | Admin |
| GET / POST | `/api/associados/{sqPesCli}/pix` | Conforme acessos (chaves Pix do associado) |
| PUT / DELETE | `/api/associados/{sqPesCli}/pix/{sqPix}` | Conforme acessos; DELETE só Admin e Gestores |
| GET / POST | `/api/associados/{sqPesCli}/referencias` | Conforme acessos (referências pessoais do associado) |
| PUT / DELETE | `/api/associados/{sqPesCli}/referencias/{sqReferencia}` | Conforme acessos; DELETE só Admin e Gestores |
| GET / POST | `/api/associados/{sqPesCli}/creditos` | Conforme acessos (créditos do associado; saldo calculado pela forma de pagamento) |
| PUT / DELETE | `/api/associados/{sqPesCli}/creditos/{sqCredito}` | Conforme acessos; DELETE só Admin e Gestores, e só com valor = saldo |
| GET | `/api/cadastros/formas-pagamento`, `/api/cadastros/produtos`, `/api/convenios`, `/api/convenios/{cdCvn}`, `/api/convenios/tipos-governo` | Quem tem acesso ao sistema |
| POST / PUT / DELETE | `/api/cadastros/formas-pagamento`, `/api/cadastros/produtos`, `/api/convenios` (PUT e DELETE com `/{codigo}` ou `/{cdCvn}`) | Admin |
| GET | `/api/corretores?busca=&cdFil=&tipoPessoa=&cdSituacao=`, `/api/corretores/{sqPesCorretor}` | Quem tem acesso ao sistema (senha do representante só para Admin e Gestores) |
| POST / PUT / DELETE | `/api/corretores` (PUT e DELETE com `/{sqPesCorretor}`) | Admin e Gestores (DELETE = exclusão lógica) |
| GET / POST | `/api/associados/{sqPesCli}/orgaos` | Conforme acessos (vínculos do associado com órgãos) |
| PUT / DELETE | `/api/associados/{sqPesCli}/orgaos/{sqOrg}` | Conforme acessos; DELETE só Admin e Gestores |
| GET | `/api/cadastros/filiais`, `/filiais/{cdFil}`, `/situacoes`, `/tratamentos`, `/municipios?uf=&busca=` | Quem tem acesso ao sistema (`X-Sigera-Sistema`) |
| POST / PUT / DELETE | `/api/cadastros/filiais`, `/situacoes`, `/tratamentos`, `/municipios` (PUT e DELETE com `/{codigo}`) | Admin |
| GET | `/api/administracao/grupos` | Admin, Gestores |
| POST | `/api/administracao/usuarios` | Admin, Gestores |
| GET | `/api/administracao/usuarios` | Admin, Gestores |
| GET / DELETE | `/api/administracao/usuarios/{id}` | Admin, Gestores |
| PUT | `/api/administracao/usuarios/{id}/grupo` | Admin, Gestores |
| POST | `/api/administracao/usuarios/{id}/grupos` | Admin, Gestores |
| DELETE | `/api/administracao/usuarios/{id}/grupos/{grupo}` | Admin, Gestores |
| POST | `/api/administracao/usuarios/{id}/bloquear` e `/desbloquear` | Admin, Gestores |
| POST | `/api/administracao/usuarios/{id}/reenviar-confirmacao` | Admin, Gestores |
| GET / POST | `/api/administracao/usuarios/{id}/acessos` | Admin |
| GET | `/api/administracao/sistemas/{sistema}/filiais` | Admin |
| GET | `/api/administracao/sistemas/{sistema}/corretores?cdFil=&busca=` | Admin |
| DELETE | `/api/administracao/usuarios/{id}/acessos/{acessoId}` | Admin |
| GET | `/api/diagnostico/conexao`, `/api/diagnostico/pessoas` | Admin |
| GET | `/health` | Público (API no ar) |
| GET | `/health/pronto` | Público (bancos acessíveis: 200 ou 503) |

## Confirmação de e-mail pelo túnel

Com a API rodando, em outra janela:

```powershell
winget install --id Cloudflare.cloudflared   # uma vez
.\scripts\iniciar-tunel-cloudflared.ps1
```

O script grava a URL `https://xxxx.trycloudflare.com` em `src\SigeraAssociados.Api\urlpublica.local.json`. A API recarrega o arquivo sozinha e os e-mails passam a trazer os botões **Confirmar e-mail** (cadastro) e **Redefinir senha** (recuperação). Ctrl+C fecha o túnel e apaga o arquivo. Sem o túnel, o e-mail traz o token para uso em `POST /api/auth/confirmar-email`.

O túnel expõe a API na internet: use só em desenvolvimento.

## Documentação

- [Pendências gerais](doc/SigeraAssociados%20API%20%E2%80%94%20Pend%C3%AAncias%20gerais.md)
- [Fluxogramas](doc/Fluxograma%20do%20projeto.md)
- [Implantação em produção](doc/Implanta%C3%A7%C3%A3o%20em%20produ%C3%A7%C3%A3o.md)
- [Postman: passo a passo](doc/postman/Postman%20-%20passo%20a%20passo.md)
- [Roteiro de testes](doc/postman/Roteiro%20de%20testes.md)
- Coleção do Postman: `doc/postman/SigeraAssociados API.postman_collection.json`
