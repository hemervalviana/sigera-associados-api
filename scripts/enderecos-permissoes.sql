/*
  Endereços do associado pela API: permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez.

  Concede ao login usr_sisgera_api INSERT, UPDATE e DELETE somente em:
  - dbo.T000ENDE (endereços da pessoa)
  - dbo.T000TEND (tipos de endereço)

  Antes de usar a API, confira na consulta 1 abaixo que T000ENDE.ID_EN_PCP
  usa S (principal) e N (não principal), como a API grava.

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
    GRANT INSERT, UPDATE, DELETE ON dbo.T000ENDE TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T000TEND TO usr_sisgera_api;
    PRINT N''Permissões de endereços concedidas em ' + @Banco + N''';
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
  1) Valores usados em ID_EN_PCP (rode em cada base; deve voltar só S e N):

  SELECT ID_EN_PCP, COUNT(*) AS quantidade FROM dbo.T000ENDE GROUP BY ID_EN_PCP;

  2) Pessoas com mais de um endereço principal (a API mantém um só):

  SELECT CD_EMP, SQ_PES, COUNT(*) AS principais
    FROM dbo.T000ENDE
   WHERE ID_EN_PCP = 'S'
   GROUP BY CD_EMP, SQ_PES
  HAVING COUNT(*) > 1;

  3) Triggers nessas tabelas:

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name IN ('T000ENDE', 'T000TEND');

  Para desfazer (em cada base):

  REVOKE INSERT, UPDATE, DELETE ON dbo.T000ENDE FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T000TEND FROM usr_sisgera_api;
*/
