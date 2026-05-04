using System;
using System.Data;
using FirebirdSql.Data.FirebirdClient;
using System.Collections.Generic;

namespace Relatorio.Core
{
    public class DbConnectionManager
    {
        private readonly string _connectionString;

        public DbConnectionManager(string host, string database, string user, string password)
        {
            _connectionString = $"User={user};Password={password};Database={database};DataSource={host};Port=3050;Dialect=3;Charset=UTF8;";
        }

        public FbConnection CreateConnection() => new FbConnection(_connectionString);
    }

    public static class SchemaExtensions
    {
        public static List<string> GetTables(this FbConnection connection)
        {
            var tables = new List<string>();
            using (var cmd = new FbCommand("SELECT RDB$RELATION_NAME FROM RDB$RELATIONS WHERE RDB$SYSTEM_FLAG = 0 AND RDB$VIEW_BLR IS NULL", connection))
            {
                if (connection.State != ConnectionState.Open) connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tables.Add(reader.GetString(0).Trim());
                    }
                }
            }
            return tables;
        }

        public static List<ColumnInfo> GetColumns(this FbConnection connection, string tableName)
        {
            var columns = new List<ColumnInfo>();
            using (var cmd = new FbCommand($@"
                SELECT 
                    R.RDB$FIELD_NAME AS COLUMN_NAME,
                    F.RDB$FIELD_TYPE AS FIELD_TYPE
                FROM RDB$RELATION_FIELDS R
                JOIN RDB$FIELDS F ON R.RDB$FIELD_SOURCE = F.RDB$FIELD_NAME
                WHERE R.RDB$RELATION_NAME = '{tableName}'
                ORDER BY R.RDB$FIELD_POSITION", connection))
            {
                if (connection.State != ConnectionState.Open) connection.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        columns.Add(new ColumnInfo
                        {
                            Name = reader.GetString(0).Trim(),
                            DataType = reader.GetInt16(1).ToString() // Simplified type mapping
                        });
                    }
                }
            }
            return columns;
        }

        public static DataTable GetTableData(this FbConnection connection, string tableName, List<string> columns, string filter = "")
        {
            string cols = columns.Count > 0 ? string.Join(", ", columns.Select(c => $"\"{c}\"")) : "*";
            string where = string.IsNullOrWhiteSpace(filter) ? "" : $" WHERE {filter}";
            var dt = new DataTable(tableName);
            using (var adapter = new FbDataAdapter($"SELECT {cols} FROM \"{tableName}\"{where}", connection))
            {
                adapter.Fill(dt);
            }
            return dt;
        }

        public static DataTable GetRawQueryData(this FbConnection connection, string sql, string tableName = "QueryData")
        {
            var dt = new DataTable(tableName);
            using (var adapter = new FbDataAdapter(sql, connection))
            {
                adapter.Fill(dt);
            }
            return dt;
        }

        public static List<ForeignKeyInfo> GetForeignKeys(this FbConnection connection)
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

            using (var cmd = new FbCommand(sql, connection))
            {
                if (connection.State != ConnectionState.Open) connection.Open();
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
            }
            return fks;
        }
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
