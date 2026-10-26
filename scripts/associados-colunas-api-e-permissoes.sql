/*
  CRUD de associados pela API: colunas novas em T000CLIE e permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez: só cria o que não existe.

  1) Colunas novas em dbo.T000CLIE (todas aceitam nulo; o sistema legado
     não é afetado e não precisa conhecê-las):
     - DT_EXC_API      datetime      : data da exclusão lógica feita pela API
     - NM_USU_EXC_API  varchar(256)  : quem excluiu (e-mail do usuário)
     - SQ_PES_CRO_CAD  numeric(7,0)  : corretor que cadastrou o associado pela API

     IMPORTANTE: rode este script ANTES de publicar a versão da API com o
     CRUD. A API passa a ler essas colunas em toda consulta de associados.

  2) Permissão do login usr_sisgera_api: INSERT e UPDATE somente em
     T000PESS, T000PFIS e T000CLIE (a leitura já vem do db_datareader).
     Não há DELETE: a exclusão é lógica.
     A trigger TR_CLIE_UPD atualiza T009CTRT; como a trigger e a tabela são
     do dbo, o SQL Server não exige permissão do usr_sisgera_api em T009CTRT.

  Execute com um login que possa alterar tabelas e conceder permissões
  (sa ou db_owner nas bases).
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

IF COL_LENGTH(N''dbo.T000CLIE'', N''DT_EXC_API'') IS NULL
BEGIN
    ALTER TABLE dbo.T000CLIE ADD DT_EXC_API datetime NULL;
    PRINT N''Coluna DT_EXC_API criada em ' + @Banco + N''';
END

IF COL_LENGTH(N''dbo.T000CLIE'', N''NM_USU_EXC_API'') IS NULL
BEGIN
    ALTER TABLE dbo.T000CLIE ADD NM_USU_EXC_API varchar(256) NULL;
    PRINT N''Coluna NM_USU_EXC_API criada em ' + @Banco + N''';
END

IF COL_LENGTH(N''dbo.T000CLIE'', N''SQ_PES_CRO_CAD'') IS NULL
BEGIN
    ALTER TABLE dbo.T000CLIE ADD SQ_PES_CRO_CAD numeric(7, 0) NULL;
    PRINT N''Coluna SQ_PES_CRO_CAD criada em ' + @Banco + N''';
END

IF DATABASE_PRINCIPAL_ID(N''usr_sisgera_api'') IS NOT NULL
BEGIN
    GRANT INSERT, UPDATE ON dbo.T000PESS TO usr_sisgera_api;
    GRANT INSERT, UPDATE ON dbo.T000PFIS TO usr_sisgera_api;
    GRANT INSERT, UPDATE ON dbo.T000CLIE TO usr_sisgera_api;
    PRINT N''Permissões de gravação concedidas em ' + @Banco + N''';
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
  Conferência (em cada base):

  SELECT name, TYPE_NAME(user_type_id) AS tipo, max_length, is_nullable
    FROM sys.columns
   WHERE object_id = OBJECT_ID(N'dbo.T000CLIE')
     AND name IN (N'DT_EXC_API', N'NM_USU_EXC_API', N'SQ_PES_CRO_CAD');

  SELECT OBJECT_NAME(major_id) AS tabela, permission_name
    FROM sys.database_permissions
   WHERE grantee_principal_id = DATABASE_PRINCIPAL_ID(N'usr_sisgera_api')
     AND OBJECT_NAME(major_id) IN (N'T000PESS', N'T000PFIS', N'T000CLIE');

  Para desfazer (em cada base; só depois de voltar a API para a versão anterior):

  REVOKE INSERT, UPDATE ON dbo.T000PESS FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE ON dbo.T000PFIS FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE ON dbo.T000CLIE FROM usr_sisgera_api;
  ALTER TABLE dbo.T000CLIE DROP COLUMN DT_EXC_API, NM_USU_EXC_API, SQ_PES_CRO_CAD;
*/
