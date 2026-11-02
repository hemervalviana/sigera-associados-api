/*
  Usuário da API na tabela de usuários do legado (T009USUA).

  Por quê: os triggers do legado em T009CTRT (TR_CTRT_INS, TR_CTRT_UPD,
  TR_CTRT_DEL) e T009PAGA (TR_PAGA_UPD, TR_PAGA_DEL) gravam um log com o
  código do usuário (T009LCTR.CD_USU, obrigatório e com FK para T009USUA).
  Eles procuram o usuário pelo login do SQL Server (SYSTEM_USER) e, se não
  acham, pelo NM_USU do contrato. A API grava com o login usr_sisgera_api e
  o NM_USU com o e-mail do usuário; nenhum dos dois existe em T009USUA, o
  CD_USU fica nulo e a gravação inteira é desfeita (inclusão de contrato,
  alterações, quitação, estornos, parcelas...).

  O que faz: cadastra em T009USUA, para cada empresa da base, o usuário
  NM_USU = 'usr_sisgera_api' (igual ao login, para o SYSTEM_USER do
  trigger achá-lo), sem senha web (PWD_WEB nulo), FLG_USER_WEB = 'N' e
  FLG_USER_STATUS = '1' (ATIVO). O CD_USU é gerado pela tabela (IDENTITY).
  No log do legado, o que a API fizer aparece com esse usuário; quem fez de
  verdade fica no NM_USU do contrato e no log da API.

  Nenhum trigger é alterado.

  Bases: PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG.
  Pode ser executado mais de uma vez: só cadastra onde ainda não existe.
  Execute com sa ou db_owner (o usr_sisgera_api não grava em T009USUA).
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

DECLARE @Login varchar(100) = ''usr_sisgera_api'';
DECLARE @Criados int;

INSERT INTO dbo.T009USUA (NM_USU, PWD_WEB, FLG_USER_WEB, NR_CPF, FLG_USER_STATUS, CD_EMP)
SELECT @Login, NULL, ''N'', NULL, ''1'', e.CD_EMP
  FROM dbo.T009EMPS e
 WHERE NOT EXISTS (SELECT 1 FROM dbo.T009USUA u
                    WHERE u.CD_EMP = e.CD_EMP
                      AND RTRIM(LTRIM(u.NM_USU)) = @Login);

SET @Criados = @@ROWCOUNT;

PRINT N''' + @Banco + N': usuário da API cadastrado em '' + CONVERT(nvarchar(10), @Criados) + N'' empresa(s).'';

SELECT ''' + @Banco + N''' AS banco, CD_EMP, CD_USU, NM_USU, FLG_USER_WEB, FLG_USER_STATUS
  FROM dbo.T009USUA
 WHERE RTRIM(LTRIM(NM_USU)) = @Login;';

        EXEC (@cmd);
    END

    FETCH NEXT FROM cur INTO @Banco;
END

CLOSE cur;
DEALLOCATE cur;
GO

/*
  Conferência (em cada base):

  1) O trigger acha o usuário? Rode conectado COMO usr_sisgera_api (ou use
     EXECUTE AS LOGIN = 'usr_sisgera_api' com sa) e veja se volta um CD_USU:

     SELECT CD_USU, NM_USU FROM dbo.T009USUA
      WHERE RTRIM(LTRIM(NM_USU)) = RTRIM(LTRIM(SYSTEM_USER));

  2) Espaço no código: CD_USU é numeric(3) (máximo 999).

     SELECT MAX(CD_USU) AS maior_codigo FROM dbo.T009USUA;

  3) Depois de incluir um contrato pela API, o log do legado:

     SELECT TOP 10 l.*, u.NM_USU
       FROM dbo.T009LCTR l
       JOIN dbo.T009USUA u ON u.CD_EMP = l.CD_EMP AND u.CD_USU = l.CD_USU
      ORDER BY l.DT_OPE DESC;

  Para desfazer (só se o usuário ainda não tiver log em T009LCTR, por causa da FK):

  DELETE FROM dbo.T009USUA WHERE RTRIM(LTRIM(NM_USU)) = 'usr_sisgera_api';
*/
