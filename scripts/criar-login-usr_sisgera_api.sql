/*
  Cria o login usr_sisgera_api usado pela SigeraAssociados API
  no lugar do sa.

  Permissões:
  - PROASP_SIG, SONASP_SIG, ABSP_SIG e GSP_SIG: somente leitura (db_datareader).
  - Banco de identidade: leitura e escrita (usuários, grupos, tokens e acessos).
    Ajuste @BancoIdentidade se o nome não for SigeraIdentity.

  Execute conectado como sa (ou outro sysadmin) no SQL Server da API.
  Troque a senha antes de executar e grave a mesma senha nos User Secrets.
*/

USE master;
GO

DECLARE @Senha nvarchar(128) = N'TROQUE_POR_UMA_SENHA_FORTE';

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'usr_sisgera_api')
BEGIN
    DECLARE @sql nvarchar(max) =
        N'CREATE LOGIN [usr_sisgera_api] WITH PASSWORD = ' + QUOTENAME(@Senha, '''') +
        N', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF, DEFAULT_DATABASE = [master];';
    EXEC (@sql);
    PRINT N'Login usr_sisgera_api criado.';
END
ELSE
    PRINT N'Login usr_sisgera_api já existe.';
GO

/* Bancos de negócio: somente leitura. */
DECLARE @Bancos TABLE (Nome sysname);
INSERT INTO @Bancos VALUES (N'PROASP_SIG'), (N'SONASP_SIG'), (N'ABSP_SIG'), (N'GSP_SIG');

DECLARE @Banco sysname, @cmd nvarchar(max);
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Nome FROM @Bancos;
OPEN cur;
FETCH NEXT FROM cur INTO @Banco;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF DB_ID(@Banco) IS NULL
        PRINT N'Banco não encontrado: ' + @Banco;
    ELSE
    BEGIN
        SET @cmd = N'USE ' + QUOTENAME(@Banco) + N';
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N''usr_sisgera_api'')
    CREATE USER [usr_sisgera_api] FOR LOGIN [usr_sisgera_api];
ALTER ROLE db_datareader ADD MEMBER [usr_sisgera_api];';
        EXEC (@cmd);
        PRINT N'Leitura concedida em ' + @Banco;
    END
    FETCH NEXT FROM cur INTO @Banco;
END
CLOSE cur;
DEALLOCATE cur;
GO

/* Banco de identidade: leitura e escrita.
   db_ddladmin só é necessário para aplicar migrations com este login;
   remova essa linha se as migrations forem aplicadas com outro usuário. */
DECLARE @BancoIdentidade sysname = N'SigeraIdentity';
DECLARE @cmd nvarchar(max);

IF DB_ID(@BancoIdentidade) IS NULL
    PRINT N'Banco de identidade não encontrado: ' + @BancoIdentidade;
ELSE
BEGIN
    SET @cmd = N'USE ' + QUOTENAME(@BancoIdentidade) + N';
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N''usr_sisgera_api'')
    CREATE USER [usr_sisgera_api] FOR LOGIN [usr_sisgera_api];
ALTER ROLE db_datareader ADD MEMBER [usr_sisgera_api];
ALTER ROLE db_datawriter ADD MEMBER [usr_sisgera_api];
ALTER ROLE db_ddladmin ADD MEMBER [usr_sisgera_api];';
    EXEC (@cmd);
    PRINT N'Leitura e escrita concedidas em ' + @BancoIdentidade;
END
GO
