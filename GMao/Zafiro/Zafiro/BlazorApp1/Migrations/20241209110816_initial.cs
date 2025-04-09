using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ZafiroGmao.Migrations
{
    /// <inheritdoc />
    public partial class initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActionRecordElements",
                columns: table => new
                {
                    Guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionRecordOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    recordStatusInternal = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionRecordElements", x => x.Guid);
                });

            migrationBuilder.CreateTable(
                name: "ActionRecordOperations",
                columns: table => new
                {
                    Guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainPartId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ServiceParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Operation = table.Column<byte>(type: "tinyint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionRecordOperations", x => x.Guid);
                });

            migrationBuilder.CreateTable(
                name: "ActionRecords",
                columns: table => new
                {
                    Guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Campaign = table.Column<bool>(type: "bit", nullable: false),
                    Corrective = table.Column<bool>(type: "bit", nullable: false),
                    Terminated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActionRecords", x => x.Guid);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CF = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TelegramId = table.Column<long>(type: "bigint", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatusChanges",
                columns: table => new
                {
                    Guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TrainId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TimeStamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    mvarOperationId = table.Column<byte>(type: "tinyint", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatusChanges", x => x.Guid);
                });

            migrationBuilder.CreateTable(
                name: "Trains",
                columns: table => new
                {
                    Guid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GmaoId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameCloud = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    lastChange = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trains", x => x.Guid);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Trains",
                columns: new[] { "Guid", "Comment", "GmaoId", "LastStatus", "Name", "NameCloud", "lastChange" },
                values: new object[,]
                {
                    { new Guid("01fc72c2-bdf5-433b-8137-7b7888675133"), "", 0, (byte)0, "7109-7110", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0c531df0-c72e-421a-bc55-35b3ba8b375f"), "", 0, (byte)0, "8109-8110", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("0c937da8-75ac-4c67-80a5-43193fdde141"), "", 0, (byte)0, "8101-8102", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("10b390f5-04d8-4df5-8ddb-807eefc22006"), "", 0, (byte)0, "9105-9106", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("244c09a6-3196-4444-8686-6bc5fad6eed0"), "", 0, (byte)0, "7103-7104", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("26794ede-f53b-4acf-8443-1dfa856aaba8"), "", 0, (byte)0, "1103-1104", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("2d804345-4872-4f77-90d3-9519a05b5e49"), "", 0, (byte)0, "1109-1110", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3172ea3f-8eac-4ee7-9efd-8558f5edaac4"), "", 0, (byte)0, "1101-1102", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("38672cc7-a7db-4fc1-ba4b-c42e7d8a4790"), "", 0, (byte)0, "9109-9110", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3a1e215d-1c4f-4824-b5db-3f57d8fda2c8"), "", 0, (byte)0, "8117-8118", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("3aa8813c-b932-4f04-a561-bc9bbd0177d0"), "", 0, (byte)0, "9103-9104", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("44baaa40-2506-4fdb-97d1-0257ddd7072e"), "", 0, (byte)0, "8103-8104", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("514cc523-2e54-41c9-aff8-191f6bc9c5e6"), "", 0, (byte)0, "8125-8126", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("515dd247-271d-4c3e-aabb-32192cfe7812"), "", 0, (byte)0, "1105-1106", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("52bd59cd-ce9b-49f4-bad9-e0326ed8dbf8"), "", 0, (byte)0, "8105-8106", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("61ba1f9a-d200-444d-b333-b579b5509b54"), "", 0, (byte)0, "1107-1108", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("87050346-775d-4293-95bd-61393dff8354"), "", 0, (byte)0, "8123-8124", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8d9fd4fa-c955-47a6-8a2f-50fc507752aa"), "", 0, (byte)0, "7101-7102", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8f11e9f9-8e49-46ac-8e1e-de0c30559326"), "", 0, (byte)0, "9107-9108", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("8f9e5054-e51b-4f01-bb49-c2269e5e2f95"), "", 0, (byte)0, "8113-8114", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9454da68-34ef-488f-a8ab-78fb210ea32f"), "", 0, (byte)0, "7111-7112", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("9939f087-f0f4-43e4-96ba-7761db8fd16f"), "", 0, (byte)0, "7107-7108", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("99e7f464-7ef4-4cfa-98d9-40315f57f54d"), "", 0, (byte)0, "8107-8108", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a38abd87-d91c-4cc2-8239-c7c9adaea689"), "", 0, (byte)0, "7105-7106", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a57b5084-6f91-43f6-9af3-3ba7c52faf76"), "", 0, (byte)0, "8121-8122", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("a7f27f7e-632b-41b0-b28e-d23bc3308967"), "", 0, (byte)0, "8115-8116", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("af659352-4a8e-4273-b34f-39e7c11b68f9"), "", 0, (byte)0, "8111-8112", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("c7f59ddb-6df5-48c9-952b-d89af5088b7f"), "", 0, (byte)0, "9111-9112", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("d0e1f33a-71d2-4571-86fb-b1a08db741ba"), "", 0, (byte)0, "8119-8120", "", new Guid("00000000-0000-0000-0000-000000000000") },
                    { new Guid("fc3c11c2-16c7-4c79-b28f-aff615050076"), "", 0, (byte)0, "9101-9102", "", new Guid("00000000-0000-0000-0000-000000000000") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActionRecordElements");

            migrationBuilder.DropTable(
                name: "ActionRecordOperations");

            migrationBuilder.DropTable(
                name: "ActionRecords");

            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "StatusChanges");

            migrationBuilder.DropTable(
                name: "Trains");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");
        }
    }
}
