# .NET MCP Server for Microsoft SQL Server

**mcp-dotnet-mssql** is a Model Context Protocol (MCP) server implementation for Microsoft SQL Server, built on .NET&nbsp;9. It enables clients to interact with SQL Server databases via the MCP JSON-RPC API, providing schema introspection, query execution, and data streaming capabilities for agentic LLM tools.

## Features

- Support for direct connections to Microsoft SQL Server
- Dynamic schema and metadata introspection
- CSV export of query results for improved performance over JSON
- Configurable permissions for write access and database visibility

## Usage

### Release Package

Begin by downloading the [latest version](https://github.com/little-fort/mcp-dotnet-mssql/releases) from the Releases page and extract the archive into a directory of your choice.

Connect any MCP-compatible client to the server binary like so:

```json
"mcp-dotnet-mssql": {
    "type": "stdio",
    "command": "C:/download/mcp-dotnet-mssql-win-x64/mcp-dotnet-mssql.exe",
    "env": {
        "DB_DATASOURCE": "localhost",
        "DB_INITIAL_CATALOG": "MyDatabase",
        "DB_USER": "user",
        "DB_PASSWORD": "password",
        "DB_ALLOW_MULTI": "false",
        "DB_ALLOW_WRITE": "false"
    }
}
```

Use the client to send MCP requests for schema listing, query execution, and data retrieval.

### Source

Begin by downloading the source [directly](https://github.com/little-fort/mcp-dotnet-mssql/archive/refs/heads/main.zip), or by using git to clone the repository:

```powershell
# Clone the repository
git clone https://github.com/littlefort/mcp-dotnet-mssql.git
cd mcp-dotnet-mssql/src/mcp-dotnet-mssql

# Restore dependencies and build
dotnet restore
dotnet build --configuration Release
```

You can then connect your MCP-compatible client to the project like so:

```json
"mcp-dotnet-mssql": {
    "type": "stdio",
    "command": "dotnet",
    "args": [
        "run",
        "--project",
        "C:\\download\\mcp-dotnet-mssql\\src\\mcp-dotnet-mssql\\mcp-dotnet-mssql.csproj"
    ],
    "env": {
        "DB_DATASOURCE": "localhost",
        "DB_INITIAL_CATALOG": "MyDatabase",
        "DB_USER": "user",
        "DB_PASSWORD": "password",
        "DB_ALLOW_MULTI": "false",
        "DB_ALLOW_WRITE": "false"
    }
}
```

### Docker

The server is also available as a [Docker image](https://hub.docker.com/r/littlefort/mcp-dotnet-mssql):

```json
"mcp-dotnet-mssql": {
    "command": "docker",
    "args": [
        "run",
        "-i",
        "--rm",
        "littlefort/mcp-dotnet-mssql"
    ],
    "env": {
        "DB_DATASOURCE": "localhost",
        "DB_INITIAL_CATALOG": "MyDatabase",
        "DB_USER": "user",
        "DB_PASSWORD": "password",
        "DB_ALLOW_MULTI": "false",
        "DB_ALLOW_WRITE": "false"
    }
}
```

### Options

The server makes use of the following environment variables:

- `DB_DATASOURCE` - The machine name or IP address of your target MS SQL Server instance.
- `DB_INITIAL_CATALOG` - The default database that should be used when opening a connection.
- `DB_USER` - The username used to open the database connection.
- `DB_PASSWORD` - The password used to open the database connection.
- `DB_ALLOW_MULTI` - (Optional) If set to `true`, will allow the agent to query other databases on the target server.
- `DB_ALLOW_WRITE` - (Optional) If set to `true`, will allow the agent to execute write operations (e.g. INSERT, UPDATE, DELETE, etc.).

## Contributing

Contributions are welcome! Please fork the repository and submit pull requests.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Third-Party References

- [Dapper](https://github.com/DapperLib/Dapper) – Micro ORM for .NET
- [CsvHelper](https://joshclose.github.io/CsvHelper/) – CSV parsing & writing
- [Microsoft.Data.SqlClient](https://docs.microsoft.com/dotnet/api/microsoft.data.sqlclient) – SQL Server connectivity
- [.NET Extensions](https://github.com/dotnet/extensions) libraries
