using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WaterDrop.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: a teammate may have already added this column manually
            // before EF Core migrations were introduced. Only add it if it is missing.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[dbo].[DatabaseKloModel]')
                      AND name = N'CreatedAt'
                )
                BEGIN
                    ALTER TABLE [DatabaseKloModel] ADD [CreatedAt] datetime2 NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Idempotent: only drop the column if it actually exists.
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1
                    FROM sys.columns
                    WHERE object_id = OBJECT_ID(N'[dbo].[DatabaseKloModel]')
                      AND name = N'CreatedAt'
                )
                BEGIN
                    ALTER TABLE [DatabaseKloModel] DROP COLUMN [CreatedAt];
                END
            ");
        }
    }
}
