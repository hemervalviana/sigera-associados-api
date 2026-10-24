# SigeraAssociados API — Roteiro de testes no Postman

Siga na ordem e marque cada item. Os números são os nomes das requisições na coleção (por exemplo, **05.2**). A coluna **Esperado** é o status que deve aparecer no canto direito da resposta. A aba **Test Results** também mostra *PASS* ou *FAIL*.

Antes de começar:

- [ ] API rodando (`dotnet run --project src\SigeraAssociados.Api --launch-profile https`)
- [ ] Coleção importada com a versão mais recente do arquivo `doc\postman\SigeraAssociados API.postman_collection.json`
- [ ] Variável `senha` preenchida em **Variables > Current value**
- [ ] **SSL certificate verification** desligado em Settings

## Parte 1 — Login e conta

| OK | Nº | O que testar | Preparação | Esperado |
|---|---|---|---|---|
| [ ] | 01.1 | Login com o seu usuário | — | **200** e "Token salvo" no Console |
| [ ] | 02.1 | Ver meus dados e acessos | — | **200**, `acessoIrrestrito: true` e grupo Administradores |

## Parte 2 — Conexão com os 4 bancos

| OK | Nº | O que testar | Preparação | Esperado |
|---|---|---|---|---|
| [ ] | 03.1 | Conexão PROASP_SIG | `sistema` = 1 | **200**, `banco: PROASP_SIG` |
| [ ] | 03.1 | Conexão SONASP_SIG | `sistema` = 2 | **200**, `banco: SONASP_SIG` |
| [ ] | 03.1 | Conexão ABSP_SIG | `sistema` = 3 | **200**, `banco: ABSP_SIG` |
| [ ] | 03.1 | Conexão GSP_SIG | `sistema` = 4 | **200**, `banco: GSP_SIG` |
| [ ] | 03.2 | Leitura da tabela T000PESS | `sistema` = 2 | **200**, até 10 pessoas |

## Parte 3 — Associados

| OK | Nº | O que testar | Preparação | Esperado |
|---|---|---|---|---|
| [ ] | 04.1 | Listar os 10 primeiros | `sistema` = 2 | **200**, `quantidade` até 10 |
| [ ] | 04.2 | Busca paginada padrão | — | **200**, com `totalRegistros` e `totalPaginas` |
| [ ] | 04.2 | Filtro por nome | Em **Params**, marque `nome` com um sobrenome real | **200**, todos os nomes contêm o trecho |
| [ ] | 04.2 | Ordem decrescente | `direcao` = desc | **200**, nomes de Z para A |
| [ ] | 04.2 | Segunda página | `pagina` = 2 | **200**, registros diferentes da página 1 |
| [ ] | 04.3 | Consultar por código | Ajuste `cdEmp` e `sqPesCli` com um associado do 04.1 | **200** |
| [ ] | 04.3 | Código que não existe | `sqPesCli` = 9999999 | **404** |

## Parte 4 — Gestão de usuários

| OK | Nº | O que testar | Preparação | Esperado |
|---|---|---|---|---|
| [ ] | 05.1 | Listar grupos | — | **200**, 7 grupos |
| [ ] | 05.2 | Criar usuário de teste | Ajuste `novoUsuarioEmail` com um e-mail seu que você consiga abrir | **201**, `usuarioId` salvo |
| [ ] | — | E-mail de confirmação chegou | Abra a caixa de entrada do e-mail de teste | E-mail "Confirme seu cadastro" |
| [ ] | 05.3 | Listar usuários | — | **200**, o usuário de teste aparece |
| [ ] | 05.3 | Filtro por grupo | Em **Params**, marque `grupo` = Corretores | **200**, só Corretores |
| [ ] | 05.4 | Consultar pelo ID | — | **200**, `emailConfirmado: false` |
| [ ] | 05.6 | Trocar grupo | Body `grupo` = Atendimento | **200**, `grupos: ["Atendimento"]` |
| [ ] | 05.7 | Bloquear | — | **200**, `bloqueado: true` |
| [ ] | 05.8 | Desbloquear | — | **200**, `bloqueado: false` |

## Parte 5 — Acessos do usuário de teste

| OK | Nº | O que testar | Preparação | Esperado |
|---|---|---|---|---|
| [ ] | 06.2 | Conceder empresa + filial | Ajuste `cdEmp`/`cdFil` no Body com valores reais | **201** |
| [ ] | 06.2 | Repetir o mesmo acesso | Envie de novo sem mudar | **409** (duplicado) |
| [ ] | 06.3 | Conceder carteira de corretor | `sqPesCorretor` real no Body | **201** |
| [ ] | 06.4 | Listar acessos | — | **200**, 2 acessos |
| [ ] | 06.5 | Revogar o último acesso | — | **200** |
| [ ] | 06.4 | Conferir a revogação | — | **200**, 1 acesso |

## Parte 6 — Confirmação de e-mail e login do usuário de teste

| OK | Nº | O que testar | Preparação | Esperado |
|---|---|---|---|---|
| [ ] | — | Abrir o túnel | Em outra janela: `.\scripts\iniciar-tunel-cloudflared.ps1` | URL `https://xxxx.trycloudflare.com` |
| [ ] | 05.5 | Reenviar confirmação | — | **200** |
| [ ] | — | Clicar no botão do e-mail | Abra o e-mail novo | Página "E-mail confirmado" |
| [ ] | 05.4 | Conferir confirmação | — | `emailConfirmado: true` |
| [ ] | 01.1 | Login como usuário de teste | Troque `email`/`senha` pelas variáveis do usuário de teste | **200** |
| [ ] | 02.1 | Acessos do usuário de teste | — | `acessoIrrestrito: false` e só o acesso da Parte 5 |
| [ ] | 04.1 | Associados do usuário de teste | `sistema` igual ao do acesso concedido | **200**, só associados da filial ou carteira liberada |
| [ ] | 04.1 | Sistema sem acesso | `sistema` diferente | **403** |
| [ ] | 05.3 | Corretor tenta listar usuários | — | **403** |
| [ ] | 01.1 | Voltar para o seu login | Restaure `email`/`senha` e rode o login | **200** |

## Parte 6b — Usuário restrito automatizado (pasta 08)

Confere que um usuário sem acesso irrestrito só vê a empresa e a filial liberadas. Também valida a correção do nível de compatibilidade 100: a consulta deve sair com `IN (...)`, sem `OPENJSON`.

Antes: preencha `restritoSenha` em **Variables > Current value**, e confira `restritoEmail` e `restritoUsuarioId`. Depois rode a pasta **08** inteira com **Run folder**.

| OK | Nº | O que testar | Esperado |
|---|---|---|---|
| [ ] | 08.1 | Login como administrador | **200** |
| [ ] | 08.2 | Remove acessos ao sistema inteiro do restrito | **200**; o Console mostra os acessos revogados |
| [ ] | 08.3 | Garante empresa 2 + filial 1 | **201** ou **409** |
| [ ] | 08.4 | Total como administrador | **200**; o Console mostra o total |
| [ ] | 08.5 | Login como restrito | **200** |
| [ ] | 08.6 | Minha conta do restrito | `acessoIrrestrito: false` e nenhum acesso ao sistema inteiro |
| [ ] | 08.7 | Associados do restrito | Total **menor** que o do administrador; todos da empresa 2; sem carteira, todos da filial 1 |
| [ ] | — | Console da API no 08.7 | Consulta com `IN (`, **sem** `OPENJSON` |
| [ ] | 08.8 | Restrito no sistema 1 (sem acesso) | **403** |
| [ ] | 08.9 | Restrito abrindo associado de outra filial | **404** |
| [ ] | 08.10 | Restrito listando usuários | **403** |

## Parte 7 — Testes de erro e segurança (pasta 07)

Rode a pasta inteira: passe o mouse em **07 - Testes de erro e segurança**, clique em `...` e depois em **Run folder**. Todas devem aparecer como *PASS*.

| OK | Nº | O que testar | Esperado |
|---|---|---|---|
| [ ] | 07.1 | Login com e-mail inexistente | **401** |
| [ ] | 07.2 | Associados sem token | **401** |
| [ ] | 07.3 | Associados sem `X-Sigera-Sistema` | **400** |
| [ ] | 07.4 | Associados com sistema 9 | **400** |
| [ ] | 07.5 | `tamanhoPagina` 500 | **400** |
| [ ] | 07.6 | Ordenação por campo inexistente | **400** |
| [ ] | 07.7 | Criar usuário no grupo Administradores | **400** |
| [ ] | 07.8 | Bloquear a própria conta | **403** |
| [ ] | 07.9 | Excluir a própria conta | **403** |
| [ ] | 07.10 | E-mail já cadastrado | **409** |
| [ ] | 07.11 | Grupo inexistente | **400** |
| [ ] | 07.12 | Filial sem empresa | **400** |
| [ ] | 07.13 | Acesso ao sistema 7 | **400** |
| [ ] | 07.14 | Link de confirmação adulterado | **400** |
| [ ] | 07.15 | Usuário inexistente | **404** |

## Parte 7b — Limite de tentativas (429)

| OK | Nº | O que testar | Como | Esperado |
|---|---|---|---|---|
| [ ] | 07.1 | Excesso de tentativas de login | Clique em **Send** no 07.1 onze vezes seguidas, em menos de 1 minuto | Da 1ª à 10ª: **401**. Da 11ª em diante: **429**, com `tenteNovamenteEmSegundos` |
| [ ] | 01.1 | Login liberado depois da espera | Aguarde o tempo indicado e rode o login | **200** |
| [ ] | 05.5 | Excesso de reenvios de e-mail | Envie o 05.5 seis vezes em menos de 10 minutos | Da 6ª em diante: **429** |

O 07.1 usa um e-mail que não existe, então não bloqueia a sua conta.

## Parte 8 — Gestor (opcional)

Confere as regras de quem é Gestor.

| OK | O que testar | Como | Esperado |
|---|---|---|---|
| [ ] | Criar um gestor | 05.2 com `grupo` = Gestores e outro e-mail; confirme o e-mail | **201** |
| [ ] | Login como gestor | 01.1 com o e-mail do gestor | **200** |
| [ ] | Gestor cria usuário | 05.2 com outro e-mail | **201** |
| [ ] | Gestor tenta alterar administrador | 05.7 com `usuarioId` = o seu ID de administrador | **403** |
| [ ] | Gestor tenta conceder acesso | 06.1 | **403** |
| [ ] | Gestor tenta diagnóstico | 03.1 | **403** |

## Parte 9 — Limpeza

| OK | Nº | O que fazer | Esperado |
|---|---|---|---|
| [ ] | 01.1 | Voltar para o seu login de administrador | **200** |
| [ ] | 05.9 | Excluir o usuário de teste (confira o `usuarioId`) | **200** |
| [ ] | 05.9 | Excluir o gestor de teste, se criou | **200** |
| [ ] | — | Fechar o túnel com Ctrl+C | Chave `UrlPublica:BaseUrl` removida |

## Anotações

Registre aqui o que falhou (número, status recebido e mensagem):

-
