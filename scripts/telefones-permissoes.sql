/*
  Telefones do associado pela API: permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez.

  Concede ao login usr_sisgera_api INSERT, UPDATE e DELETE somente em:
  - dbo.T000TELE (telefones da pessoa)
  - dbo.T000TTEL (tipos de telefone)

  A API gera SQ_TELE como MAX + 1 da empresa (a tabela inteira) e grava
  NR_FN só com os números. Antes de usar, confira as consultas 1 a 3 abaixo.

  Execute com um login que possa conceder permissões (sa ou db_owner).
*/

SET NOCOUNT ON;

DECLARE @Bancos TABLE (Nome sysname);
INSERT INTO @Bancos VALUES (N'PROASP_SIG'), (N'SONASP_SIG'), (N'ABSP_SIG'), (N'GSP_SIG');

DECLARE @Banco sysname, @cmd nvarchar(max);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Nome FROM @Bancos;
OPEN cur;
FETCH NEXT FROM cur INTO @Banco;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF DB_ID(@Banco) IS NULL
    BEGIN
        PRINT N'Banco não encontrado: ' + @Banco;
    END
    ELSE
    BEGIN
        SET @cmd = N'USE ' + QUOTENAME(@Banco) + N';

IF DATABASE_PRINCIPAL_ID(N''usr_sisgera_api'') IS NOT NULL
BEGIN
    GRANT INSERT, UPDATE, DELETE ON dbo.T000TELE TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T000TTEL TO usr_sisgera_api;
    PRINT N''Permissões de telefones concedidas em ' + @Banco + N''';
END
ELSE
    PRINT N''Usuário usr_sisgera_api não existe em ' + @Banco + N' (rode antes o criar-login-usr_sisgera_api.sql).'';';

        EXEC (@cmd);
    END

    FETCH NEXT FROM cur INTO @Banco;
END

CLOSE cur;
DEALLOCATE cur;
GO

/*
  1) Como o legado numera SQ_TELE (por empresa ou por pessoa; existe -1?):

  SELECT MIN(SQ_TELE) AS menor, MAX(SQ_TELE) AS maior, COUNT(*) AS linhas,
         COUNT(DISTINCT SQ_TELE) AS distintos
    FROM dbo.T000TELE;

  2) Formato gravado em NR_FN (a API grava só números, 8 ou 9 dígitos):

  SELECT TOP 50 NR_DDD, NR_FN, LEN(NR_FN) AS tamanho
    FROM dbo.T000TELE
   WHERE NR_FN LIKE '%[^0-9]%' OR LEN(NR_FN) NOT IN (8, 9);

  3) Triggers nessas tabelas (o default -1 de SQ_TELE pode indicar uma
     trigger do legado que gera a sequência):

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name IN ('T000TELE', 'T000TTEL');

  Para desfazer (em cada base):

  REVOKE INSERT, UPDATE, DELETE ON dbo.T000TELE FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T000TTEL FROM usr_sisgera_api;
*/
