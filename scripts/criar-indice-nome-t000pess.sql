/*
  Cria o índice de nome em T000PESS nas 4 bases de negócio do SIGERA.

  Por quê: a busca de associados por nome usa "começa com"
  (NM_PES LIKE 'MARIA%'). Com este índice o SQL Server encontra os nomes
  sem ler a tabela inteira.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez: só cria onde o índice não existe.

  Execute com um login que tenha permissão de criar índice
  (sa ou db_owner/db_ddladmin nas bases). O usr_sisgera_api só lê
  estas bases e não consegue criar o índice.
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
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N''IX_T000PESS_NM_PES''
      AND object_id = OBJECT_ID(N''dbo.T000PESS''))
BEGIN
    CREATE INDEX IX_T000PESS_NM_PES ON dbo.T000PESS (CD_EMP, NM_PES);
    PRINT N''Índice IX_T000PESS_NM_PES criado em ' + @Banco + N''';
END
ELSE
    PRINT N''Índice IX_T000PESS_NM_PES já existe em ' + @Banco + N''';';

        EXEC (@cmd);
    END

    FETCH NEXT FROM cur INTO @Banco;
END

CLOSE cur;
DEALLOCATE cur;
GO

/*
  Conferência: deve listar o índice nas 4 bases.

  SELECT 'PROASP_SIG' AS Banco, name FROM PROASP_SIG.sys.indexes WHERE name = 'IX_T000PESS_NM_PES'
  UNION ALL SELECT 'SONASP_SIG', name FROM SONASP_SIG.sys.indexes WHERE name = 'IX_T000PESS_NM_PES'
  UNION ALL SELECT 'ABSP_SIG', name FROM ABSP_SIG.sys.indexes WHERE name = 'IX_T000PESS_NM_PES'
  UNION ALL SELECT 'GSP_SIG', name FROM GSP_SIG.sys.indexes WHERE name = 'IX_T000PESS_NM_PES';

  Para desfazer (em cada base):
  DROP INDEX IX_T000PESS_NM_PES ON dbo.T000PESS;
*/
