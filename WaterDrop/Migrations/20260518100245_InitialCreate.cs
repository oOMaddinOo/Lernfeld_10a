using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterDrop.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Baseline migration: the DatabaseKloModel table already exists in the production database
            // (created before EF Core migrations were introduced to this project). We deliberately leave
            // Up() and Down() empty so that running `dotnet ef database update` only registers this
            // migration in __EFMigrationsHistory without attempting to recreate or drop the live table.
            // Future schema changes (e.g. AddCreatedAt) will produce real migrations on top of this.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — see comment in Up().
        }
    }
}
