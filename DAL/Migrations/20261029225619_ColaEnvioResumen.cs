using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DAL.Migrations
{
    public partial class ColaEnvioResumen : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EjecucionProceso",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    Codigo = table.Column<string>(maxLength: 50, nullable: true),
                    PeriodoId = table.Column<int>(nullable: true),
                    FechaInicio = table.Column<DateTime>(nullable: false),
                    FechaFin = table.Column<DateTime>(nullable: true),
                    Estado = table.Column<string>(maxLength: 30, nullable: true),
                    Total = table.Column<int>(nullable: false),
                    Exitosos = table.Column<int>(nullable: false),
                    Fallidos = table.Column<int>(nullable: false),
                    Omitidos = table.Column<int>(nullable: false),
                    ErrorGeneral = table.Column<string>(nullable: true),
                    Servidor = table.Column<string>(maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EjecucionProceso", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EjecucionProceso_Periodo_PeriodoId",
                        column: x => x.PeriodoId,
                        principalTable: "Periodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnvioResumenMail",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    ResumenTarjetaId = table.Column<int>(nullable: false),
                    PeriodoId = table.Column<int>(nullable: false),
                    UsuarioId = table.Column<string>(maxLength: 450, nullable: true),
                    EmailDestino = table.Column<string>(maxLength: 256, nullable: true),
                    EmailCliente = table.Column<string>(maxLength: 256, nullable: true),
                    EsPrueba = table.Column<bool>(nullable: false),
                    Estado = table.Column<string>(maxLength: 20, nullable: true),
                    MotivoOmision = table.Column<string>(maxLength: 50, nullable: true),
                    Proveedor = table.Column<string>(maxLength: 30, nullable: true),
                    ProveedorMessageId = table.Column<string>(maxLength: 200, nullable: true),
                    HttpStatus = table.Column<int>(nullable: true),
                    CodigoErrorProveedor = table.Column<string>(maxLength: 100, nullable: true),
                    Error = table.Column<string>(nullable: true),
                    Intentos = table.Column<int>(nullable: false),
                    ProximoIntento = table.Column<DateTime>(nullable: true),
                    FechaUltimoIntento = table.Column<DateTime>(nullable: true),
                    FechaCreacion = table.Column<DateTime>(nullable: false),
                    FechaEnvio = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvioResumenMail", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvioResumenMail_Periodo_PeriodoId",
                        column: x => x.PeriodoId,
                        principalTable: "Periodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnvioResumenMail_ResumenTarjeta_ResumenTarjetaId",
                        column: x => x.ResumenTarjetaId,
                        principalTable: "ResumenTarjeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EnvioResumenMail_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResumenGeneracion",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    PeriodoId = table.Column<int>(nullable: false),
                    UsuarioId = table.Column<string>(maxLength: 450, nullable: true),
                    EjecucionProcesoId = table.Column<int>(nullable: true),
                    Estado = table.Column<string>(maxLength: 20, nullable: true),
                    CodigoError = table.Column<string>(maxLength: 50, nullable: true),
                    Mensaje = table.Column<string>(nullable: true),
                    Detalle = table.Column<string>(nullable: true),
                    Intentos = table.Column<int>(nullable: false),
                    ResumenTarjetaId = table.Column<int>(nullable: true),
                    FechaCreacion = table.Column<DateTime>(nullable: false),
                    FechaUltimoIntento = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResumenGeneracion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResumenGeneracion_EjecucionProceso_EjecucionProcesoId",
                        column: x => x.EjecucionProcesoId,
                        principalTable: "EjecucionProceso",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResumenGeneracion_Periodo_PeriodoId",
                        column: x => x.PeriodoId,
                        principalTable: "Periodo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResumenGeneracion_ResumenTarjeta_ResumenTarjetaId",
                        column: x => x.ResumenTarjetaId,
                        principalTable: "ResumenTarjeta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResumenGeneracion_AspNetUsers_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EnvioResumenMailIntento",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("SqlServer:ValueGenerationStrategy", SqlServerValueGenerationStrategy.IdentityColumn),
                    EnvioResumenMailId = table.Column<int>(nullable: false),
                    NroIntento = table.Column<int>(nullable: false),
                    Fecha = table.Column<DateTime>(nullable: false),
                    DuracionMs = table.Column<long>(nullable: false),
                    EmailDestino = table.Column<string>(maxLength: 256, nullable: true),
                    Proveedor = table.Column<string>(maxLength: 30, nullable: true),
                    Exito = table.Column<bool>(nullable: false),
                    HttpStatus = table.Column<int>(nullable: true),
                    CodigoErrorProveedor = table.Column<string>(maxLength: 100, nullable: true),
                    Error = table.Column<string>(nullable: true),
                    ProveedorMessageId = table.Column<string>(maxLength: 200, nullable: true),
                    IdempotencyKey = table.Column<string>(maxLength: 300, nullable: true),
                    Reintentable = table.Column<bool>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnvioResumenMailIntento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnvioResumenMailIntento_EnvioResumenMail_EnvioResumenMailId",
                        column: x => x.EnvioResumenMailId,
                        principalTable: "EnvioResumenMail",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionProceso_PeriodoId",
                table: "EjecucionProceso",
                column: "PeriodoId");

            migrationBuilder.CreateIndex(
                name: "IX_EjecucionProceso_Codigo_PeriodoId",
                table: "EjecucionProceso",
                columns: new[] { "Codigo", "PeriodoId" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvioResumenMail_PeriodoId",
                table: "EnvioResumenMail",
                column: "PeriodoId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvioResumenMail_ResumenTarjetaId",
                table: "EnvioResumenMail",
                column: "ResumenTarjetaId",
                unique: true,
                filter: "[EsPrueba] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_EnvioResumenMail_UsuarioId",
                table: "EnvioResumenMail",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvioResumenMail_Estado_ProximoIntento",
                table: "EnvioResumenMail",
                columns: new[] { "Estado", "ProximoIntento" });

            migrationBuilder.CreateIndex(
                name: "IX_EnvioResumenMailIntento_EnvioResumenMailId",
                table: "EnvioResumenMailIntento",
                column: "EnvioResumenMailId");

            migrationBuilder.CreateIndex(
                name: "IX_EnvioResumenMailIntento_Fecha",
                table: "EnvioResumenMailIntento",
                column: "Fecha");

            migrationBuilder.CreateIndex(
                name: "IX_ResumenGeneracion_EjecucionProcesoId",
                table: "ResumenGeneracion",
                column: "EjecucionProcesoId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumenGeneracion_ResumenTarjetaId",
                table: "ResumenGeneracion",
                column: "ResumenTarjetaId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumenGeneracion_UsuarioId",
                table: "ResumenGeneracion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_ResumenGeneracion_PeriodoId_UsuarioId",
                table: "ResumenGeneracion",
                columns: new[] { "PeriodoId", "UsuarioId" },
                unique: true,
                filter: "[UsuarioId] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EnvioResumenMailIntento");

            migrationBuilder.DropTable(
                name: "ResumenGeneracion");

            migrationBuilder.DropTable(
                name: "EnvioResumenMail");

            migrationBuilder.DropTable(
                name: "EjecucionProceso");
        }
    }
}
