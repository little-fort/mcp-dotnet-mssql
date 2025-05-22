using CsvHelper;
using Dapper;
using Microsoft.Data.SqlClient;
using ModelContextProtocol.Server;
using System;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;

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
    private static readonly string[] _confirmArr = ["true", "1", "yes", "y"];
    private static bool AllowMultiDb => _confirmArr.Contains(Environment.GetEnvironmentVariable("DB_ALLOW_MULTI")?.ToLower());
    private static bool AllowWrite => _confirmArr.Contains(Environment.GetEnvironmentVariable("DB_ALLOW_WRITE")?.ToLower());

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

        if (AllowMultiDb && database != null && database != Environment.GetEnvironmentVariable("DB_INITIAL_CATALOG"))
            conn.ChangeDatabase(database);

        var tables = await conn.QueryAsync<string>("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'");

        return JsonSerializer.Serialize(tables);
    }

    [McpServerTool, Description("Get the list of columns in a table and their data types in CSV format.")]
    public static async Task<string> GetTableColumns([Description("The name of the table to get column information for.")] string table, [Description("(Optional) The name of the database to get table information for. Ignored if DB_ALLOW_MULTI is not set to 'true'.")] string? database = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        if (AllowMultiDb && database != null && database != Environment.GetEnvironmentVariable("DB_INITIAL_CATALOG"))
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
    public static async Task<string> ExecuteSelect([Description("The raw SQL query that should be executed.")] string sql, [Description("The name of the database to perform the SQL operation in.")] string? database = null)
    {
        using var conn = CreateConnection();
        await conn.OpenAsync();

        // Enforce only a single SELECT statement
        var trimmedSql = sql.Trim();
        if (!trimmedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Only SELECT queries are supported in this method.");
        if (trimmedSql.Contains(';'))
            throw new Exception("Multiple SQL statements in a single request are not allowed.");

        if (AllowMultiDb && ValidateDbTarget(trimmedSql, database))
            conn.ChangeDatabase(database);

        var result = await conn.QueryAsync(trimmedSql);

        // Convert the result to CSV format for improved processing
        var csv = new StringWriter();
        using var writer = new CsvWriter(csv, CultureInfo.InvariantCulture);
        writer.WriteRecords(result);

        return csv.ToString();
    }

    [McpServerTool, Description("Execute a non-SELECT SQL query and return the number of affected rows.")]
    public static async Task<string> ExecuteNonSelect([Description("The raw SQL query that should be executed.")] string sql, [Description("The name of the database to perform the SQL operation in.")] string? database = null)
    {
        if (!AllowWrite)
            throw new Exception("Method not allowed in current configuration. Set DB_ALLOW_WRITE to 'true' to allow this method.");

        using var conn = CreateConnection();
        await conn.OpenAsync();
        
        // Enforce only a single non-SELECT statement
        var trimmedSql = sql.Trim();
        if (trimmedSql.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Only non-SELECT queries are supported in this method.");
        if (trimmedSql.Contains(';'))
            throw new Exception("Multiple SQL statements are not allowed.");

        if (AllowMultiDb && ValidateDbTarget(trimmedSql, database))
            conn.ChangeDatabase(database);

        // For non-SELECT queries, return the number of affected rows
        var affectedRows = await conn.ExecuteAsync(trimmedSql);
        return JsonSerializer.Serialize(new { affectedRows });
    }

    private static bool ValidateDbTarget(string sql, string? database)
    {
        if (AllowMultiDb || string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(database))
            return true;

        var initialCatalog = Environment.GetEnvironmentVariable("DB_INITIAL_CATALOG");

        // Extract the portion after FROM and before WHERE
        var afterFrom = sql.Substring(sql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase) + 4);
        var tables = afterFrom;

        // Trim off any trailing SQL clauses
        var keywords = new[] { "WHERE", "GROUP BY", "ORDER BY", "HAVING", "LIMIT", "OFFSET" };
        int cutIndex = -1;
        foreach (var clause in keywords)
        {
            var idx = tables.IndexOf(clause, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (cutIndex < 0 || idx < cutIndex))
                cutIndex = idx;
        }
        if (cutIndex >= 0)
            tables = tables.Substring(0, cutIndex);

        // Split into tokens by whitespace and commas
        var tokens = tables.Split([ ' ', '\t', '\r', '\n', ',' ], StringSplitOptions.RemoveEmptyEntries);
        foreach (var token in tokens)
        {
            var clean = token.Replace("[", "").Replace("]", "").Trim();
            var parts = clean.Split('.');
            if (parts.Length > 2 && !parts[0].Equals(initialCatalog, StringComparison.OrdinalIgnoreCase))
                throw new Exception($"SQL statement targets database '{parts[0]}', which is not allowed. Only '{initialCatalog}' is permitted unless DB_ALLOW_MULTI is enabled.");
        }

        return true;
    }
}