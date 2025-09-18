using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CveWebApp.Data
{
    /// <summary>
    /// Handles database initialization and schema updates for production environments
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Ensures the database exists and has the required schema, including missing columns
        /// </summary>
        public static async Task InitializeDatabaseAsync(ApplicationDbContext context)
        {
            if (context.Database.IsInMemory())
            {
                // For in-memory database, just ensure it's created
                await context.Database.EnsureCreatedAsync();
                return;
            }

            // For SQL Server, handle schema initialization carefully
            var canConnect = await context.Database.CanConnectAsync();
            if (!canConnect)
            {
                // Database doesn't exist - create it
                await context.Database.EnsureCreatedAsync();
                return;
            }

            // Database exists - check if it has tables
            var hasSchema = await context.Database.GetService<IRelationalDatabaseCreator>().HasTablesAsync();
            if (!hasSchema)
            {
                // Database exists but is empty - create schema
                await context.Database.EnsureCreatedAsync();
                return;
            }

            // Database exists with tables - check for missing Active Directory columns
            await EnsureActiveDirectoryColumnsExistAsync(context);
        }

        /// <summary>
        /// Ensures the Active Directory columns exist in the AspNetUsers table
        /// </summary>
        private static async Task EnsureActiveDirectoryColumnsExistAsync(ApplicationDbContext context)
        {
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            
            try
            {
                // Check if IsActiveDirectoryUser column exists
                var isActiveDirectoryUserExists = await ColumnExistsAsync(connection, "AspNetUsers", "IsActiveDirectoryUser");
                if (!isActiveDirectoryUserExists)
                {
                    await ExecuteSqlAsync(connection, 
                        "ALTER TABLE [AspNetUsers] ADD [IsActiveDirectoryUser] bit NOT NULL DEFAULT 0");
                }

                // Check if ActiveDirectoryDn column exists
                var activeDirectoryDnExists = await ColumnExistsAsync(connection, "AspNetUsers", "ActiveDirectoryDn");
                if (!activeDirectoryDnExists)
                {
                    await ExecuteSqlAsync(connection, 
                        "ALTER TABLE [AspNetUsers] ADD [ActiveDirectoryDn] nvarchar(max) NULL");
                }
            }
            finally
            {
                await connection.CloseAsync();
            }
        }

        /// <summary>
        /// Checks if a column exists in a table
        /// </summary>
        private static async Task<bool> ColumnExistsAsync(System.Data.Common.DbConnection connection, string tableName, string columnName)
        {
            var sql = @"
                SELECT COUNT(*)
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @tableName 
                AND COLUMN_NAME = @columnName";

            using var command = connection.CreateCommand();
            command.CommandText = sql;
            
            var tableParam = command.CreateParameter();
            tableParam.ParameterName = "@tableName";
            tableParam.Value = tableName;
            command.Parameters.Add(tableParam);
            
            var columnParam = command.CreateParameter();
            columnParam.ParameterName = "@columnName";
            columnParam.Value = columnName;
            command.Parameters.Add(columnParam);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }

        /// <summary>
        /// Executes a SQL command
        /// </summary>
        private static async Task ExecuteSqlAsync(System.Data.Common.DbConnection connection, string sql)
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }
}