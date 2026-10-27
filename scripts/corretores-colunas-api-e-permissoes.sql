/*
  CRUD de corretores pela API: colunas novas em T009CRRT e permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez: só cria o que não existe.

  1) Colunas novas em dbo.T009CRRT (aceitam nulo; o sistema legado não é
     afetado e não precisa conhecê-las):
     - DT_EXC_API      datetime      : data da exclusão lógica feita pela API
     - NM_USU_EXC_API  varchar(256)  : quem excluiu (e-mail do usuário)

     IMPORTANTE: rode este script ANTES de publicar a versão da API com o
     CRUD de corretores. A API passa a ler essas colunas em toda consulta de
     corretores (inclusive na lista de corretores da concessão de acesso e
     no vínculo do associado ao corretor).

  2) Permissão do login usr_sisgera_api: INSERT e UPDATE em T000PESS,
     T000PFIS, T000PJUR e T009CRRT (a leitura já vem do db_datareader).
     Não há DELETE nessas: a exclusão do corretor é lógica, como a do associado.
     Tabelas de apoio do corretor: INSERT, UPDATE e DELETE em T009AGENT
     (agentes) e T009GVED (gerentes de vendas).

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

IF COL_LENGTH(N''dbo.T009CRRT'', N''DT_EXC_API'') IS NULL
BEGIN
    ALTER TABLE dbo.T009CRRT ADD DT_EXC_API datetime NULL;
    PRINT N''Coluna DT_EXC_API criada em T009CRRT de ' + @Banco + N''';
END

IF COL_LENGTH(N''dbo.T009CRRT'', N''NM_USU_EXC_API'') IS NULL
BEGIN
    ALTER TABLE dbo.T009CRRT ADD NM_USU_EXC_API varchar(256) NULL;
    PRINT N''Coluna NM_USU_EXC_API criada em T009CRRT de ' + @Banco + N''';
END

IF DATABASE_PRINCIPAL_ID(N''usr_sisgera_api'') IS NOT NULL
BEGIN
    GRANT INSERT, UPDATE ON dbo.T000PESS TO usr_sisgera_api;
    GRANT INSERT, UPDATE ON dbo.T000PFIS TO usr_sisgera_api;
    GRANT INSERT, UPDATE ON dbo.T000PJUR TO usr_sisgera_api;
    GRANT INSERT, UPDATE ON dbo.T009CRRT TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T009AGENT TO usr_sisgera_api;
    GRANT INSERT, UPDATE, DELETE ON dbo.T009GVED TO usr_sisgera_api;
    PRINT N''Permissões de corretores concedidas em ' + @Banco + N''';
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
   WHERE object_id = OBJECT_ID(N'dbo.T009CRRT')
     AND name IN (N'DT_EXC_API', N'NM_USU_EXC_API');

  2) Corretores fora da regra "física OU jurídica" (a API não mostra esses):
     tipo F sem T000PFIS, tipo J sem T000PJUR, ou com as duas.

  SELECT a.SQ_PES, a.NM_PES, a.TP_PES,
         CASE WHEN EXISTS (SELECT 1 FROM dbo.T000PFIS f WHERE f.CD_EMP = a.CD_EMP AND f.SQ_PES_FIS = a.SQ_PES) THEN 'S' ELSE 'N' END AS tem_pfis,
         CASE WHEN EXISTS (SELECT 1 FROM dbo.T000PJUR j WHERE j.CD_EMP = a.CD_EMP AND j.SQ_PES_JUR = a.SQ_PES) THEN 'S' ELSE 'N' END AS tem_pjur
    FROM dbo.T000PESS a
    JOIN dbo.T009CRRT b ON b.CD_EMP = a.CD_EMP AND b.SQ_PES_CRO = a.SQ_PES
   WHERE NOT (
         (a.TP_PES = 'F'
          AND EXISTS (SELECT 1 FROM dbo.T000PFIS f WHERE f.CD_EMP = a.CD_EMP AND f.SQ_PES_FIS = a.SQ_PES)
          AND NOT EXISTS (SELECT 1 FROM dbo.T000PJUR j WHERE j.CD_EMP = a.CD_EMP AND j.SQ_PES_JUR = a.SQ_PES))
      OR (a.TP_PES = 'J'
          AND EXISTS (SELECT 1 FROM dbo.T000PJUR j WHERE j.CD_EMP = a.CD_EMP AND j.SQ_PES_JUR = a.SQ_PES)
          AND NOT EXISTS (SELECT 1 FROM dbo.T000PFIS f WHERE f.CD_EMP = a.CD_EMP AND f.SQ_PES_FIS = a.SQ_PES)));

  3) Valores usados nos campos de código do legado. A API aceita situação
     1 (ATIVO) e 2 (INATIVO) e tipo 1 (CORRETOR) e 2 (COLABORADOR); outro
     valor gravado faz a alteração do corretor dar 400 até ser corrigido.
     CL_CRO segue sem lista.

  SELECT CL_CRO, COUNT(*) AS quantidade FROM dbo.T009CRRT GROUP BY CL_CRO;
  SELECT CD_SIT_CRRT, COUNT(*) AS quantidade FROM dbo.T009CRRT GROUP BY CD_SIT_CRRT;
  SELECT TP_CRRT, COUNT(*) AS quantidade FROM dbo.T009CRRT GROUP BY TP_CRRT;

     Representante fora da regra do legado (não é pessoa jurídica ou é de
     outra filial); a API recusa esses ao alterar o corretor:

  SELECT c.SQ_PES_CRO, c.CD_FIL, c.SQ_PES_RPR, r.CD_FIL AS filial_rpr, p.TP_PES AS tipo_rpr
    FROM dbo.T009CRRT c
    JOIN dbo.T009CRRT r ON r.CD_EMP = c.CD_EMP AND r.SQ_PES_CRO = c.SQ_PES_RPR
    JOIN dbo.T000PESS p ON p.CD_EMP = r.CD_EMP AND p.SQ_PES = r.SQ_PES_CRO
   WHERE p.TP_PES <> 'J' OR ISNULL(r.CD_FIL, -1) <> ISNULL(c.CD_FIL, -1);

     Gerente de vendas (sem FK) que não existe em T009GVED:

  SELECT c.SQ_PES_CRO, c.SQ_GVED FROM dbo.T009CRRT c
   WHERE c.SQ_GVED IS NOT NULL
     AND NOT EXISTS (SELECT 1 FROM dbo.T009GVED g WHERE g.CD_EMP = c.CD_EMP AND g.SQ_GVED = c.SQ_GVED);

  4) Triggers nessas tabelas:

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name IN ('T000PESS', 'T000PFIS', 'T000PJUR', 'T009CRRT', 'T009AGENT', 'T009GVED');

  Para desfazer (em cada base; só depois de voltar a API para a versão anterior):

  REVOKE INSERT, UPDATE ON dbo.T000PJUR FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE ON dbo.T009CRRT FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T009AGENT FROM usr_sisgera_api;
  REVOKE INSERT, UPDATE, DELETE ON dbo.T009GVED FROM usr_sisgera_api;
  ALTER TABLE dbo.T009CRRT DROP COLUMN DT_EXC_API, NM_USU_EXC_API;
*/
