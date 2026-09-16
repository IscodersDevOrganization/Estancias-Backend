using Microsoft.EntityFrameworkCore.Migrations;

namespace DAL.Migrations
{
    public partial class addcolumnCodigoCategoriaUsuario : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "PreRegistroCategorias");

            migrationBuilder.AddColumn<string>(
                name: "Codigo",
                table: "UsuariosCategorias",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CategoriaId",
                table: "PreRegistroCategorias",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreRegistroCategorias_CategoriaId",
                table: "PreRegistroCategorias",
                column: "CategoriaId");

            migrationBuilder.AddForeignKey(
                name: "FK_PreRegistroCategorias_UsuariosCategorias_CategoriaId",
                table: "PreRegistroCategorias",
                column: "CategoriaId",
                principalTable: "UsuariosCategorias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PreRegistroCategorias_UsuariosCategorias_CategoriaId",
                table: "PreRegistroCategorias");

            migrationBuilder.DropIndex(
                name: "IX_PreRegistroCategorias_CategoriaId",
                table: "PreRegistroCategorias");

            migrationBuilder.DropColumn(
                name: "Codigo",
                table: "UsuariosCategorias");

            migrationBuilder.DropColumn(
                name: "CategoriaId",
                table: "PreRegistroCategorias");

            migrationBuilder.AddColumn<string>(
                name: "Categoria",
                table: "PreRegistroCategorias",
                nullable: true);
        }
    }
}
