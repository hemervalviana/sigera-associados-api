# SigeraAssociados API — Pendências gerais

Oct 24, 2026 · @Sr Hemerval Viana

## Resumo

O núcleo da API está pronto: consulta de associados nas 4 bases, autenticação JWT com Identity e controle de acesso por sistema, empresa, filial e carteira do corretor. O que falta é endurecer a segurança, completar a gestão de usuários, ampliar os testes e preparar o deploy.

Pronto hoje:

- Consulta de associados (listar, por código, paginada com filtros e ordenação) nas bases PROASP\_SIG, SONASP\_SIG, ABSP\_SIG e GSP\_SIG, escolhidas pelo cabeçalho `X-Sigera-Sistema`.
- Banco de identidade separado, com as migrations `InitialIdentity` e `UsuarioAcessos`.
- Login JWT de 15 minutos, cadastro, confirmação de e-mail e recuperação de senha via Gmail.
- Escopos de acesso (`UsuarioAcessos`) com cache de 5 minutos, aplicados em todas as consultas de associados.
- Endpoints administrativos: criar usuário, reenviar confirmação, conceder, listar e revogar acessos.
- Swagger UI em `/swagger` (só em Development), com login por token.
- Gestão de usuários: listar, consultar, trocar grupo, bloquear, desbloquear e excluir (`/api/administracao/usuarios`), e `GET /api/conta` com os acessos do usuário logado.
- Confirmação de e-mail por link, usando a URL pública do cloudflared tunnel (detalhes na seção abaixo).
- Testes unitários do filtro de escopo (12 casos) e testes de integração de validação de parâmetros.

Base do levantamento: leitura do código em `D:\SIGERA_2026\SIGERA_ASSOCIADOS_API`. Build e testes ainda não foram executados.

## Confirmação de e-mail por link (cloudflared tunnel)

Ao cadastrar um usuário, o e-mail agora traz um botão **Confirmar e-mail**. O link aponta para a URL pública criada pelo cloudflared, que repassa a requisição para a API local.

Como funciona:

1. O script `scripts\iniciar-tunel-cloudflared.ps1` abre um quick tunnel para `http://localhost:5092`, captura a URL `https://xxxx.trycloudflare.com` e grava em `src\SigeraAssociados.Api\urlpublica.local.json` (chave `UrlPublica:BaseUrl`). A API recarrega esse arquivo sozinha, sem reiniciar.
2. No cadastro (`POST /api/auth/registro`) e na criação pela administração (`POST /api/administracao/usuarios` e reenvio), o `EmailConfirmacaoService` gera o token do Identity, codifica em Base64Url e monta o link `{UrlPublica:BaseUrl}/api/auth/confirmar-email?usuarioId=...&token=...`.
3. O usuário clica no link; o novo endpoint `GET /api/auth/confirmar-email` confirma a conta e mostra uma página HTML de sucesso ou erro.
4. Ao fechar o script (Ctrl+C), o túnel é encerrado e o arquivo é apagado. Sem a chave, o e-mail volta a mandar só o token, como antes, e o `POST /api/auth/confirmar-email` continua valendo.

Como usar:

```powershell
winget install --id Cloudflare.cloudflared   # uma vez
dotnet run --project src\SigeraAssociados.Api   # janela 1
.\scripts\iniciar-tunel-cloudflared.ps1        # janela 2
```

Arquivos alterados: `Services\EmailConfirmacaoService.cs` (novo), `AuthController.cs` (GET de confirmação e uso do serviço no cadastro), `AdministracaoController.cs` (uso do serviço), `Program.cs` (registro do serviço e `UseForwardedHeaders` para o HTTPS do túnel), `appsettings.json` (chave `UrlPublica:BaseUrl`), `scripts\iniciar-tunel-cloudflared.ps1` (novo).

Pendências desta parte:

- [x] **Fluxo completo testado em 24/10/2026:** usuário criado, e-mail com botão recebido via túnel, link clicado e conta confirmada.
- [x] **URL nova sem reiniciar a API:** resolvido trocando os User Secrets pelo arquivo `urlpublica.local.json` com recarga automática.
- [ ] **URL fixa.** A URL do quick tunnel muda a cada execução, e e-mails enviados antes deixam de funcionar quando o túnel fecha. Para uso contínuo, criar um túnel nomeado (`cloudflared tunnel create`) com um domínio próprio e gravar essa URL na chave.
- [ ] **O túnel expõe a API inteira na internet**, não só a confirmação. Usar só em desenvolvimento até existir rate limiting (item abaixo) e, se possível, limitar as rotas expostas pelo túnel.
- [x] **Testes da confirmação por link:** `EmailConfirmacaoServiceTests` (montagem do link e token) e `AuthEndpointsTests` (link inválido).
- [x] **Link também na recuperação de senha** (ver item da prioridade média).

## Prioridade alta — segurança e correção

São pontos que podem expor dados ou quebrar a consulta em produção.

- [x] **Regra do associado (obrigatória):** só entra quem tem `T000PESS` + `T000CLIE` + `T000PFIS` (TP_PES = 'F') ou `T000PJUR` (TP_PES = 'J'). Aplicada nas três consultas de associados; teste em `CadastroAssociadoTests`; resposta traz `tipoPessoa`.
- [x] **Tipos das chaves de `T000PFIS` e `T000PJUR` conferidos:** `CD_EMP numeric(2,0)`, `SQ_PES_FIS`/`SQ_PES_JUR numeric(7,0)`, iguais ao mapeamento.
- [x] **Contagem conferida (24/10/2026):** API e SQL de referência retornaram 1321 associados válidos.
- [x] **Cadastro público removido.** `POST /api/auth/registro` foi retirado. Só Administrador, Administradores e Gestores criam usuários (`POST /api/administracao/usuarios`).
- [x] **Limite de tentativas (rate limiting):** login, confirmação e recuperação de senha com 10 por minuto por IP; criar usuário e reenviar confirmação com 5 a cada 10 minutos; geral com 300 por minuto por usuário. Acima disso a resposta é **429**. Código em `Api/Seguranca/LimiteRequisicoes.cs`.
- [x] **Compatibilidade do SQL Server:** as 4 bases estão em nível 100 (SQL Server 2008). A fábrica de contexto usa `UseCompatibilityLevel(100)` (chave `ConnectionSettings:CompatibilityLevel`), e o filtro de escopo vira `IN (...)` em vez de `OPENJSON`.
- [x] **Erro "Valor de parâmetro '201' está fora do intervalo" corrigido:** o filtro de escopo usava chaves calculadas (`CD_EMP * 100 + CD_FIL`) que estouravam `numeric(2,0)` com a compatibilidade 100. Agora cada acesso vira uma condição própria (`CD_EMP = x AND CD_FIL = y`, unidas por OR).
- [x] **Filtro com usuário restrito validado (24/10/2026):** pasta 08 do Postman toda aprovada; usuário restrito vê só empresa 2 + filial 1, recebe 403 em outro sistema e 404 em associado de outra filial.
- [x] **Filtro de escopo validado contra o SQL real** (pasta 08 do Postman, bases em compatibilidade 100).
- [x] **Usuário `sa` trocado por `usr_sisgera_api`** no `appsettings.json`. O script `scripts\criar-login-usr_sisgera_api.sql` cria o login com leitura nas 4 bases e leitura/escrita no banco de identidade.
- [x] **Login `usr_sisgera_api` em uso:** script executado, User Secrets atualizados e política de senha religada (`CHECK_POLICY = ON`) em 24/10/2026.
- [ ] **Certificado do SQL Server.** `TrustServerCertificate = true` está no appsettings base. Deixar só em Development e usar certificado válido nos demais ambientes.
- [x] **Concessão de acesso conferida no banco do sistema:** empresa precisa existir em `T009EMPS`; filial precisa ter associado ou contrato; corretor precisa existir em `T000PESS` e ter contrato em `T009CTRT` na empresa. Senão, **400** com a mensagem. Testes em `ConcessaoAcessoTests`; Postman 07.13b e 07.13c.
- [ ] **Tabela de filiais:** se existir uma tabela própria de filiais, trocar a conferência de filial (hoje por associados/contratos) para ela.
- [ ] **Escopo corretor + empresa inteira.** Hoje um acesso com empresa e sem corretor libera a empresa toda. Conferir na tela de administração que corretores nunca recebam esse tipo de acesso por engano.

## Prioridade média — funcionalidades faltando

A gestão de usuários está incompleta: hoje só é possível criar contas e conceder acessos.

- [x] **Listar e consultar usuários:** `GET /api/administracao/usuarios?busca=&grupo=&pagina=&tamanhoPagina=` e `GET /api/administracao/usuarios/{id}`, com grupos, confirmação de e-mail e bloqueio.
- [x] **Trocar grupo, bloquear/desbloquear e excluir:** `PUT .../{id}/grupo`, `POST .../{id}/bloquear`, `POST .../{id}/desbloquear`, `DELETE .../{id}`. Regras: ninguém altera a própria conta; Gestores não mexem em contas de administradores; o grupo Administradores não é concedido por esses endpoints.
- [ ] **Bloqueio e troca de grupo valem no próximo login.** O token JWT já emitido continua válido até expirar (15 min). Resolver junto com refresh token/revogação.
- [ ] **Gestores devem conceder acessos?** Hoje Gestores criam e gerenciam usuários, mas conceder e revogar acessos (`/acessos`) continua só para administradores.
- [ ] **Processo para conceder o grupo Administradores.** O código recusa esse grupo no cadastro e diz que "exige um processo separado", mas esse processo não existe.
- [x] **Swagger instalado** (Swashbuckle.AspNetCore 10.3.1) no lugar do Microsoft.AspNetCore.OpenApi. Em Development: tela em `/swagger`, botão **Authorize** para o token JWT, campo `X-Sigera-Sistema` nas rotas de associados e diagnóstico, e descrições vindas dos comentários `///`.
- [ ] **Refresh token e logout.** O token dura 15 minutos e não há renovação nem revogação. Troca de grupo só vale após o token expirar.
- [x] **Link de confirmação de e-mail.** Feito com o cloudflared tunnel (seção acima).
- [x] **Link de redefinição de senha:** o e-mail de recuperação traz o botão **Redefinir senha** (com o túnel); o link abre um formulário da própria API (`GET /api/auth/redefinir-senha`), que só aparece se o token for válido, e o envio (`POST /api/auth/redefinir-senha/formulario`) troca a senha e zera as tentativas erradas.
- [ ] **Auditoria persistida.** Concessão e revogação de acesso ficam só no log. Gravar quem fez, quando e o quê numa tabela.
- [x] **Endpoint "meus acessos":** `GET /api/conta` devolve o usuário, os grupos, se tem acesso irrestrito e as linhas de acesso.
- [ ] **Mais dados do associado.** O DTO traz só empresa, código, nome, situação, filial e data da carteira. Definir se a API vai expor contratos, dependentes ou outros dados.

## Qualidade — testes e verificação

Os testes cobrem bem o filtro de escopo e a validação de parâmetros; autenticação e administração estão sem testes.

- [x] **Build** compilando e API no ar com Swagger em `https://localhost:7001/swagger` (24/10/2026).
- [x] **`dotnet test`: 148 testes, 0 falhas** (24/10/2026).
- [ ] **Confirmar que a migration `UsuarioAcessos` foi aplicada** no banco de identidade.
- [ ] **Testes do `AuthController`:** login com e-mail não confirmado, conta bloqueada, token com as roles certas, recuperação sem revelar contas.
- [x] **Testes da administração:** Corretor recebe 403, grupo Administradores recusado; Gestor recebe 403 em acessos e diagnóstico.
- [x] **Testes da gestão de usuários:** `GestaoUsuariosServiceTests` (regras com Identity em memória) e `GestaoUsuariosEndpointsTests` (permissões por grupo e status HTTP).
- [x] **Testes de `GestaoAcessosService.Validar`:** `ValidacaoAcessoTests`.
- [ ] **Teste do middleware:** usuário sem acesso ao sistema recebe 403; administrador passa em qualquer sistema.
- [ ] **Teste de integração com SQL Server real** (Testcontainers ou base local de teste) para o filtro de escopo e a paginação.

## Arquitetura e organização do código

A separação em camadas existe, mas o projeto Domain está vazio e há código duplicado entre as camadas.

- [ ] **Projeto Domain vazio** (o `Class1.cs` já foi removido). As entidades estão em Infrastructure. Decidir se o Domain ganha modelos ou se é removido.
- [x] **Validação duplicada removida:** o `AssociadosController` não valida mais; a validação fica só no `AssociadosService`.
- [x] **Erros padronizados em ProblemDetails** (`application/problem+json`) com título em português, `codigo` e `traceId`; tratador global registra no log e responde 500 sem detalhes internos; 401/403/404 sem corpo também recebem ProblemDetails. Erros de entrada usam `DadosInvalidosException` (não mais `ArgumentException`). Testes em `RespostasErroTests`.
- [ ] **Registro de serviços espalhado.** `AssociadosService` e `SigeraSystemContext` são registrados no `Program.cs`. Criar um `AddApplication()` e mover o contexto para `AddSigeraInfrastructure`.
- [ ] **`SigeraDatabaseService` duplica a criação de contexto** do `AssociadosRepository`. Unificar.
- [x] **Grupos funcionais centralizados** em `Application/Usuarios/Grupos.cs`, usados por `SeedData`, `UsuarioAtual` e pelos `[Authorize]` dos controllers.
- [ ] **Função legada `Administrador`.** Mantida só por compatibilidade. Migrar tudo para `Administradores` e remover.
- [ ] **Busca por nome.** `Contains` vira `LIKE '%nome%'`, sem uso de índice. Avaliar busca por início do nome ou índice de texto completo se a base for grande.

## Limpeza de arquivos

Concluída em 24/10/2026.

- [x] `WeatherForecast.cs` e `WeatherForecastController.cs` removidos.
- [x] `SigeraAssociados.Api.http` reescrito com chamadas reais (login, conta, diagnóstico, associados, usuários e acessos).
- [x] `Class1.cs` do Domain removido (o projeto Domain ficou vazio; ver item de arquitetura).
- [x] `AssociadosController.cs.bak` removido.
- [x] Pasta `_backup_antes_permissoes` removida.
- [x] `DTOs/Auth/RegistroRequest.cs` removido.
- [x] `Criar o projeto.txt` movido para `doc/`.
- [x] `UnitTest1.cs` renomeados: `AssociadosServiceTests.cs` (unitários) e `ConsultaAssociadosValidacaoTests.cs` (integração).

## Infra, deploy e documentação

O projeto não tem Git, README nem configuração de produção.

- [x] **Git e GitHub (24/10/2026):** repositório privado https://github.com/hemervalviana/sigera-associados-api, branch `main`, com `.gitignore` e `.gitattributes`.
- [x] **README.md** na raiz: estrutura, configuração dos User Secrets, banco e migrations, execução, testes, sistemas e permissões, endpoints, túnel e links da documentação.
- [ ] **`appsettings.Production.json`** e definição de onde ficarão os segredos em produção.
- [ ] **Health checks** (`/health`) para o banco de identidade e as 4 bases.
- [ ] **CORS**, quando houver frontend.
- [ ] **E-mail em produção.** Gmail com senha de app serve para desenvolvimento; avaliar um provedor transacional.
- [ ] **Cache de acessos em mais de uma instância.** O cache é em memória; com duas instâncias, uma revogação só vale na instância que a recebeu (até 5 min nas outras). Aceitável com uma instância só.
- [ ] **Definir onde a API vai rodar** (IIS, serviço Windows, container) e o processo de publicação.

## Decisões em aberto

Perguntas que definem várias das pendências acima.

- [x] O cadastro público deve continuar existindo? **Não.** Só Administrador, Administradores e Gestores criam usuários.
- [ ] Os grupos (Gestores, Corretores, Financeiro, Associados, Atendimento, Coordenadora Adm. & Operacional) terão regras próprias? Hoje só o grupo de administradores muda algo; o resto vem das linhas de `UsuarioAcessos`.
- [ ] O grupo **Associados** é para o próprio associado ver os seus dados? Se for, falta ligar o usuário ao `SQ_PES_CLI`.
- [ ] Vai existir frontend? Isso decide CORS, links nos e-mails e refresh token.
- [ ] Quais dados além do cadastro básico a API precisa expor?
- [ ] Manter o projeto Domain ou simplificar para 3 camadas?
