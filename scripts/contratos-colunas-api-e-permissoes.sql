/*
  CRUD de contratos e parcelas pela API: colunas novas em T009CTRT e permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez: só cria o que não existe.

  1) Colunas novas em dbo.T009CTRT (aceitam nulo; o sistema legado não é
     afetado e não precisa conhecê-las):
     - DT_EXC_API      datetime      : data da exclusão lógica feita pela API
     - NM_USU_EXC_API  varchar(256)  : quem excluiu (e-mail do usuário)

     IMPORTANTE: rode este script ANTES de publicar a versão da API com o
     CRUD de contratos. A API passa a ler T009CTRT.DT_EXC_API em toda
     consulta de contratos, inclusive na carteira do corretor (pesquisa e
     acesso aos associados) e na conferência do contrato do crédito.

  2) Permissão do login usr_sisgera_api:
     - INSERT e UPDATE em T009CTRT (sem DELETE: a exclusão é lógica);
     - INSERT, UPDATE e DELETE em T009PAGA (parcelas);
     - INSERT, UPDATE e DELETE em T009CAT (categorias, só administradores).
     A leitura das tabelas de apoio (T009SCTR, T009CARE, T009OREC, T009PROD,
     T009INDI, T009RBSR, T009PARM) já vem do db_datareader.

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

IF COL_LENGTH(N''dbo.T009CTRT'', N''DT_EXC_API'') IS NULL
BEGIN
    ALTER TABLE dbo.T009CTRT ADD DT_EXC_API datetime NULL;
    PRINT N''Coluna DT_EXC_API criada em T009CTRT de ' + @Banco + N''';
END

IF COL_LENGTH(N''dbo.T009CTRT'', N''NM_USU_EXC_API'') IS NULL
BEGIN
    ALTER TABLE dbo.T009CTRT ADD NM_USU_EXC_API varchar(256) NULL;
    PRINT N''Coluna NM_USU_EXC_API criada em T009CTRT de ' + @Banco + N''';
END

IF DATABASE_PRINCIPAL_ID(N''usr_sisgera_api'') IS NOT NULL
BEGIN
    GRANT INSERT, UPDATE ON dbo.T009CTRT TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T009PAGA TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T009CAT TO usr_sisgera_api;
    PRINT N''Permissões de contratos concedidas em ' + @Banco + N''';
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

  1) Colunas criadas:

  SELECT name, TYPE_NAME(user_type_id) AS tipo, max_length, is_nullable
    FROM sys.columns
   WHERE object_id = OBJECT_ID(N'dbo.T009CTRT')
     AND name IN (N'DT_EXC_API', N'NM_USU_EXC_API');

  2) Contratos com QT_PCL diferente da quantidade de parcelas em T009PAGA.
     A API acerta QT_PCL a cada gravação de parcela; estes ficam como estão
     até alguém mexer nas parcelas:

  SELECT c.CD_FIL, c.SQ_CTR, c.QT_PCL, COUNT(p.NR_PCL) AS parcelas
    FROM dbo.T009CTRT c
    LEFT JOIN dbo.T009PAGA p ON p.CD_EMP = c.CD_EMP AND p.CD_FIL = c.CD_FIL AND p.SQ_CTR = c.SQ_CTR
   GROUP BY c.CD_FIL, c.SQ_CTR, c.QT_PCL
  HAVING ISNULL(c.QT_PCL, 0) <> COUNT(p.NR_PCL);

  3) Situações usadas e as que têm regra na API (4 e 9 preenchem o
     encerramento; 9 e 13 exigem a quitação; 10 o cancelamento; 12 a
     finalização). Confira se os códigos batem com a T009SCTR:

  SELECT s.CD_ST_CTR, s.DE_ST_CTR, COUNT(c.SQ_CTR) AS contratos
    FROM dbo.T009SCTR s
    LEFT JOIN dbo.T009CTRT c ON c.CD_EMP = s.CD_EMP AND c.CD_ST_CTR = s.CD_ST_CTR
   GROUP BY s.CD_ST_CTR, s.DE_ST_CTR
   ORDER BY s.CD_ST_CTR;

     Contratos que a API recusaria ao alterar por causa das datas da
     situação (corrigir antes, ou a alteração dá 400):

  SELECT CD_FIL, SQ_CTR, CD_ST_CTR, DT_QIT_CTRT, DT_FIN_CTR, DT_CAN_CTRT
    FROM dbo.T009CTRT
   WHERE (ISNULL(CD_ST_CTR, -1) NOT IN (9, 13) AND DT_QIT_CTRT IS NOT NULL)
      OR (CD_ST_CTR IN (9, 13) AND DT_QIT_CTRT IS NULL)
      OR (CD_ST_CTR = 12 AND DT_FIN_CTR IS NULL) OR (ISNULL(CD_ST_CTR, -1) <> 12 AND DT_FIN_CTR IS NOT NULL)
      OR (CD_ST_CTR = 10 AND DT_CAN_CTRT IS NULL) OR (ISNULL(CD_ST_CTR, -1) <> 10 AND DT_CAN_CTRT IS NOT NULL);

  4) Produtos (CD_PRD): a API grava DT_ULT_DCT só no produto 2.

  SELECT CD_PRD, DE_PRD FROM dbo.T009PRDT ORDER BY CD_PRD;

  5) Parâmetro 1 (saldo zero na parcela paga) por filial. A API considera
     ligado quando TP_FLG = 'S':

  SELECT CD_FIL, SQ_PRM, DE_PRM, TP_FLG FROM dbo.T009PARM WHERE SQ_PRM = 1 ORDER BY CD_FIL;

  6) Categorias usadas pelos contratos que não existem em T009CAT (a API
     recusa a alteração desses contratos até a categoria existir; corrija
     antes de rodar scripts/contratos-fk-categoria.sql):

  SELECT c.CD_CAT, COUNT(*) AS contratos FROM dbo.T009CTRT c
   WHERE c.CD_CAT IS NOT NULL
     AND NOT EXISTS (SELECT 1 FROM dbo.T009CAT t WHERE t.CD_EMP = c.CD_EMP AND t.CD_CAT = c.CD_CAT)
   GROUP BY c.CD_CAT;

  7) Contratos antigos que a alteração pela API passa a recusar (400) até
     serem corrigidos (listas fixas, compra de dívida e depósito). Valores
     a conferir antes de liberar a tela:

  -- tipo do contrato fora de 1 a 5, tipo da compra fora de 1 e 2, entrega física fora de 1 a 3
  SELECT CD_FIL, SQ_CTR, CD_TP_CTRT, VR_CMP_DIV_TP, CD_ENT_FISI
    FROM dbo.T009CTRT
   WHERE (CD_TP_CTRT IS NOT NULL AND CD_TP_CTRT NOT IN (1, 2, 3, 4, 5))
      OR (VR_CMP_DIV_TP IS NOT NULL AND VR_CMP_DIV_TP NOT IN (1, 2))
      OR (CD_ENT_FISI IS NOT NULL AND CD_ENT_FISI NOT IN (1, 2, 3));

  -- compra de dívida incoerente: valor sem tipo, ou tipo sem valor
  SELECT CD_FIL, SQ_CTR, VR_CMP_DIV, VR_CMP_DIV_TP
    FROM dbo.T009CTRT
   WHERE (VR_CMP_DIV > 0 AND VR_CMP_DIV_TP IS NULL)
      OR (ISNULL(VR_CMP_DIV, 0) <= 0 AND VR_CMP_DIV_TP IS NOT NULL);

  -- depósito incompleto ou com valor zero fora do PLANO ASSOCIATIVO (produto 1)
  SELECT CD_FIL, SQ_CTR, CD_PRD, CD_ORI_REC, DT_PGT_AUX, VR_DEP, CD_BCO
    FROM dbo.T009CTRT
   WHERE ISNULL(CD_PRD, 0) <> 1
     AND (CASE WHEN CD_ORI_REC IS NULL THEN 0 ELSE 1 END
        + CASE WHEN DT_PGT_AUX IS NULL THEN 0 ELSE 1 END
        + CASE WHEN VR_DEP     IS NULL THEN 0 ELSE 1 END
        + CASE WHEN CD_BCO     IS NULL THEN 0 ELSE 1 END) BETWEEN 1 AND 3
      OR (ISNULL(CD_PRD, 0) <> 1 AND VR_DEP <= 0);

  -- PLANO ASSOCIATIVO com depósito gravado (a API mantém como está e não deixa mudar)
  SELECT CD_FIL, SQ_CTR, CD_ORI_REC, DT_PGT_AUX, VR_DEP, CD_BCO
    FROM dbo.T009CTRT
   WHERE CD_PRD = 1
     AND (CD_ORI_REC IS NOT NULL OR DT_PGT_AUX IS NOT NULL OR VR_DEP IS NOT NULL OR CD_BCO IS NOT NULL);

  8) Triggers nessas tabelas:

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name IN ('T009CTRT', 'T009PAGA', 'T009CAT');

  Para desfazer (em cada base; só depois de voltar a API para a versão anterior):

  REVOKE INSERT, UPDATE ON dbo.T009CTRT FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T009PAGA FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T009CAT FROM usr_sisgera_api;
  ALTER TABLE dbo.T009CTRT DROP COLUMN DT_EXC_API, NM_USU_EXC_API;
*/
