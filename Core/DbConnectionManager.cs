using System;
using System.Data;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FirebirdSql.Data.FirebirdClient;
using Microsoft.Data.SqlClient;
using Oracle.ManagedDataAccess.Client;
using Relatorio.Models;

namespace Relatorio.Core
{
    public interface IDbService : IDisposable
    {
        List<string> GetTables();
        List<ColumnInfo> GetColumns(string tableName);
        List<ForeignKeyInfo> GetForeignKeys();
        DataTable GetTableData(string tableName, List<string> columns, string filter = "");
        DataTable GetRawQueryData(string sql, string tableName = "QueryData");
        string QuoteIdentifier(string identifier);
    }

    public class DbServiceFactory
    {
        public static IDbService CreateService(DatabaseType type, string host, string database, string user, string password)
        {
            return type switch
            {
                DatabaseType.Firebird => new FirebirdService(host, database, user, password),
                DatabaseType.SqlServer => new SqlServerService(host, database, user, password),
                DatabaseType.Oracle => new OracleService(host, database, user, password),
                _ => throw new NotSupportedException($"Database type {type} is not supported.")
            };
        }
    }

    public abstract class BaseDbService : IDbService
    {
        protected string ConnectionString { get; }
        protected IDbConnection Connection { get; set; } = null!;

        protected BaseDbService(string connectionString)
        {
            ConnectionString = connectionString;
        }

        public abstract List<string> GetTables();
        public abstract List<ColumnInfo> GetColumns(string tableName);
        public abstract List<ForeignKeyInfo> GetForeignKeys();
        public abstract string QuoteIdentifier(string identifier);

        public virtual DataTable GetTableData(string tableName, List<string> columns, string filter = "")
        {
            string cols = columns.Count > 0 ? string.Join(", ", columns.Select(QuoteIdentifier)) : "*";
            string where = string.IsNullOrWhiteSpace(filter) ? "" : $" WHERE {filter}";
            return GetRawQueryData($"SELECT {cols} FROM {QuoteIdentifier(tableName)}{where}", tableName);
        }

        public virtual DataTable GetRawQueryData(string sql, string tableName = "QueryData")
        {
            EnsureConnection();
            var dt = new DataTable(tableName);
            using (var cmd = Connection.CreateCommand())
            {
                cmd.CommandText = sql;
                using (var reader = cmd.ExecuteReader())
                {
                    dt.Load(reader);
                }
            }
            return dt;
        }

        protected void EnsureConnection()
        {
            if (Connection.State != ConnectionState.Open) Connection.Open();
        }

        public void Dispose()
        {
            Connection?.Close();
            Connection?.Dispose();
        }
    }

    public class FirebirdService : BaseDbService
    {
        public FirebirdService(string host, string database, string user, string password) 
            : base($"User={user};Password={password};Database={database};DataSource={host};Port=3050;Dialect=3;Charset=UTF8;")
        {
            Connection = new FbConnection(ConnectionString);
        }

        public override List<string> GetTables()
        {
            var tables = new List<string>();
            EnsureConnection();
            using (var cmd = new FbCommand("SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 AND RDB$VIEW_BLR IS NULL", (FbConnection)Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read()) tables.Add(reader.GetString(0).Trim());
            }
            return tables;
        }

        public override List<ColumnInfo> GetColumns(string tableName)
        {
            var columns = new List<ColumnInfo>();
            EnsureConnection();
            using (var cmd = new FbCommand($@"
                SELECT 
                    R.RDB$FIELD_NAME AS COLUMN_NAME,
                    F.RDB$FIELD_TYPE AS FIELD_TYPE
                FROM RDB$RELATION_FIELDS R
                JOIN RDB$FIELDS F ON R.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                WHERE R.RDB$RELATION_NAME = '{tableName}'
                ORDER BY R.RDB$FIELD_POSITION", (FbConnection)Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns.Add(new ColumnInfo { Name = reader.GetString(0).Trim(), DataType = reader.GetInt16(1).ToString() });
                }
            }
            return columns;
        }

        public override List<ForeignKeyInfo> GetForeignKeys()
        {
            var fks = new List<ForeignKeyInfo>();
            const string sql = @"
                SELECT 
                    PK.RDB$RELATION_NAME AS PK_TABLE,
                    FK.RDB$RELATION_NAME AS FK_TABLE,
                    ISP.RDB$FIELD_NAME AS PK_COLUMN,
                    ISF.RDB$FIELD_NAME AS FK_COLUMN
                FROM RDB$RELATION_CONSTRAINTS FK
                JOIN RDB$REF_CONSTRAINTS RC ON FK.RDB$CONSTRAINT_NAME = RC.RDB$CONSTRAINT_NAME
                JOIN RDB$RELATION_CONSTRAINTS PK ON RC.RDB$CONST_NAME_UQ = PK.RDB$CONSTRAINT_NAME
                JOIN RDB$INDEX_SEGMENTS ISF ON FK.RDB$INDEX_NAME = ISF.RDB$INDEX_NAME
                JOIN RDB$INDEX_SEGMENTS ISP ON PK.RDB$INDEX_NAME = ISP.RDB$INDEX_NAME
                WHERE FK.RDB$CONSTRAINT_TYPE = 'FOREIGN KEY'";

            EnsureConnection();
            using (var cmd = new FbCommand(sql, (FbConnection)Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    fks.Add(new ForeignKeyInfo
                    {
                        PkTable = reader.GetString(0).Trim(),
                        FkTable = reader.GetString(1).Trim(),
                        PkColumn = reader.GetString(2).Trim(),
                        FkColumn = reader.GetString(3).Trim()
                    });
                }
            }
            return fks;
        }

        public override string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
    }

    public class SqlServerService : BaseDbService
    {
        public SqlServerService(string host, string database, string user, string password) 
            : base($"Server={host};Database={database};User Id={user};Password={password};TrustServerCertificate=True;")
        {
            Connection = new SqlConnection(ConnectionString);
        }

        public override List<string> GetTables()
        {
            var tables = new List<string>();
            EnsureConnection();
            using (var cmd = new SqlCommand("SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'", (SqlConnection)Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read()) tables.Add(reader.GetString(0));
            }
            return tables;
        }

        public override List<ColumnInfo> GetColumns(string tableName)
        {
            var columns = new List<ColumnInfo>();
            EnsureConnection();
            using (var cmd = new SqlCommand("SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @tableName", (SqlConnection)Connection))
            {
                cmd.Parameters.AddWithValue("@tableName", tableName);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(new ColumnInfo { Name = reader.GetString(0), DataType = reader.GetString(1) });
                    }
                }
            }
            return columns;
        }

        public override List<ForeignKeyInfo> GetForeignKeys()
        {
            var fks = new List<ForeignKeyInfo>();
            const string sql = @"
                SELECT 
                    OBJECT_NAME(f.referenced_object_id) AS PK_TABLE,
                    OBJECT_NAME(f.parent_object_id) AS FK_TABLE,
                    COL_NAME(fc.referenced_object_id, fc.referenced_column_id) AS PK_COLUMN,
                    COL_NAME(fc.parent_object_id, fc.parent_column_id) AS FK_COLUMN
                FROM sys.foreign_keys AS f
                INNER JOIN sys.foreign_key_columns AS fc ON f.object_id = fc.constraint_object_id";

            EnsureConnection();
            using (var cmd = new SqlCommand(sql, (SqlConnection)Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    fks.Add(new ForeignKeyInfo
                    {
                        PkTable = reader.GetString(0),
                        FkTable = reader.GetString(1),
                        PkColumn = reader.GetString(2),
                        FkColumn = reader.GetString(3)
                    });
                }
            }
            return fks;
        }

        public override string QuoteIdentifier(string identifier) => $"[{identifier}]";
    }

    public class OracleService : BaseDbService
    {
        public OracleService(string host, string service, string user, string password) 
            : base($"Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST={host})(PORT=1521))(CONNECT_DATA=(SERVICE_NAME={service})));User Id={user};Password={password};")
        {
            Connection = new OracleConnection(ConnectionString);
        }

        public override List<string> GetTables()
        {
            var tables = new List<string>();
            EnsureConnection();
            using (var cmd = new OracleCommand("SELECT TABLE_NAME FROM USER_TABLES", (OracleConnection)Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read()) tables.Add(reader.GetString(0));
            }
            return tables;
        }

        public override List<ColumnInfo> GetColumns(string tableName)
        {
            var columns = new List<ColumnInfo>();
            EnsureConnection();
            using (var cmd = new OracleCommand("SELECT COLUMN_NAME, DATA_TYPE FROM USER_TAB_COLUMNS WHERE TABLE_NAME = :tableName", (OracleConnection)Connection))
            {
                cmd.Parameters.Add(":tableName", tableName);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(new ColumnInfo { Name = reader.GetString(0), DataType = reader.GetString(1) });
                    }
                }
            }
            return columns;
        }

        public override List<ForeignKeyInfo> GetForeignKeys()
        {
            var fks = new List<ForeignKeyInfo>();
            const string sql = @"
                SELECT 
                    r.TABLE_NAME AS PK_TABLE,
                    c.TABLE_NAME AS FK_TABLE,
                    rc.COLUMN_NAME AS PK_COLUMN,
                    cc.COLUMN_NAME AS FK_COLUMN
                FROM USER_CONSTRAINTS c
                JOIN USER_CONSTRAINTS r ON c.R_CONSTRAINT_NAME = r.CONSTRAINT_NAME
                JOIN USER_CONS_COLUMNS cc ON c.CONSTRAINT_NAME = cc.CONSTRAINT_NAME
                JOIN USER_CONS_COLUMNS rc ON r.CONSTRAINT_NAME = rc.CONSTRAINT_NAME AND cc.POSITION = rc.POSITION
                WHERE c.CONSTRAINT_TYPE = 'R'";

            EnsureConnection();
            using (var cmd = new OracleCommand(sql, (OracleConnection)Connection))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    fks.Add(new ForeignKeyInfo
                    {
                        PkTable = reader.GetString(0),
                        FkTable = reader.GetString(1),
                        PkColumn = reader.GetString(2),
                        FkColumn = reader.GetString(3)
                    });
                }
            }
            return fks;
        }

        public override string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
    }

    public class ColumnInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
    }

    public class ForeignKeyInfo
    {
        public string PkTable { get; set; } = string.Empty;
        public string FkTable { get; set; } = string.Empty;
        public string PkColumn { get; set; } = string.Empty;
        public string FkColumn { get; set; } = string.Empty;
    }
}
