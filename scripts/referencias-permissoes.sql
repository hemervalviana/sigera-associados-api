/*
  Referências do associado pela API: permissão de gravação.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez.

  Concede ao login usr_sisgera_api INSERT, UPDATE e DELETE somente em:
  - dbo.T000REFE (referências pessoais da pessoa)

  A API gera SQ_REF como MAX + 1 da pessoa (até 9999) e grava NR_FN1_REF e
  NR_FN2_REF só com os números (DDD + número). Antes de usar, confira as
  consultas 1 e 2 abaixo.

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
    GRANT INSERT, UPDATE, DELETE ON dbo.T000REFE TO usr_sisgera_api;
    PRINT N''Permissões de referências concedidas em ' + @Banco + N''';
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
  1) Formato gravado pelo legado nos telefones (a API grava só números,
     DDD + número, 10 ou 11 dígitos):

  SELECT TOP 50 NR_FN1_REF, NR_FN2_REF
    FROM dbo.T000REFE
   WHERE NR_FN1_REF LIKE '%[^0-9]%' OR LEN(NR_FN1_REF) NOT IN (10, 11)
      OR NR_FN2_REF LIKE '%[^0-9]%' OR LEN(NR_FN2_REF) NOT IN (10, 11);

  2) Triggers na tabela:

  SELECT t.name AS tabela, tr.name AS trigger_nome, te.type_desc AS evento
    FROM sys.triggers tr
    JOIN sys.tables t ON t.object_id = tr.parent_id
    JOIN sys.trigger_events te ON te.object_id = tr.object_id
   WHERE t.name = 'T000REFE';

  Para desfazer (em cada base):

  REVOKE INSERT, UPDATE, DELETE ON dbo.T000REFE FROM usr_sisgera_api;
*/
