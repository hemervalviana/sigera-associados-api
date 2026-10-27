/*
  Chaves Pix do associado pela API: permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez.

  Concede ao login usr_sisgera_api INSERT, UPDATE e DELETE somente em:
  - dbo.T009PIX (chaves Pix da pessoa)

  CD_PIX (valores fixos, sem tabela): 1 = CPF/CNPJ, 2 = CELULAR, 3 = E-MAIL, 4 = ALEATÓRIA.
  A API gera SQ_REG como MAX + 1 da pessoa (até 99) e grava VL_CHAVE assim:
  CPF/CNPJ só números; celular DDD + número, sem o +55; e-mail e aleatória
  em minúsculas. Antes de usar, confira as consultas 1 a 3 abaixo.

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
    GRANT INSERT, UPDATE, DELETE ON dbo.T009PIX TO usr_sisgera_api;
    PRINT N''Permissões de chaves Pix concedidas em ' + @Banco + N''';
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
  1) Tipos usados em CD_PIX (deve voltar só 1 a 4):

  SELECT CD_PIX, COUNT(*) AS quantidade FROM dbo.T009PIX GROUP BY CD_PIX;

  2) Formato gravado em VL_CHAVE pelo legado, por tipo (amostra):

  SELECT CD_PIX, VL_CHAVE, LEN(VL_CHAVE) AS tamanho
    FROM (SELECT CD_PIX, VL_CHAVE,
                 ROW_NUMBER() OVER (PARTITION BY CD_PIX ORDER BY SQ_PES DESC) AS n
            FROM dbo.T009PIX) x
   WHERE n <= 10
   ORDER BY CD_PIX;

  3) Triggers na tabela:

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name = 'T009PIX';

  Para desfazer (em cada base):

  REVOKE INSERT, UPDATE, DELETE ON dbo.T009PIX FROM usr_sisgera_api;
*/
