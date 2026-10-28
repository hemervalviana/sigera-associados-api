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
| [ ] | 04.2 | Filtro por filial | Em **Params**, marque `cdFil` | **200**, só associados da filial |
| [ ] | 04.3 | Consultar cadastro completo | Ajuste `sqPesCli` com um associado do 04.1 | **200**, com `pessoaFisica` e `associado` |
| [ ] | 04.3 | Código que não existe | `sqPesCli` = 9999999 | **404** |
| [ ] | — | Script do CRUD rodado | `scripts\associados-colunas-api-e-permissoes.sql` nas 4 bases | Colunas e permissões criadas |
| [ ] | — | Script das tabelas de apoio | `scripts\tabelas-apoio-permissoes.sql` nas 4 bases | Permissões concedidas; consulta de triggers vazia |
| [ ] | 04.24 | Incluir tratamento de teste | Body `descricao` = TESTE API | **201**, `item.codigo` salvo |
| [ ] | 04.25 | Alterar o tratamento | Body `descricao` = TESTE API 2 | **200** |
| [ ] | 04.26 | Excluir o tratamento | — | **200** |
| [ ] | 04.26 | Excluir tratamento em uso | `codigoApoio` de um tratamento usado por alguém | **409** |
| [ ] | 04.27 | Excluir a situação 10 | — | **409** (falecido é protegido) |
| [ ] | — | Script de contas bancárias | `scripts\contas-bancarias-permissoes.sql` nas 4 bases | Permissões concedidas |
| [ ] | 11.2 | Incluir conta no associado de teste | `cdBanco` do 11.5 | **201**, `sqConta` salvo |
| [ ] | 11.2 | Repetir a mesma conta | Envie de novo | **409** |
| [ ] | 11.3 | Alterar a conta | — | **200** |
| [ ] | 11.4 | Excluir a conta | Como administrador | **200** |
| [ ] | — | Script de endereços | `scripts\enderecos-permissoes.sql` nas 4 bases; conferir que `ID_EN_PCP` usa S/N | Permissões concedidas |
| [ ] | 10.2 | Incluir endereço no associado de teste | `cdTipo` do 10.6, `cdMunicipio` do 04.23 | **201**, `principal: true` (primeiro endereço) |
| [ ] | 10.2 | Incluir segundo endereço com `principal: true` | — | **201**; no 10.1, o primeiro deixa de ser principal |
| [ ] | 10.3 | Alterar endereço | — | **200** |
| [ ] | 10.4 | Excluir o principal | Como administrador | **200**; o outro vira principal |
| [ ] | — | Script de telefones | `scripts\telefones-permissoes.sql` nas 4 bases; conferir as consultas do fim | Permissões concedidas |
| [ ] | 12.2 | Incluir telefone no associado de teste | `cdTipo` do 12.6 | **201**, `sqTelefone` salvo |
| [ ] | 12.2 | Repetir o mesmo telefone | Envie de novo | **409** |
| [ ] | 12.3 | Alterar o telefone | — | **200**, `formatado` = (88) 3521-1234 |
| [ ] | 12.4 | Excluir como Corretor ou Atendimento | — | **403** |
| [ ] | 12.4 | Excluir o telefone | Como administrador | **200** |
| [ ] | — | Script de chaves Pix | `scripts\pix-permissoes.sql` nas 4 bases; conferir as consultas do fim | Permissões concedidas |
| [ ] | 13.2 | Incluir chave Pix (celular) no associado de teste | — | **201**, `sqPix` salvo, `chave` = 88999991234 |
| [ ] | 13.2 | Repetir a mesma chave | Envie de novo | **409** |
| [ ] | 13.2 | Chave inválida | `cdTipo` 1 com CPF errado | **400** |
| [ ] | 13.3 | Alterar para e-mail | — | **200**, em minúsculas |
| [ ] | 13.4 | Excluir como Corretor ou Atendimento | — | **403** |
| [ ] | 13.4 | Excluir a chave | Como administrador | **200** |
| [ ] | — | Script de referências | `scripts\referencias-permissoes.sql` nas 4 bases; conferir as consultas do fim | Permissões concedidas |
| [ ] | 14.2 | Incluir referência no associado de teste | — | **201**, `sqReferencia` salvo, `telefone1` = 88999991234 |
| [ ] | 14.2 | Repetir a mesma referência | Envie de novo | **409** |
| [ ] | 14.2 | Sem telefone 1 | Tire o `telefone1` do Body | **400** |
| [ ] | 14.3 | Alterar a referência | — | **200**, `telefone2` vazio |
| [ ] | 14.4 | Excluir como Corretor ou Atendimento | — | **403** |
| [ ] | 14.4 | Excluir a referência | Como administrador | **200** |
| [ ] | — | Script de créditos | `scripts\creditos-permissoes.sql` nas 4 bases; conferir as consultas do fim | Permissões concedidas |
| [ ] | 15.6 | Formas de pagamento | — | **200**; anote uma com `geraSaldo: true` e outra com `false` |
| [ ] | 15.2 | Incluir crédito com forma que gera saldo | `cdFormaPagamento` da forma com `geraSaldo: true` | **201**, `saldo` = `valor`, `podeExcluir: true` |
| [ ] | 15.2 | Incluir crédito com forma que não gera saldo | `cdFormaPagamento` da forma com `geraSaldo: false` | **201**, `saldo` = 0, `podeExcluir: false` |
| [ ] | 15.2 | Valor zero ou negativo | `"valor": 0` e depois `"valor": -10` | **400** "maior que zero" |
| [ ] | 15.2 | Sem valor | Tire o `valor` do Body | **400** "obrigatório" |
| [ ] | 15.3 | Alterar só a observação | — | **200**, saldo igual ao de antes |
| [ ] | 15.3 | Alterar o valor com saldo intacto | Novo valor | **200**, saldo = novo valor |
| [ ] | 15.3 | Alterar o valor com saldo já utilizado | Crédito com `saldoUtilizado: true` | **409** "saldo deste crédito já foi utilizado" |
| [ ] | 15.3 | Alterar só a observação com saldo utilizado | Mesmo valor e forma | **200** |
| [ ] | 15.4 | Excluir crédito com saldo 0 (forma que não gera) | Como administrador | **409** "Valor do pagamento diferente do valor do saldo" |
| [ ] | 15.4 | Excluir crédito com saldo = valor | Como administrador | **200** |
| [ ] | 15.4 | Excluir como Corretor, Atendimento ou Financeiro | — | **403** |
| [ ] | 15.7 / 15.8 | Produtos e convênios | — | **200** |
| [ ] | 15.9 | Incluir convênio como não administrador | Gestor | **403** |
| [ ] | 15.9 | Convênio com banco e governo | `"cdBanco": 1` junto com `"tipoGoverno": 3` | **400** "não os dois" |
| [ ] | — | Script de corretores | `scripts\corretores-colunas-api-e-permissoes.sql` nas 4 bases, antes de subir a API | Colunas e permissões criadas |
| [ ] | 16.1 | Pesquisar corretores | `busca` = começo de um nome | **200**, só corretores coerentes (F com PFIS, J com PJUR) |
| [ ] | 16.3 | Incluir corretor pessoa física | Troque o CPF por um válido que não exista | **201**, `sqPesCorretor` salvo |
| [ ] | 16.3 | Mesmo CPF de novo | Envie de novo | **409** "Já existe um corretor" |
| [ ] | 16.3 | Associado que vira corretor | CPF do associado de teste (04.4) | **201**, mensagem "aproveitando a pessoa já existente"; `sqPesCorretor` = `sqPesCli`; nome e nascimento do associado mantidos |
| [ ] | 04.4 | Corretor que vira associado | CPF do corretor pessoa física (16.3) que ainda não é associado | **201**, mensagem "aproveitando a pessoa já existente"; nome do corretor mantido |
| [ ] | 16.3 | Física com bloco de jurídica | Acrescente `pessoaJuridica` | **400** |
| [ ] | 16.4 | Incluir corretor pessoa jurídica | Troque o CNPJ por um válido que não exista | **201** |
| [ ] | 16.7 | Cargos do corretor | — | **200**, mesma lista de `/api/orgaos/cargos` |
| [ ] | 16.4 | Jurídica com cargo | `pessoaJuridica.cdCargo` de um cargo do 16.7 | **201**, `pessoaJuridica.cargo` com a descrição |
| [ ] | 16.4 | Jurídica com cargo inexistente | `"cdCargo": 9999` | **400** "não está cadastrado (T000CARG)" |
| [ ] | 16.8 | Excluir cargo usado por corretor | Cargo usado no teste acima | **409** "há corretores pessoa jurídica" |
| [ ] | 16.9 | Incluir agente | — | **201**, `sqAgente` salvo |
| [ ] | 16.10 | Incluir gerente de vendas | — | **201**, `sqGerenteVendas` salvo |
| [ ] | 16.5 | Corretor com agente e gerente | `sqAgente` e `sqGerenteVendas` acima | **200**, `agente` e `gerenteVendas` com os nomes |
| [ ] | 16.5 | Representante pessoa física | `sqPesRepresentante` de um corretor F | **400** "precisa ser um corretor pessoa jurídica" |
| [ ] | 16.5 | Representante de outra filial | Corretor J de outra filial | **400** |
| [ ] | 16.5 | Representante sem filial | Tire `cdFil` | **400** |
| [ ] | 16.5 | Situação ou tipo fora da lista | `"cdSituacao": 3` ou `"tipo": 3` | **400** |
| [ ] | 16.11 | Pesquisa da parametrização | `tipo`, `sqAgente` e `sqGerenteVendas` do 16.9/16.10 | **200**, só corretores com esses valores; linhas com `agente` e `gerenteVendas` |
| [ ] | 16.11 | Pesquisa com TODOS | `tipo=0` e `cdSituacao=0` | **200**, mesmo total de quando os filtros são desmarcados |
| [ ] | 16.11 | Classificação fora da lista | `tipo=3` | **400** |
| [ ] | 16.12 | Parametrizar corretor | `sqPesCorretor` do 16.3, agente e gerente do 16.9/16.10 | **200**, linha com classificação, status e nomes novos; 16.2 mostra o resto do cadastro igual |
| [ ] | 16.12 | Parametrizar limpando agente e gerente | `"sqAgente": null, "sqGerenteVendas": null` | **200**, `agente` e `gerenteVendas` nulos |
| [ ] | 16.12 | Parametrizar com agente inexistente | `"sqAgente": 99999999` | **400** "não está cadastrado" |
| [ ] | 16.12 | Parametrizar sem tipo ou com status 3 | Tire `tipo` ou `"cdSituacao": 3` | **400** |
| [ ] | 16.12 | Parametrizar como Corretor ou Atendimento | Outro usuário | **403** |
| [ ] | 16.9 | Excluir agente usado por corretor | — | **409** |
| [ ] | 17.1 | Endereços do corretor de teste | `sqPesCorretor` do 16.3 | **200** |
| [ ] | 17.2 | Incluir endereço no corretor | Como administrador | **201**, principal |
| [ ] | 17.3 | Incluir telefone no corretor | — | **201** |
| [ ] | 17.4 | Incluir conta no corretor | `cdBanco` do 11.5 | **201** |
| [ ] | 17.5 | Incluir referência no corretor | — | **201** |
| [ ] | 17.6 | Incluir chave Pix no corretor | — | **201** |
| [ ] | 17.2 | Incluir como Atendimento | Outro usuário | **403** |
| [ ] | 17.1 | Corretor excluído | Depois do 16.6 | **404** "Corretor não encontrado" |
| [ ] | — | Script de contratos | `scripts\contratos-colunas-api-e-permissoes.sql` nas 4 bases, antes de subir a API | Colunas e permissões criadas |
| [ ] | 18.1 | Contratos do associado de teste | `sqPesCli` do 04 | **200** |
| [ ] | 18.4 | Incluir contrato | Códigos reais de produto, convênio, forma, situação (18.2) e categoria | **201**, 12 parcelas de 280,50 (10/12/2026 a 10/11/2027); `cdFilContrato` e `sqCtr` salvos |
| [ ] | 18.4 | 1ª parcela até a data do contrato | `"dataPrimeiraParcela"` = hoje | **400** |
| [ ] | 18.4 | Situação quitado sem quitação | `"cdSituacao": 9` sem `dataQuitacao` | **400** |
| [ ] | 18.4 | Vínculo de órgão de outro associado | `sqOrgao` inexistente para o associado | **400** "não é deste associado" |
| [ ] | 18.4 | Sem mensalidade nem índice | Tire `valorMensalidade` e informe `cdCarencia` sem índice para 12 parcelas | **400** "Não há índice cadastrado" |
| [ ] | 18.6 | Alterar contrato | — | **200**, mensalidade 300; parcelas continuam 280,50 |
| [ ] | 18.6 | Alterar trocando o associado ou o produto | Outro `sqPesCli` ou `cdProduto` | **400** |
| [ ] | 18.6 | Situação 4 (encerrado) | `"cdSituacao": 4` | **200**, `dataEncerramento` = hoje; voltando para 2, fica nula |
| [ ] | 18.8 | Incluir parcela | — | **201**, número 13; 18.5 mostra `quantidadeParcelas` 13 |
| [ ] | 18.9 | Alterar parcela | — | **200**, saldo -290,00 |
| [ ] | 18.9 | Alterar parcela paga | Uma parcela com `baixada: true` | **409** |
| [ ] | 18.10 | Excluir parcela como Corretor ou Atendimento | Outro usuário | **403** |
| [ ] | 18.10 | Excluir parcela | Como administrador | **200**; `quantidadeParcelas` volta a 12 |
| [ ] | 18.x | Corretor vendo contrato de outro corretor | Usuário com acesso só à carteira de outro corretor | **404** no 18.5; não aparece no 18.1 |
| [ ] | 18.11 | Excluir contrato | Como administrador | **200**; 18.5 dá **404**; no banco, `DT_EXC_API` preenchida e parcelas mantidas |
| [ ] | 04.x | Associado que também é corretor | Listar endereços pelo associado e pelo corretor | Mesma lista nas duas rotas |
| [ ] | 16.2 | Detalhe como administrador | — | **200**, com `senhaRepresentante` |
| [ ] | 16.2 | Detalhe como Atendimento | Outro usuário | **200**, `senhaRepresentante` nula |
| [ ] | 16.5 | Alterar trocando o tipo para J | `"tipoPessoa": "J"` | **400** |
| [ ] | 16.5 | Alterar sem a senha | Tire `senhaRepresentante` | **200**; senha mantida no 16.2 |
| [ ] | 16.6 | Excluir como Corretor ou Atendimento | — | **403** |
| [ ] | 16.6 | Excluir como administrador | — | **200**; 16.2 dá **404**; no banco, `DT_EXC_API` preenchida |
| [ ] | 15.9 | Convênio sem banco nem governo | Tire o `tipoGoverno` | **201** (os dois vazios são aceitos) |
| [ ] | — | Script de órgãos | `scripts\orgaos-permissoes.sql` nas 4 bases | Permissões concedidas |
| [ ] | 09.1 | Pesquisar órgãos | — | **200**, lista paginada |
| [ ] | 09.2 | Incluir órgão de teste | — | **201**, `cdOrg` salvo |
| [ ] | 09.3 | Detalhe do órgão | — | **200**, com endereço e contatos |
| [ ] | 09.6 | Incluir o associado de teste no órgão | `sqPesCli` do 04.4 | **201**, `sqOrg` salvo |
| [ ] | 09.6 | Repetir com a mesma matrícula | Envie de novo | **409** |
| [ ] | 09.5 | Listar órgãos do associado | — | **200**, com nome do órgão |
| [ ] | 09.8 | Excluir o vínculo | Como administrador | **200** |
| [ ] | 09.4 | Excluir o órgão de teste | Depois de excluir o vínculo | **200** (com vínculo: **409**) |
| [ ] | 04.4 | Incluir associado de teste | Troque o CPF no Body por um CPF válido que não exista na base | **201**, `sqPesCli` salvo |
| [ ] | 04.4 | Incluir de novo o mesmo CPF | Envie de novo | **409** |
| [ ] | 04.3 | Conferir a inclusão | — | **200**, dados iguais aos enviados |
| [ ] | 04.5 | Alterar o associado de teste | Mude o nome no Body | **200**, nome novo |
| [ ] | 04.5 | Alterar sem mandar as senhas | Tire `senhaPortal` e `senhaAverbacao` do Body | **200**; no 04.3, as senhas continuam as mesmas |
| [ ] | 04.5 | Apagar a senha de averbação | `"senhaAverbacao": ""` | **200**; no 04.3, `senhaAverbacao` = null |
| [ ] | 04.3 | Senhas no detalhe | — | **200**, `associado.senhaPortal` e `senhaAverbacao` em texto puro |
| [ ] | 04.x | Pesquisa não traz senha | `GET /api/associados/consulta` | **200**, sem `senha` em nenhum item |
| [ ] | 04.5 | Corretor marcando falecido | Com um usuário Corretor, `cdSitCli` = 10 | **403** |
| [ ] | 04.6 | Excluir como Corretor ou Atendimento | — | **403** |
| [ ] | 04.6 | Excluir como administrador | — | **200** |
| [ ] | 04.3 | Conferir a exclusão | — | **404**; no banco, `DT_EXC_API` preenchida |

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
| [ ] | 05.10 | Incluir em mais um grupo | Body `grupo` = Financeiro | **200**, `grupos: ["Atendimento", "Financeiro"]` |
| [ ] | 05.10 | Incluir de novo no mesmo grupo | Envie de novo sem mudar | **409** |
| [ ] | 05.11 | Remover de um grupo | Variável `grupoRemover` = Financeiro | **200**, `grupos: ["Atendimento"]` |
| [ ] | 05.11 | Remover o único grupo | Variável `grupoRemover` = Atendimento | **400** |
| [ ] | 05.7 | Bloquear | — | **200**, `bloqueado: true` |
| [ ] | 05.8 | Desbloquear | — | **200**, `bloqueado: false` |

## Parte 5 — Acessos do usuário de teste

| OK | Nº | O que testar | Preparação | Esperado |
|---|---|---|---|---|
| [ ] | 06.7 | Listar filiais do sistema | — | **200**, lista de `cdFil` e nomes |
| [ ] | 06.8 | Listar corretores da filial | Ajuste `cdFil` com uma filial do 06.7 | **200**, só corretores da filial |
| [ ] | 06.2 | Conceder filial | `cdFil` de uma filial do 06.7 | **201** |
| [ ] | 06.2 | Repetir o mesmo acesso | Envie de novo sem mudar | **409** (duplicado) |
| [ ] | 06.3 | Conceder carteira de corretor | `sqPesCorretor` de um corretor do 06.8 | **201** |
| [ ] | 06.4 | Listar acessos | — | **200**, 2 acessos |
| [ ] | 06.5 | Revogar o último acesso | — | **200** |
| [ ] | 06.4 | Conferir a revogação | — | **200**, 1 acesso |
| [ ] | 06.6 | Conceder todos os sistemas | — | **201**, `acessos` com os sistemas que faltavam |
| [ ] | 06.6 | Repetir | Envie de novo | **409** |

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

## Parte 6a — Recuperação de senha pelo link

Use o usuário de teste. O túnel precisa estar aberto (`.\scripts\iniciar-tunel-cloudflared.ps1`).

| OK | Nº | O que testar | Como | Esperado |
|---|---|---|---|---|
| [ ] | 01.3 | Pedir recuperação | Body com o e-mail do usuário de teste | **202** |
| [ ] | — | E-mail recebido | Abra a caixa de entrada | Botão **Redefinir senha** |
| [ ] | — | Abrir o link | Clique no botão | Página com o formulário "Redefinir senha" |
| [ ] | — | Senhas diferentes | Digite duas senhas diferentes e salve | Formulário de novo, com "A senha e a confirmação não coincidem." |
| [ ] | — | Senha fraca | Ex.: `senhafraca12345` (sem maiúscula e símbolo) | Formulário de novo, com o que falta na senha |
| [ ] | — | Senha válida | Senha forte nas duas caixas | Página "Senha redefinida" |
| [ ] | — | Reusar o link | Clique de novo no mesmo botão do e-mail | "Link inválido ou expirado" |
| [ ] | 01.1 | Login com a senha nova | Atualize a senha do usuário de teste nas variáveis | **200** |

## Parte 6c — Sessão: renovar token e logout

| OK | Nº | O que testar | Como | Esperado |
|---|---|---|---|---|
| [ ] | 01.1 | Login | — | **200** com `accessToken` e `refreshToken` |
| [ ] | 01.5 | Renovar sem senha | — | **200**, tokens novos guardados |
| [ ] | 01.5 | Reusar um refreshToken antigo | Copie o `refreshToken` antes do 01.5, rode o 01.5 e depois cole o antigo de volta na variável e rode de novo | **401**; todas as sessões encerradas (02.1 também dá **401**) |
| [ ] | 01.1 | Login de novo | — | **200** |
| [ ] | 01.6 | Logout desta sessão | — | **200** |
| [ ] | 02.1 | Usar a API depois do logout | Cole em `token` o token antigo, se quiser testar | **401** na hora (sem esperar 15 min) |
| [ ] | 05.7 | Bloqueio vale na hora | Logado como restrito em outra aba, bloqueie-o como admin e chame o 08.6 | **401** imediato para o restrito |
| [ ] | 05.6 | Troca de grupo sem novo login | Troque o grupo do restrito; ele roda o 01.5 (renovar) | Token novo já com o grupo novo |

## Parte 6b — Usuário restrito automatizado (pasta 08)

Confere que um usuário sem acesso irrestrito só vê a empresa e a filial liberadas. Também valida a correção do nível de compatibilidade 100: a consulta deve sair com `IN (...)`, sem `OPENJSON`.

Antes: preencha `restritoSenha` em **Variables > Current value**, e confira `restritoEmail` e `restritoUsuarioId`. Depois rode a pasta **08** inteira com **Run folder**.

| OK | Nº | O que testar | Esperado |
|---|---|---|---|
| [ ] | 08.1 | Login como administrador | **200** |
| [ ] | 08.2 | Remove acessos ao sistema inteiro do restrito | **200**; o Console mostra os acessos revogados |
| [ ] | 08.3 | Garante a filial 1 | **201** ou **409** |
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
| [ ] | 07.12 | Todos os sistemas com filial | **400** |
| [ ] | 07.13 | Acesso ao sistema 7 | **400** |
| [ ] | 07.13b | Filial inexistente (99) | **400** |
| [ ] | 07.13c | Carteira de corretor inexistente | **400** |
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
