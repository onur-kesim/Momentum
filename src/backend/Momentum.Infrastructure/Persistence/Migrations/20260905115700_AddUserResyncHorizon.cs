using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Momentum.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserResyncHorizon : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_resync_horizon",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    horizon_seq = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_resync_horizon", x => x.user_id);
                });

            // IS-EMRI-o86-D D1: sync_gc_state.gc_horizon_xid'in BIREBIR deseni -- xid8'in EF/bigint
            // cast'i yok, RAW SQL ile eklenir (InitialSync migration'indaki ayni satirin ayni gerekcesi).
            migrationBuilder.Sql("ALTER TABLE user_resync_horizon ADD COLUMN horizon_xid xid8 NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_resync_horizon");
        }
    }
}
