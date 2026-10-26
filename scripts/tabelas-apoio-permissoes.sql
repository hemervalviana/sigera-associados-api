/*
  Manutenção das tabelas de apoio pela API: permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez.

  Concede ao login usr_sisgera_api INSERT, UPDATE e DELETE somente em:
  - dbo.T000PARA (filiais)
  - dbo.T000SCLI (situações do associado)
  - dbo.T000TRTA (tratamentos)
  - dbo.T000MUNI (municípios)

  A exclusão é física, mas a API recusa excluir o que estiver em uso
  (e as FKs do banco também barram).

  A API grava sem OUTPUT em todas as tabelas do legado, então triggers
  nessas tabelas não impedem a gravação. Mesmo assim, confira com a
  consulta no fim deste arquivo se existe alguma e o que ela faz.

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
    GRANT INSERT, UPDATE, DELETE ON dbo.T000PARA TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T000SCLI TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T000TRTA TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T000MUNI TO usr_sisgera_api;
    PRINT N''Permissões das tabelas de apoio concedidas em ' + @Banco + N''';
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
  Triggers nas tabelas de apoio (rode em uma base; deve voltar vazio):

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name IN ('T000PARA', 'T000SCLI', 'T000TRTA', 'T000MUNI');

  Conferência das permissões (em cada base):

  SELECT OBJECT_NAME(major_id) AS tabela, permission_name
    FROM sys.database_permissions
   WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'usr_sisgera_api')
     AND OBJECT_NAME(major_id) IN (N'T000PARA', N'T000SCLI', N'T000TRTA', N'T000MUNI');

  Para desfazer (em cada base):

  REVOKE INSERT, UPDATE, DELETE ON dbo.T000PARA FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T000SCLI FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T000TRTA FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T000MUNI FROM usr_sisgera_api;
*/
