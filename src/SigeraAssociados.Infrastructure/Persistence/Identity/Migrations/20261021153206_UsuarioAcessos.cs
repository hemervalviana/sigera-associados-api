using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigeraAssociados.Infrastructure.Persistence.Identity.Migrations
{
    /// <inheritdoc />
    public partial class UsuarioAcessos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UsuarioAcessos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Sistema = table.Column<int>(type: "int", nullable: false),
                    CdEmp = table.Column<decimal>(type: "decimal(2,0)", precision: 2, scale: 0, nullable: true),
                    CdFil = table.Column<decimal>(type: "decimal(2,0)", precision: 2, scale: 0, nullable: true),
                    SqPesCorretor = table.Column<decimal>(type: "decimal(7,0)", precision: 7, scale: 0, nullable: true),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UsuarioAcessos", x => x.Id);
                    table.CheckConstraint("CK_UsuarioAcessos_CorretorExigeEmpresa", "[SqPesCorretor] IS NULL OR [CdEmp] IS NOT NULL");
                    table.CheckConstraint("CK_UsuarioAcessos_FilialExigeEmpresa", "[CdFil] IS NULL OR [CdEmp] IS NOT NULL");
                    table.CheckConstraint("CK_UsuarioAcessos_Sistema", "[Sistema] BETWEEN 1 AND 4");
                    table.ForeignKey(
                        name: "FK_UsuarioAcessos_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UX_UsuarioAcessos_Escopo",
                table: "UsuarioAcessos",
                columns: new[] { "UsuarioId", "Sistema", "CdEmp", "CdFil", "SqPesCorretor" },
                unique: true,
                filter: "[CdEmp] IS NOT NULL AND [CdFil] IS NOT NULL AND [SqPesCorretor] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UsuarioAcessos");
        }
    }
}
