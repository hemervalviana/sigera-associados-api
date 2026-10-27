/*
  Créditos do associado, formas de pagamento, produtos e convênios pela API:
  permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez.

  Concede ao login usr_sisgera_api INSERT, UPDATE e DELETE somente em:
  - dbo.T009EXTRA (créditos do associado)
  - dbo.T009FRPG  (formas de pagamento)
  - dbo.T009PRDT  (produtos)
  - dbo.T009CONV  (convênios)

  A API só lê T009RBSR (averbações) e T009CONT (contas correntes), pelo
  db_datareader que o login já tem.

  Regras do legado que a API segue no crédito:
  - SQ_PGT = MAX + 1 do associado;
  - VR_SLD = VR_PGT quando a forma (T009FRPG.ID_GER_SLD) gera saldo, senão 0;
  - só exclui quando VR_PGT = VR_SLD.
  Antes de usar, confira as consultas 1 a 4 abaixo.

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
    GRANT INSERT, UPDATE, DELETE ON dbo.T009EXTRA TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T009FRPG TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T009PRDT TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T009CONV TO usr_sisgera_api;
    PRINT N''Permissões de créditos e cadastros financeiros concedidas em ' + @Banco + N''';
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
  1) Formas de pagamento e quais geram saldo (ID_GER_SLD = 1). A API grava
     1 ou 0; confira se há outros valores:

  SELECT TP_FMA_PGT, DE_FMA_PGT, ID_GER_SLD FROM dbo.T009FRPG ORDER BY TP_FMA_PGT;

  2) Créditos com forma de pagamento que não existe em T009FRPG
     (a API exige a forma cadastrada, exceto o código 0):

  SELECT DISTINCT e.TP_PGT
    FROM dbo.T009EXTRA e
   WHERE e.TP_PGT <> 0
     AND NOT EXISTS (SELECT 1 FROM dbo.T009FRPG f WHERE f.CD_EMP = e.CD_EMP AND f.TP_FMA_PGT = e.TP_PGT);

  3) Valores usados em ID_PGT_AUT (a API aceita 1, 2 e 4) e em CD_SIT_CON:

  SELECT ID_PGT_AUT, COUNT(*) AS quantidade FROM dbo.T009EXTRA GROUP BY ID_PGT_AUT;
  SELECT CD_SIT_CON, COUNT(*) AS quantidade FROM dbo.T009CONV GROUP BY CD_SIT_CON;

  4) Triggers nessas tabelas:

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name IN ('T009EXTRA', 'T009FRPG', 'T009PRDT', 'T009CONV');

  Para desfazer (em cada base):

  REVOKE INSERT, UPDATE, DELETE ON dbo.T009EXTRA FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T009FRPG FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T009PRDT FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T009CONV FROM usr_sisgera_api;
*/
