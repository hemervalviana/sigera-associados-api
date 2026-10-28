/*
  FK da categoria do contrato: T009CTRT (CD_EMP, CD_CAT) -> T009CAT (CD_EMP, CD_CAT).

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez: só cria a FK onde ela ainda não existe.

  CD_CAT nulo continua aceito (FK não confere nulo). A FK é criada WITH
  CHECK (confere os contratos que já existem). Para não travar, contrato
  com categoria que não está na T009CAT ganha uma categoria TEMPORÁRIA com
  o MESMO código e o nome 'TEMPORARIA <código> - AJUSTAR'. Nenhum contrato
  é alterado e o código original não se perde. Depois, para cada
  temporária, o usuário:
    - renomeia (PUT /api/contratos/categorias/{cdCategoria}) se o código
      estiver certo; ou
    - troca a categoria dos contratos para a correta e exclui a temporária
      (o DELETE só passa quando nenhum contrato usa).
  Tudo numa transação por base: se a FK falhar, as temporárias são desfeitas.

  Efeito no legado: depois da FK, o banco recusa contrato com categoria
  inexistente e a exclusão de categoria usada por contrato (a API já
  fazia as duas conferências).

  Execute com um login que possa alterar tabelas (sa ou db_owner nas bases).
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

IF OBJECT_ID(N''dbo.FK_T009CTRT_T009CAT'', N''F'') IS NOT NULL
BEGIN
    PRINT N''FK_T009CTRT_T009CAT já existe em ' + @Banco + N''';
END
ELSE IF OBJECT_ID(N''dbo.T009CAT'', N''U'') IS NULL
BEGIN
    PRINT N''Tabela T009CAT não existe em ' + @Banco + N': FK não criada.'';
END
ELSE
BEGIN
    DECLARE @Temporarias int;

    BEGIN TRY
        BEGIN TRANSACTION;

        INSERT INTO dbo.T009CAT (CD_EMP, CD_CAT, NM_CAT)
        SELECT DISTINCT c.CD_EMP, c.CD_CAT,
               ''TEMPORARIA '' + CONVERT(varchar(3), CONVERT(int, c.CD_CAT)) + '' - AJUSTAR''
          FROM dbo.T009CTRT c
         WHERE c.CD_CAT IS NOT NULL
           AND NOT EXISTS (SELECT 1 FROM dbo.T009CAT t WHERE t.CD_EMP = c.CD_EMP AND t.CD_CAT = c.CD_CAT);

        SET @Temporarias = @@ROWCOUNT;

        ALTER TABLE dbo.T009CTRT WITH CHECK
            ADD CONSTRAINT FK_T009CTRT_T009CAT FOREIGN KEY (CD_EMP, CD_CAT)
            REFERENCES dbo.T009CAT (CD_EMP, CD_CAT);

        ALTER TABLE dbo.T009CTRT CHECK CONSTRAINT FK_T009CTRT_T009CAT;

        COMMIT TRANSACTION;

        PRINT N''FK_T009CTRT_T009CAT criada em ' + @Banco + N'; categorias temporárias criadas: '' + CONVERT(nvarchar(10), @Temporarias);
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        PRINT N''Falha em ' + @Banco + N' (nada foi gravado): '' + ERROR_MESSAGE();
    END CATCH
END';

        EXEC (@cmd);
    END

    FETCH NEXT FROM cur INTO @Banco;
END

CLOSE cur;
DEALLOCATE cur;
GO

/*
  Conferência (em cada base):

  Categorias temporárias que ainda precisam de ajuste, com os contratos:

  SELECT t.CD_CAT, t.NM_CAT, COUNT(c.SQ_CTR) AS contratos
    FROM dbo.T009CAT t
    LEFT JOIN dbo.T009CTRT c ON c.CD_EMP = t.CD_EMP AND c.CD_CAT = t.CD_CAT
   WHERE t.NM_CAT LIKE 'TEMPORARIA % - AJUSTAR'
   GROUP BY t.CD_CAT, t.NM_CAT
   ORDER BY t.CD_CAT;

  FK ativa (is_disabled = 0 e is_not_trusted = 0):

  SELECT fk.name, fk.is_disabled, fk.is_not_trusted
    FROM sys.foreign_keys fk
   WHERE fk.name = N'FK_T009CTRT_T009CAT';

  Para desfazer (em cada base):

  ALTER TABLE dbo.T009CTRT DROP CONSTRAINT FK_T009CTRT_T009CAT;
  -- e, se quiser, as temporárias sem contrato:
  DELETE t FROM dbo.T009CAT t
   WHERE t.NM_CAT LIKE 'TEMPORARIA % - AJUSTAR'
     AND NOT EXISTS (SELECT 1 FROM dbo.T009CTRT c WHERE c.CD_EMP = t.CD_EMP AND c.CD_CAT = t.CD_CAT);
*/
