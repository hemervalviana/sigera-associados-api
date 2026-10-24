using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SigeraAssociados.Infrastructure.Persistence.Identity.Migrations
{
    /// <inheritdoc />
    public partial class TokensRenovacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TokensRenovacao",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UsuarioId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TokenHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: false),
                    CriadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiraEmUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevogadoEmUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MotivoRevogacao = table.Column<string>(type: "varchar(30)", unicode: false, maxLength: 30, nullable: true),
                    SubstituidoPorHash = table.Column<string>(type: "varchar(64)", unicode: false, maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TokensRenovacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TokensRenovacao_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TokensRenovacao_Usuario_Ativos",
                table: "TokensRenovacao",
                columns: new[] { "UsuarioId", "RevogadoEmUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_TokensRenovacao_TokenHash",
                table: "TokensRenovacao",
                column: "TokenHash",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TokensRenovacao");
        }
    }
}
