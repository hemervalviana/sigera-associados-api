# Postman — passo a passo da SigeraAssociados API

A coleção `SigeraAssociados API.postman_collection.json` tem 24 requisições, organizadas em 6 pastas, na ordem em que você deve usá-las. Cada requisição tem uma explicação na aba **Docs** do Postman.

## 1. Instalar e abrir o Postman

1. Baixe em https://www.postman.com/downloads/ (ou `winget install Postman.Postman`).
2. Abra o Postman. Pode criar uma conta grátis ou clicar em **Continue without an account** / **Lightweight API Client**.

## 2. Importar a coleção

1. Clique em **Import** (canto superior esquerdo).
2. Arraste o arquivo `doc\postman\SigeraAssociados API.postman_collection.json` para a janela.
3. Clique em **Import**. A coleção **SigeraAssociados API** aparece no menu **Collections**, à esquerda.

## 3. Preencher a sua senha

1. Clique no nome **SigeraAssociados API**, no menu à esquerda.
2. Abra a aba **Variables**.
3. Na linha `senha`, coluna **Current value**, digite a sua senha.
4. Confira `email` (já vem com `hemerval@outlook.com`) e `baseUrl` (`https://localhost:7001`).
5. Clique em **Save** (Ctrl+S).

O valor em **Current value** fica só no seu computador e não vai junto se você exportar a coleção.

## 4. Desligar a verificação do certificado (só uma vez)

A API local usa um certificado de desenvolvimento, que o Postman não reconhece.

1. Clique na engrenagem (canto superior direito) > **Settings**.
2. Na aba **General**, desligue **SSL certificate verification**.

## 5. Subir a API

No PowerShell:

```powershell
cd D:\SIGERA_2026\SIGERA_ASSOCIADOS_API
dotnet run --project src\SigeraAssociados.Api --launch-profile https
```

Deixe essa janela aberta.

## 6. Fazer login

1. Abra **01 - Autenticação > 01.1 - Login** e clique em **Send**.
2. A resposta deve ser **200 OK**, com um `accessToken`.
3. O token já foi guardado na variável `token`, e todas as outras requisições passam a usá-lo sozinhas.
4. O token vale 15 minutos. Se aparecer **401 Unauthorized**, faça o login de novo.

## 7. Usar as outras pastas

| Pasta | Para que serve | Quem pode |
|---|---|---|
| 01 - Autenticação | Login, confirmar e-mail, recuperar senha | Qualquer pessoa |
| 02 - Minha conta | Ver seus grupos e acessos | Qualquer usuário logado |
| 03 - Diagnóstico | Testar conexão com cada banco | Administradores |
| 04 - Associados | Consultar associados | Conforme os acessos |
| 05 - Usuários | Criar, listar, trocar grupo, bloquear, excluir | Administradores e Gestores |
| 06 - Acessos | Definir o que cada usuário vê | Administradores |

Para escolher o banco nas pastas 03 e 04, troque a variável `sistema` da coleção: 1 = PROASP, 2 = SONASP, 3 = ABSP, 4 = GSP.

## 8. Teste completo sugerido

1. **01.1** Login.
2. **03.1** Testar conexão, com `sistema` = 1, 2, 3 e 4.
3. **04.2** Buscar associados.
4. **05.2** Criar usuário de teste. O `usuarioId` é guardado sozinho.
5. **06.2** Dar acesso a uma filial para esse usuário.
6. **06.4** Conferir os acessos.
7. **05.7** e **05.8** Bloquear e desbloquear.
8. **05.9** Excluir o usuário de teste.

## 9. Onde olhar cada coisa no Postman

- **Params**: filtros da URL. Marque a caixinha para ativar um filtro.
- **Headers**: cabeçalhos. O `X-Sigera-Sistema` já vem preenchido com `{{sistema}}`.
- **Body**: dados enviados nos POST e PUT.
- **Scripts > Post-response**: código que guarda token e IDs depois da resposta.
- **Docs** (ícone de documento à direita): explicação de cada requisição.
- **Test Results** (embaixo, junto da resposta): mostra se o status veio como esperado.
- **Console** (canto inferior esquerdo): mostra mensagens como "Token salvo".

## 10. Rodar uma pasta inteira de uma vez

1. Passe o mouse sobre a pasta, clique nos `...` > **Run folder**.
2. Clique em **Run**. O Postman executa as requisições em ordem e mostra quais passaram.

Não rode a pasta **05** inteira sem querer: o **05.9** exclui o usuário que está em `usuarioId`.

## Problemas comuns

| Mensagem | O que fazer |
|---|---|
| `Error: connect ECONNREFUSED` | A API não está rodando. Volte ao passo 5. |
| `SSL Error: self signed certificate` | Desligue a verificação SSL (passo 4). |
| **401 Unauthorized** | Token vencido ou ausente: rode o **01.1 - Login**. |
| **403 Forbidden** | Seu grupo não tem permissão para essa rota, ou você está alterando a própria conta. |
| **400** com "cabeçalho X-Sigera-Sistema" | Variável `sistema` vazia ou fora de 1 a 4. |
