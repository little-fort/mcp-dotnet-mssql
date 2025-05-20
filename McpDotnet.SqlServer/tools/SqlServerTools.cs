using CsvHelper;
using Dapper;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace McpDotnet.SqlServer;

[McpServerToolType]
public static class SqlServerTools
{
    // Build connection string from environment variables
    private static string ConnectionString => new SqlConnectionStringBuilder
    {
        DataSource = Environment.GetEnvironmentVariable("DB_DATASOURCE"),
        InitialCatalog = Environment.GetEnvironmentVariable("DB_INITIAL_CATALOG"),
        UserID = Environment.GetEnvironmentVariable("DB_USER"),
        Password = Environment.GetEnvironmentVariable("DB_PASSWORD"),
        TrustServerCertificate = true
    }.ConnectionString;

    // This is a flag to allow the agent to switch between other databases on the server
    private static bool AllowMultiDb => (new string[] { "true", "1", "yes", "y" }).Contains(Environment.GetEnvironmentVariable("DB_ALLOW_MULTI")?.ToLower());

    private static SqlConnection CreateConnection() => new SqlConnection(ConnectionString);

    [McpServerTool, Description("Get the list of available databases on the server.")]
    public static async Task<string> GetDatabases()
    {
        if (!AllowMultiDb)
            throw new Exception("Method not allowed in current configuration. Set DB_ALLOW_MULTI to 'true' to allow this method.");

        using var conn = CreateConnection();
        await conn.OpenAsync();
        var databases = await conn.QueryAsync<string>("SELECT name FROM sys.databases");

        return JsonSerializer.Serialize(databases);
    }

    [McpServerTool, Description("Get the list of tables in a database.")]
    public static async Task<string> GetDatabaseTables([Description("(Optional) The name of the database to get table information for. Ignored if DB_ALLOW_MULTI is not set to 'true'.")] string? database = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        if (AllowMultiDb && database != null)
            conn.ChangeDatabase(database);

        var tables = await conn.QueryAsync<string>("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");

        return JsonSerializer.Serialize(tables);
    }

    [McpServerTool, Description("Get the list of columns in a table and their data types in CSV format.")]
    public static async Task<string> GetTableColumns([Description("The name of the table to get column information for.")] string table, [Description("(Optional) The name of the database to get table information for. Ignored if DB_ALLOW_MULTI is not set to 'true'.")] string? database = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        if (AllowMultiDb && database != null)
            conn.ChangeDatabase(database);

        // Check if table exists
        var exists = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @table", new { table });
        if (exists == 0)
            throw new Exception($"Table '{table}' does not exist.");

        // Get the columns and their data types
        var columns = await conn.QueryAsync("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @table", new { table });

        // Convert the result to CSV format
        var csv = new StringWriter();
        using (var writer = new CsvWriter(csv, CultureInfo.InvariantCulture))
        {
            writer.WriteRecords(columns);
        }

        return csv.ToString();
    }

    [McpServerTool, Description("Execute a SELECT SQL query and return the result in CSV format.")]
    public static async Task<string> ExecuteSelect([Description("The name of the database to perform the SQL operation in.")] string database, [Description("The raw SQL query that should be executed.")] string sql)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        conn.ChangeDatabase(database);

        if (!sql.StartsWith("SELECT"))
            throw new Exception("Only SELECT queries are supported in this method.");

        var result = await conn.QueryAsync(sql);

        // Convert the result to CSV format for improved processing
        var csv = new StringWriter();
        using var writer = new CsvWriter(csv, CultureInfo.InvariantCulture);
        writer.WriteRecords(result);

        return csv.ToString();
    }

    [McpServerTool, Description("Execute a non-SELECT SQL query and return the number of affected rows.")]
    public static async Task<string> ExecuteNonSelect([Description("The name of the database to perform the SQL operation in.")] string database, [Description("The raw SQL query that should be executed.")] string sql)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();
        conn.ChangeDatabase(database);

        if (sql.StartsWith("SELECT"))
            throw new Exception("Only non-SELECT queries are supported in this method.");

        // For non-SELECT queries, return the number of affected rows
        var affectedRows = await conn.ExecuteAsync(sql);
        return JsonSerializer.Serialize(new { affectedRows });
    }
}