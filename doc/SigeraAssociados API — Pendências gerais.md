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
- [x] **Certificado do SQL Server:** `TrustServerCertificate = false` em produção (`appsettings.Production.json`); orientação em `doc/Implantação em produção.md`.
- [x] **Concessão de acesso conferida no banco do sistema:** empresa precisa existir em `T009EMPS`; filial precisa ter associado ou contrato; corretor precisa existir em `T000PESS` e ter contrato em `T009CTRT` na empresa. Senão, **400** com a mensagem. Testes em `ConcessaoAcessoTests`; Postman 07.13b e 07.13c.
- [x] **Empresa fora da concessão de acesso e opção "todos os sistemas":** cada sistema tem uma única empresa, então `POST .../acessos` não recebe mais `cdEmp`. Com filial ou corretor, a API grava a empresa da base (lida em `T009EMPS`; se a tabela não tiver exatamente uma empresa, **400**). `sistema: 0` libera todos os sistemas inteiros, gravando uma linha por sistema que o usuário ainda não tem; não aceita filial nem corretor. A resposta traz `acessos` com as linhas gravadas. Sem mudança no banco: as linhas antigas com empresa continuam valendo.
- [x] **Pesquisa de filiais e corretores para a concessão de acesso:** `GET /api/administracao/sistemas/{sistema}/filiais` (T000PARA) e `GET /api/administracao/sistemas/{sistema}/corretores?cdFil=&busca=&pagina=&tamanhoPagina=` (T009CRRT + nome em T000PESS; com `cdFil`, só os corretores da filial; `busca` pelo começo do nome ou pelo código). Só administradores.
- [x] **Tabela de filiais:** a conferência de filial na concessão usa `T000PARA`, e a de corretor usa `T009CRRT` (com filial informada, o corretor tem de ser dessa filial).
- [x] **Tabelas de apoio do cadastro de associados:** `GET /api/cadastros/filiais` (T000PARA), `/situacoes` (T000SCLI), `/tratamentos` (T000TRTA) e `/municipios?uf=&busca=&pagina=&tamanhoPagina=` (T000MUNI; começo do nome ou código). Usam o cabeçalho `X-Sigera-Sistema` e valem para qualquer usuário com acesso ao sistema.
- [x] **CRUD de associados pessoa física (T000PESS + T000PFIS + T000CLIE):** `GET/PUT/DELETE /api/associados/{sqPesCli}` e `POST /api/associados`; a empresa vem do sistema (`cdEmp` saiu das rotas e da pesquisa, que ganhou `cdFil`). Inclusão numa transação com `SQ_PES = MAX + 1` (`UPDLOCK, HOLDLOCK`, até 3 tentativas). Obrigatórios: nome, CPF (único na base), nascimento, sexo, filial e situação; filial, situação, município, tratamento e corretor conferidos nas tabelas. Exclusão lógica em `T000CLIE.DT_EXC_API`/`NM_USU_EXC_API`, sem usar `CD_SIT_CLI`. Permissões: excluir só Administradores e Gestores; demais incluem e alteram dentro dos acessos; corretor só os da carteira (contrato) ou cadastrados por ele (`T000CLIE.SQ_PES_CRO_CAD`). Situação 10 (falecido) só Administradores e Gestores, porque a trigger `TR_CLIE_UPD` encerra os contratos ativos (2/5 → 7); a resposta informa quantos. `T000CLIE` marcada com `HasTrigger`. Testes: `ValidacaoCadastroAssociadoTests`, `EscopoEscritaTests` e permissões nos testes de integração.
- [x] **Manutenção das tabelas de apoio:** `POST`, `PUT /{codigo}` e `DELETE /{codigo}` em `/api/cadastros/filiais` (T000PARA, todos os campos; `GET /filiais/{cdFil}` traz o detalhe), `/situacoes` (T000SCLI), `/tratamentos` (T000TRTA) e `/municipios` (T000MUNI). Só administradores. Código omitido na inclusão = MAX + 1 (município exige o código). Exclusão física recusada (409) quando em uso: filial com associados, contratos, corretores ou acessos da API; situação com associados; tratamento ou município com pessoas/corretores. A situação 10 (falecido) nunca é excluída. Testes em `ValidacaoTabelasApoioTests` e permissões nos testes de integração.
- [x] **Órgãos e vínculos do associado:** `/api/orgaos` (T000ORGA, todos os campos, com endereço, contatos administrativo/financeiro e empresa processadora; pesquisa pelo começo do nome/sigla ou código, filial e UF), `/api/orgaos/lotacoes` (T000LOTA) e `/api/orgaos/situacoes` (T000SPOG): consultar para quem tem acesso ao sistema, gravar só administradores; exclusão física recusada (409) se houver vínculos. Vínculos em `/api/associados/{sqPesCli}/orgaos` (T000RPOR): listar com nome do órgão, lotação e situação; incluir (`sqOrg` = MAX + 1 da pessoa) e alterar para quem altera o associado; excluir só Administradores e Gestores; mesmo órgão + matrícula não repete (409).
- [x] **Gravação sem OUTPUT em todas as tabelas do legado** (`HasTrigger` em todas as entidades do `SigeraDbContext`): triggers desconhecidas não quebram a gravação.
- [x] **Cargos (T000CARG):** `/api/orgaos/cargos` (consultar para quem tem acesso ao sistema; gravar só administradores; exclusão recusada se houver vínculos). O `cdCargo` do vínculo passou a ser conferido em T000CARG e a lista de órgãos do associado traz a descrição do cargo.
- [x] **Grupos (T009GRUP) e subórgãos (T009SBOR):** `/api/orgaos/grupos` e `/api/orgaos/suborgaos` (consultar para quem tem acesso; gravar só administradores; exclusão recusada se houver órgãos). Subórgão tem `SQ_SBOR` IDENTITY: incluído por SQL direto com `SCOPE_IDENTITY()`. O órgão passou a conferir `cdGrupo` e `sqSubOrgao` nas tabelas.
- [x] **Situação do órgão (`T000ORGA.CD_SIT_ORG`):** valores fixos do legado, sem tabela: 1 = ATIVO, 0 = INATIVO (`SituacaoOrgao`). A API recusa outros valores (400), grava 1 quando a inclusão não informa, devolve a descrição (`situacao`) e a pesquisa aceita `cdSituacao`. Não usa a T000SPOG, que é só da situação do vínculo do associado.
- [x] **Contas bancárias do associado (T000CCOR):** `/api/associados/{sqPesCli}/contas` (listar com nome do banco; incluir e alterar para quem altera o associado; excluir só Administradores e Gestores). `sqConta` = MAX + 1 da pessoa (até 99). Obrigatórios: banco (conferido em T000BANC), agência e número; mesma conta (banco + agência + número) não repete (409). Bancos (T000BANC) em `/api/cadastros/bancos` (código obrigatório, não é gerado; gravar só administradores; exclusão recusada se houver contas).
- [ ] **Rodar `scripts\contas-bancarias-permissoes.sql`** nas 4 bases.
- [x] **Contas bancárias, valores fixos do legado (`RegrasContaBancaria`):** `cdTipoConta` (TP_CTA_COR) 0 = SEM CONTA, 1 = CONTA CORRENTE, 2 = POUPANÇA; `cdSituacao` (ID_CC_ATV) 1 = ATIVA, 2 = INATIVA (omitida na inclusão: 1); `enviarOptanteCef` (env_opt_cax) true/false gravado como 2 = SIM, 1 = NÃO. `identificador` = NR_IDT, `identificadorCef` = NR_IDT_02. Outros valores dão 400; a resposta traz `tipoConta` e `situacao` por extenso.
- [x] **Situação do banco (`T000BANC.CD_SIT_BCO`):** 1 = ATIVO, 0 = INATIVO (`SituacaoAtivoInativo.Banco`). Outros valores dão 400; omitida na inclusão grava 1; a resposta traz `situacao`.
- [x] **Endereços do associado (T000ENDE):** `/api/associados/{sqPesCli}/enderecos` (listar com tipo, município e UF, principal primeiro; incluir e alterar para quem altera o associado; excluir só Administradores e Gestores). `sqEndereco` = MAX + 1 da pessoa (até 99). Sempre um único principal: o primeiro endereço é principal; marcar outro desmarca os demais; ao excluir o principal, o de menor sequência assume. Tipos de endereço (T000TEND) em `/api/cadastros/tipos-endereco` (gravar só administradores; exclusão recusada se houver endereços). Município com endereços não pode ser excluído.
- [ ] **Rodar `scripts\enderecos-permissoes.sql`** nas 4 bases e conferir as consultas do fim: `ID_EN_PCP` deve usar S/N (a API grava assim) e não deve haver pessoa com mais de um endereço principal.
- [x] **Telefones do associado (T000TELE):** `/api/associados/{sqPesCli}/telefones` (listar com tipo e número formatado; incluir e alterar para quem altera o associado; excluir só Administradores e Gestores). `sqTelefone` = MAX + 1 da empresa (SQ_TELE é numeric(18,0), default -1). Obrigatórios: `ddd` (11 a 99) e `numero` (8 ou 9 números, gravado só com números); `cdTipo` opcional, conferido em T000TTEL. Mesmo DDD + número no associado não repete (409). Tipos de telefone (T000TTEL) em `/api/cadastros/tipos-telefone` (gravar só administradores; exclusão recusada se houver telefones).
- [ ] **Rodar `scripts\telefones-permissoes.sql`** nas 4 bases e conferir as consultas do fim: como o legado numera `SQ_TELE` (por empresa ou por pessoa), se há trigger em T000TELE (o default -1 pode indicar uma) e o formato gravado em `NR_FN`.
- [x] **Chaves Pix do associado (T009PIX):** `/api/associados/{sqPesCli}/pix` (listar com tipo e chave formatada; incluir e alterar para quem altera o associado; excluir só Administradores e Gestores). `sqPix` = MAX + 1 da pessoa (até 99). `cdTipo` (CD_PIX), valores fixos do legado, sem tabela (`TipoChavePix`): 1 = CPF/CNPJ, 2 = CELULAR, 3 = E-MAIL, 4 = ALEATÓRIA. A chave é validada pelo tipo (CPF/CNPJ com dígitos verificadores; celular DDD + 9 números; e-mail até 77; aleatória UUID) e gravada normalizada (só números; celular sem +55; e-mail e aleatória em minúsculas). Mesma chave no associado não repete (409).
- [ ] **Rodar `scripts\pix-permissoes.sql`** nas 4 bases e conferir as consultas do fim: só tipos 1 a 4 em `CD_PIX` e o formato que o legado grava em `VL_CHAVE`. Celular confirmado: DDD + número, sem o +55.
- [x] **Senhas do associado (T000CLIE.NR_PWS e NR_PWS_02):** no CRUD do associado como `associado.senhaPortal` (NR_PWS, senha do portal) e `associado.senhaAverbacao` (NR_PWS_02, senha de averbação), até 50 caracteres, gravadas em texto puro como no legado (sem aparar; só caracteres que o varchar guarda). Na alteração, omitida mantém a atual e `""` apaga. Voltam só no detalhe (`GET /api/associados/{sqPesCli}`) e nas respostas de inclusão e alteração; a pesquisa e os logs não trazem senhas.
- [x] **Referências do associado (T000REFE):** `/api/associados/{sqPesCli}/referencias` (listar com telefones formatados; incluir e alterar para quem altera o associado; excluir só Administradores e Gestores). `sqReferencia` = MAX + 1 da pessoa (até 9999). Obrigatórios: `nome` (até 50, gravado como digitado) e `telefone1`; `dataNascimento` (1900 até hoje) e `telefone2` opcionais. Telefones com DDD + número (10 ou 11 números), gravados só com números. Mesmo nome + telefone 1 no associado não repete (409).
- [ ] **Rodar `scripts\referencias-permissoes.sql`** nas 4 bases e conferir as consultas do fim (formato dos telefones gravados pelo legado e triggers).
- [x] **Créditos do associado (T009EXTRA):** `/api/associados/{sqPesCli}/creditos` (listar com descrições, `totalValor`, `totalSaldo` e `podeExcluir`; incluir e alterar para quem altera o associado; excluir só Administradores e Gestores). Regras do legado: `sqCredito` (SQ_PGT) = MAX + 1 do associado; saldo = valor quando a forma de pagamento gera saldo (T009FRPG.ID_GER_SLD = 1) ou é 0 (sem forma), senão 0; valor obrigatório e maior que zero (senão 400); o saldo é consumido por outras rotinas até zero; valor e forma de pagamento só podem ser alterados com o saldo intacto (saldo = valor, `saldoUtilizado: false`), e então o saldo é recalculado; com o saldo utilizado, 409; exclusão só com valor = saldo (409 "Valor do pagamento diferente do valor do saldo"). Omitidos na inclusão: data = hoje, `tipoBaixa` (ID_PGT_AUT) = 4. `tipoBaixa`: 1 = AUTOMÁTICA, 2 = MANUAL, 4 = CRÉDITO. Contrato conferido como sendo do associado (exige filial); averbação conferida em T009RBSR (exige filial e convênio). CD_USU (usuário do legado) fica vazio.
- [x] **Crédito: valor travado depois de usar o saldo (decidido).** O valor só pode ser alterado enquanto o saldo não foi utilizado; a forma de pagamento entra na mesma trava, porque trocá-la recalcula o saldo. Valor menor ou igual a zero é recusado.
- [x] **Formas de pagamento (T009FRPG), produtos (T009PRDT) e convênios (T009CONV):** `/api/cadastros/formas-pagamento` (com `geraSaldo`; código até 99, porque T009EXTRA.TP_PGT é numeric(2)), `/api/cadastros/produtos` e `/api/convenios` (pesquisa, detalhe e todos os campos; filial, banco e conta corrente T009CONT conferidos). Consultar: quem tem acesso ao sistema; gravar: só administradores; exclusão recusada (409) se houver créditos (e, no convênio, averbações).
- [ ] **Convênio: confirmar os valores de `CD_SIT_CON`** (a API aceita 0 a 9 sem descrição).
- [ ] **Rodar `scripts\creditos-permissoes.sql`** nas 4 bases e conferir as consultas do fim (formas que geram saldo, créditos com forma não cadastrada, valores de ID_PGT_AUT e CD_SIT_CON, triggers).
- [x] **Situação de lotação e cargo, valores fixos do legado:** lotação (`T000LOTA.CD_SIT_LOT`) 1 = ATIVO, **2** = INATIVO; cargo (`T000CARG.CD_SIT_CAG`) 1 = ATIVO, 0 = INATIVO (`SituacaoAtivoInativo`, que também atende o órgão). Outros valores dão 400; omitida na inclusão grava 1; a resposta traz `situacao` (ATIVO/INATIVO).
- [x] **Grupo de órgãos (T009GRUP), valores fixos do legado:** `tipo` (TP_GP) F = DIA FIXO, U = DIA ÚTIL; `ordem` (ID_ORD) A = ASCENDENTE, D = DESCENDENTE; `numeroDias` (NR_DD) 1 a 31 (`RegrasGrupoOrgao`). Outros valores dão 400; a resposta traz `descricaoTipo` e `descricaoOrdem`. Campos renomeados de `idOrdem`/`nrDd` para `ordem`/`numeroDias`.
- [ ] **Rodar `scripts\orgaos-permissoes.sql`** nas 4 bases (INSERT/UPDATE/DELETE em T000ORGA, T009GRUP, T009SBOR, T000LOTA, T000CARG, T000SPOG e T000RPOR) e conferir a consulta de triggers no fim do script.
- [ ] **Órgão: campos sem tabela conhecida.** `CD_SIT_ORG` do órgão (0 a 9) é gravado sem conferência (confirmar se usa a T000SPOG).
- [ ] **Rodar `scripts\tabelas-apoio-permissoes.sql`** nas 4 bases (INSERT/UPDATE/DELETE do `usr_sisgera_api` nas 4 tabelas de apoio) e conferir a consulta de triggers no fim do script.
- [ ] **Rodar `scripts\associados-colunas-api-e-permissoes.sql` nas 4 bases antes de publicar** esta versão (colunas novas em T000CLIE + INSERT/UPDATE do `usr_sisgera_api`). Sem as colunas, todas as consultas de associados falham.
- [ ] **Testar o CRUD contra o banco real** (Postman 04.4 a 04.6): inclusão, alteração, marcação de falecido num associado de teste e exclusão. Não há teste automatizado da gravação (SQL Server real).
- [ ] **Pessoa jurídica (T000PJUR)** fora do CRUD por decisão; associados PJ continuam só na pesquisa.
- [ ] **Associado excluído pela API continua aparecendo no sistema legado** (decidido assim). Rever se o legado também deve esconder.
- [ ] **Situação do corretor:** a pesquisa devolve `cdSituacao` (`T009CRRT.CD_SIT_CRRT`). Definir quais códigos são "ativo" para filtrar ou bloquear corretores inativos na concessão.
- [ ] **Escopo corretor + sistema inteiro.** Um acesso sem filial e sem corretor libera o sistema todo. Conferir na tela de administração que corretores nunca recebam esse tipo de acesso por engano.

## Prioridade média — funcionalidades faltando

- [x] **Filtro por CPF** em `GET /api/associados/consulta?cpf=` (busca exata em `T000PFIS.NR_CPF`, com validação dos dígitos; só pessoa física). Testes em `CpfTests`.
- [x] **CPF completo nas respostas** (`123.456.789-09`) na pesquisa e na consulta do associado, a pedido (antes vinha mascarado `***.456.789-**`). `DocumentoCpf.Mascarar` continua disponível se for preciso voltar.

A gestão de usuários está incompleta: hoje só é possível criar contas e conceder acessos.

- [x] **Listar e consultar usuários:** `GET /api/administracao/usuarios?busca=&grupo=&pagina=&tamanhoPagina=` e `GET /api/administracao/usuarios/{id}`, com grupos, confirmação de e-mail e bloqueio.
- [x] **Trocar grupo, bloquear/desbloquear e excluir:** `PUT .../{id}/grupo`, `POST .../{id}/bloquear`, `POST .../{id}/desbloquear`, `DELETE .../{id}`. Regras: ninguém altera a própria conta; Gestores não mexem em contas de administradores; o grupo Administradores não é concedido por esses endpoints.
- [x] **Incluir e remover de grupos:** `POST .../{id}/grupos` (body `{ "grupo": "Financeiro" }`) inclui em mais um grupo mantendo os atuais; `DELETE .../{id}/grupos/{grupo}` tira de um grupo mantendo os demais. Já pertence / não pertence → **409**; remover o único grupo → **400** (use o `PUT .../grupo`); Administradores não é concedido por aqui. Vale na hora, como a troca. Testes em `GestaoUsuariosServiceTests` e `GestaoUsuariosEndpointsTests`.
- [x] **Bloqueio e troca de grupo valem na hora** (carimbo de segurança no token).
- [ ] **Gestores devem conceder acessos?** Hoje Gestores criam e gerenciam usuários, mas conceder e revogar acessos (`/acessos`) continua só para administradores.
- [ ] **Processo para conceder o grupo Administradores.** O código recusa esse grupo no cadastro e diz que "exige um processo separado", mas esse processo não existe.
- [x] **Swagger instalado** (Swashbuckle.AspNetCore 10.3.1) no lugar do Microsoft.AspNetCore.OpenApi. Em Development: tela em `/swagger`, botão **Authorize** para o token JWT, campo `X-Sigera-Sistema` nas rotas de associados e diagnóstico, e descrições vindas dos comentários `///`.
- [x] **Refresh token e logout:** login devolve `refreshToken` (7 dias, uso único, hash no banco); `POST /api/auth/renovar` e `POST /api/auth/logout` (sessão atual ou todos os dispositivos). Carimbo de segurança conferido a cada requisição: bloqueio, troca de grupo e senha nova valem na hora.
- [x] **Migration `TokensRenovacao` aplicada** no `SigeraIdentity` (24/10/2026); 198 testes aprovados.
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

- [x] **Projeto Domain mantido** (decisão de 24/10/2026): fica reservado para regras e modelos de domínio quando surgirem; hoje as entidades legadas continuam em Infrastructure.
- [x] **Validação duplicada removida:** o `AssociadosController` não valida mais; a validação fica só no `AssociadosService`.
- [x] **Erros padronizados em ProblemDetails** (`application/problem+json`) com título em português, `codigo` e `traceId`; tratador global registra no log e responde 500 sem detalhes internos; 401/403/404 sem corpo também recebem ProblemDetails. Erros de entrada usam `DadosInvalidosException` (não mais `ArgumentException`). Testes em `RespostasErroTests`.
- [ ] **Registro de serviços espalhado.** `AssociadosService` e `SigeraSystemContext` são registrados no `Program.cs`. Criar um `AddApplication()` e mover o contexto para `AddSigeraInfrastructure`.
- [ ] **`SigeraDatabaseService` duplica a criação de contexto** do `AssociadosRepository`. Unificar.
- [x] **Grupos funcionais centralizados** em `Application/Usuarios/Grupos.cs`, usados por `SeedData`, `UsuarioAtual` e pelos `[Authorize]` dos controllers.
- [ ] **Função legada `Administrador`.** Mantida só por compatibilidade. Migrar tudo para `Administradores` e remover.
- [x] **Busca por nome pelo começo** (`NM_PES LIKE 'nome%'`): "MARIA" encontra "MARIA DA SILVA", não "ANA MARIA". Testes em `BuscaPorNomeTests`.
- [x] **Índice `IX_T000PESS_NM_PES` criado nas 4 bases** (24/10/2026) com `scripts\criar-indice-nome-t000pess.sql`.

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
- [x] **`appsettings.Production.json`** criado (sem senhas; servidor e URL a configurar). Segredos por variáveis de ambiente: ver `doc/Implantação em produção.md`.
- [x] **Health checks:** `GET /health` (API no ar) e `GET /health/pronto` (SigeraIdentity + 4 bases; 503 se algum falhar).
- [ ] **CORS**, quando houver frontend.
- [ ] **E-mail em produção.** Gmail com senha de app serve para desenvolvimento; avaliar um provedor transacional.
- [ ] **Cache de acessos em mais de uma instância.** O cache é em memória; com duas instâncias, uma revogação só vale na instância que a recebeu (até 5 min nas outras). Aceitável com uma instância só.
- [x] **Onde a API vai rodar:** Kestrel como serviço do Windows (decisão de 24/10/2026). Scripts `instalar-servico-windows.ps1` e `remover-servico-windows.ps1`; passo a passo em `doc/Implantação em produção.md`.
- [ ] **Instalar no servidor de produção:** certificado HTTPS, `appsettings.Production.json` (servidor SQL e nome do certificado) e `instalar-servico-windows.ps1 -ConfigurarVariaveis`.

## Decisões em aberto

Perguntas que definem várias das pendências acima.

- [x] O cadastro público deve continuar existindo? **Não.** Só Administrador, Administradores e Gestores criam usuários.
- [ ] Os grupos (Gestores, Corretores, Financeiro, Associados, Atendimento, Coordenadora Adm. & Operacional) terão regras próprias? Hoje só o grupo de administradores muda algo; o resto vem das linhas de `UsuarioAcessos`.
- [ ] O grupo **Associados** é para o próprio associado ver os seus dados? Se for, falta ligar o usuário ao `SQ_PES_CLI`.
- [ ] Vai existir frontend? Isso decide CORS, links nos e-mails e refresh token.
- [ ] Quais dados além do cadastro básico a API precisa expor?
- [x] Manter o projeto Domain ou simplificar para 3 camadas? **Manter.**
