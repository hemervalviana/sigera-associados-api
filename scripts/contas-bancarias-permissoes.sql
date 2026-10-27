/*
  Contas bancárias do associado pela API: permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez.

  Concede ao login usr_sisgera_api INSERT, UPDATE e DELETE somente em:
  - dbo.T000CCOR (contas bancárias da pessoa)
  - dbo.T000BANC (bancos)

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
    GRANT INSERT, UPDATE, DELETE ON dbo.T000CCOR TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T000BANC TO usr_sisgera_api;
    PRINT N''Permissões de contas bancárias concedidas em ' + @Banco + N''';
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
  Valores usados nos códigos sem tabela (rode em uma base), para definir
  as regras na API:

  SELECT TP_CTA_COR, COUNT(*) AS quantidade FROM dbo.T000CCOR GROUP BY TP_CTA_COR;
  SELECT ID_CC_ATV,  COUNT(*) AS quantidade FROM dbo.T000CCOR GROUP BY ID_CC_ATV;
  SELECT env_opt_cax, COUNT(*) AS quantidade FROM dbo.T000CCOR GROUP BY env_opt_cax;
  SELECT CD_SIT_BCO, COUNT(*) AS quantidade FROM dbo.T000BANC GROUP BY CD_SIT_BCO;

  Triggers nessas tabelas:

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name IN ('T000CCOR', 'T000BANC');

  Para desfazer (em cada base):

  REVOKE INSERT, UPDATE, DELETE ON dbo.T000CCOR FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T000BANC FROM usr_sisgera_api;
*/
