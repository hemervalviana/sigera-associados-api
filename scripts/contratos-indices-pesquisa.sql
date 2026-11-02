/*
  Índices estreitos para a pesquisa de contratos (GET /api/contratos).

  Por quê: a pesquisa precisa de filial, número, data, associado, corretor,
  situação, produto e exclusão de cada contrato. Sem índice, o SQL Server lê
  a T009CTRT inteira (mais de 50 colunas por linha). Com o disco lento
  (espera PAGEIOLATCH_SH), essa leitura passava de 30 segundos quando a
  tabela não estava em memória. Com estes índices ele lê só as colunas que
  a pesquisa usa (poucas páginas).

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez: só cria o índice onde ele não existe.
  Não muda dados nem o legado (o legado continua funcionando igual).

  Rode DEPOIS de scripts/contratos-colunas-api-e-permissoes.sql (que cria
  T009CTRT.DT_EXC_API e T000CLIE.DT_EXC_API já existe pelo script de
  associados).

  Execute com um login que possa criar índice (sa ou db_owner/db_ddladmin).
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

IF COL_LENGTH(N''dbo.T009CTRT'', N''DT_EXC_API'') IS NULL
    PRINT N''T009CTRT.DT_EXC_API não existe em ' + @Banco + N': rode antes o contratos-colunas-api-e-permissoes.sql.'';
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.T009CTRT'') AND name = N''IX_T009CTRT_API_PESQUISA'')
BEGIN
    CREATE NONCLUSTERED INDEX IX_T009CTRT_API_PESQUISA
        ON dbo.T009CTRT (CD_EMP, DT_CTR DESC)
        INCLUDE (CD_FIL, SQ_CTR, SQ_PES_CLI, SQ_PES_CRO, CD_ST_CTR, CD_PRD, DT_EXC_API);
    PRINT N''IX_T009CTRT_API_PESQUISA criado em ' + @Banco + N''';
END
ELSE
    PRINT N''IX_T009CTRT_API_PESQUISA já existe em ' + @Banco + N''';

IF COL_LENGTH(N''dbo.T000CLIE'', N''DT_EXC_API'') IS NULL
    PRINT N''T000CLIE.DT_EXC_API não existe em ' + @Banco + N': rode antes o associados-colunas-api-e-permissoes.sql.'';
ELSE IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N''dbo.T000CLIE'') AND name = N''IX_T000CLIE_API_EXCLUSAO'')
BEGIN
    CREATE NONCLUSTERED INDEX IX_T000CLIE_API_EXCLUSAO
        ON dbo.T000CLIE (CD_EMP, SQ_PES_CLI)
        INCLUDE (DT_EXC_API, CD_FIL, SQ_PES_CRO_CAD);
    PRINT N''IX_T000CLIE_API_EXCLUSAO criado em ' + @Banco + N''';
END
ELSE
    PRINT N''IX_T000CLIE_API_EXCLUSAO já existe em ' + @Banco + N''';';

        EXEC (@cmd);
    END

    FETCH NEXT FROM cur INTO @Banco;
END

CLOSE cur;
DEALLOCATE cur;
GO

/*
  Conferência (em cada base):

  SELECT OBJECT_NAME(object_id) AS tabela, name
    FROM sys.indexes
   WHERE name IN (N'IX_T009CTRT_API_PESQUISA', N'IX_T000CLIE_API_EXCLUSAO');

  Saúde do servidor (se as esperas de disco continuarem):

  -- Esperas acumuladas; PAGEIOLATCH_* alto = disco lento ou pouca memória para cache
  SELECT TOP 10 wait_type, wait_time_ms / 1000 AS segundos, waiting_tasks_count
    FROM sys.dm_os_wait_stats
   WHERE wait_type LIKE 'PAGEIOLATCH%' OR wait_type IN ('WRITELOG', 'IO_COMPLETION')
   ORDER BY wait_time_ms DESC;

  -- Memória máxima configurada para o SQL Server (MB)
  SELECT name, value_in_use FROM sys.configurations WHERE name = N'max server memory (MB)';

  Para desfazer (em cada base):

  DROP INDEX IX_T009CTRT_API_PESQUISA ON dbo.T009CTRT;
  DROP INDEX IX_T000CLIE_API_EXCLUSAO ON dbo.T000CLIE;
*/
