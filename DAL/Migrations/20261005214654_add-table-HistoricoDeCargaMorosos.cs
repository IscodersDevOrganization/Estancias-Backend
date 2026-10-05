using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DAL.Migrations
{
    public partial class addtableHistoricoDeCargaMorosos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HistoricoDeCargaMorosos",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    RegistrosTotales = table.Column<int>(nullable: false),
                    Cargados = table.Column<int>(nullable: false),
                    Fallidos = table.Column<int>(nullable: false),
                    Omitidos = table.Column<int>(nullable: false),
                    Observacion = table.Column<string>(nullable: true),
                    UsuarioId = table.Column<string>(nullable: true),
                    FechaCarga = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoricoDeCargaMorosos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoricoDeCargaMorosos_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HistoricoDeCargaMorosos_UsuarioId",
                table: "HistoricoDeCargaMorosos",
                column: "UsuarioId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoricoDeCargaMorosos");
        }
    }
}
