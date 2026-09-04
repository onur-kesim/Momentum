using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Momentum.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // IS-EMRI-o86-A2 §A: EF varligi olarak ESLENMEZ (ham SQL ile okunur) -- DbSet/konfigurasyon
    // eklenmez, ModelSnapshot bu gorunumu YONETMEZ (DispatcherIndexes'in raw-SQL-yalniz deseni).
    // is_deleted SUZULMEZ: silinmis projenin uyesi silinme olayini cekebilmeli. Sahip project_members'e
    // YAZILMAZ (§C3 kilidi duruyor) -- gorunum tam bu yuzden UNION'dur, fiziksel satir degil.
    public partial class AddProjectAccessView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "CREATE VIEW project_access AS " +
                "SELECT entity_id AS project_id, owner_id AS user_id FROM projects " +
                "UNION " +
                "SELECT project_id,             user_id           FROM project_members;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP VIEW project_access;");
        }
    }
}
