using System;
using System.Text;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Data;
using System.Linq;
using Relatorio.Models;
using Relatorio.Core;
using Relatorio.Reporting;
using System.Windows.Data;
using System.Text.Json;
using Microsoft.Win32;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;

namespace Relatorio.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _host = "localhost";
        public string Host { get => _host; set { _host = value; OnPropertyChanged(); } }

        private string _database = "C:\\Path\\To\\Your\\DB.FDB";
        public string Database { get => _database; set { _database = value; OnPropertyChanged(); } }

        private string _user = "SYSDBA";
        public string User { get => _user; set { _user = value; OnPropertyChanged(); } }

        private string _password = "masterkey";
        public string Password { get => _password; set { _password = value; OnPropertyChanged(); } }

        private DatabaseType _selectedDbType = DatabaseType.Firebird;
        public DatabaseType SelectedDbType { get => _selectedDbType; set { _selectedDbType = value; OnPropertyChanged(); } }

        public IEnumerable<DatabaseType> DbTypes => Enum.GetValues(typeof(DatabaseType)).Cast<DatabaseType>();

        private string _sqlQuery = @"-- BUSCA GLOBAL DE COLUNAS (Ex: NCM)
SELECT 
    RDB$RELATION_NAME AS TABELA, 
    RDB$FIELD_NAME AS COLUNA
FROM RDB$RELATION_FIELDS 
WHERE RDB$FIELD_NAME LIKE '%NCM%'
ORDER BY 1";
        public string SqlQuery { get => _sqlQuery; set { _sqlQuery = value; OnPropertyChanged(); } }

        private DataTable? _queryResults;
        public DataTable? QueryResults { get => _queryResults; set { _queryResults = value; OnPropertyChanged(); } }

        private string _errorMessage = "";
        public string ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); } }

        private string _searchText = "";
        public string SearchText 
        { 
            get => _searchText; 
            set 
            { 
                _searchText = value; 
                AvailableTablesView.Refresh(); 
                OnPropertyChanged(); 
            } 
        }

        private IDbService? _dbService;
        private ReportGenerator _reportGenerator = new ReportGenerator();
        private const string ConfigFile = "connection.json";

        public ObservableCollection<TableSchema> AvailableTables { get; set; } = new ObservableCollection<TableSchema>();
        public ICollectionView AvailableTablesView { get; }
        public ObservableCollection<TableSchema> SelectedTables { get; set; } = new ObservableCollection<TableSchema>();
        public ObservableCollection<RelationSchema> Relations { get; set; } = new ObservableCollection<RelationSchema>();
        public ObservableCollection<ManualRelation> ManualRelations { get; set; } = new ObservableCollection<ManualRelation>();


        public ICommand ConnectCommand { get; }
        public ICommand GenerateReportCommand { get; }
        public ICommand ExportXmlCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand LoadProjectCommand { get; }
        public ICommand RemoveTableCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExecuteQueryCommand { get; }
        public ICommand AddManualRelationCommand { get; }
        public ICommand RemoveManualRelationCommand { get; }


        public MainViewModel()
        {
            ConnectCommand = new RelayCommand(Connect);
            GenerateReportCommand = new RelayCommand(GenerateReport);
            ExportXmlCommand = new RelayCommand(ExportXml);
            SaveProjectCommand = new RelayCommand(SaveProject);
            LoadProjectCommand = new RelayCommand(LoadProject);
            RemoveTableCommand = new RelayCommand(RemoveTable);
            ExportCsvCommand = new RelayCommand(ExportCsv);
            ExportExcelCommand = new RelayCommand(ExportExcel);
            ExportPdfCommand = new RelayCommand(ExportPdf);
            ExecuteQueryCommand = new RelayCommand(ExecuteQuery);
            AddManualRelationCommand = new RelayCommand(AddManualRelation);
            RemoveManualRelationCommand = new RelayCommand(RemoveManualRelation);


            AvailableTablesView = CollectionViewSource.GetDefaultView(AvailableTables);
            AvailableTablesView.Filter = FilterTables;

            LoadConnectionConfig();
        }

        private bool FilterTables(object obj)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            if (obj is TableSchema table)
            {
                // Busca no nome da tabela
                if (table.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)) return true;

                // Busca nos nomes das colunas da tabela
                return table.Columns.Any(c => c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }

        private void Connect(object? parameter)
        {
            try
            {
                ErrorMessage = "";
                _dbService?.Dispose();
                _dbService = DbServiceFactory.CreateService(SelectedDbType, Host, Database, User, Password);
                
                var tables = _dbService.GetTables();
                AvailableTables.Clear();
                foreach (var t in tables) AvailableTables.Add(new TableSchema { Name = t });

                SaveConnectionConfig();
            }
            catch (Exception ex)
            {
                Logger.LogError("Connect", ex);
                ErrorMessage = $"Erro ao conectar: {ex.Message}";
                AvailableTables.Clear();
            }
        }

        private void RemoveTable(object? parameter)
        {
            if (parameter is TableSchema table)
            {
                SelectedTables.Remove(table);
                
                // Cleanup relations
                var toRemove = Relations.Where(r => r.SourceTable == table || r.TargetTable == table).ToList();
                foreach (var rel in toRemove)
                    Relations.Remove(rel);
            }
        }

        private void SaveConnectionConfig()
        {
            try
            {
                var config = new ConnectionConfig
                {
                    DbType = SelectedDbType,
                    Host = Host,
                    Database = Database,
                    User = User,
                    Password = Password,
                    ManualRelations = ManualRelations.ToList()
                };

                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFile, json);
            }
            catch (Exception ex) { Logger.LogError("SaveConnectionConfig", ex); }
        }

        private void LoadConnectionConfig()
        {
            try
            {
                if (File.Exists(ConfigFile))
                {
                    string json = File.ReadAllText(ConfigFile);
                    var config = JsonSerializer.Deserialize<ConnectionConfig>(json);
                    if (config != null)
                    {
                        SelectedDbType = config.DbType;
                        Host = config.Host;
                        Database = config.Database;
                        User = config.User;
                        Password = config.Password;
                        ManualRelations.Clear();
                        if (config.ManualRelations != null)
                        {
                            foreach (var mr in config.ManualRelations) ManualRelations.Add(mr);
                        }
                    }
                }
            }
            catch (Exception ex) { Logger.LogError("LoadConnectionConfig", ex); }
        }

        public void AddTableToCanvas(TableSchema table)
        {
            if (!SelectedTables.Contains(table))
            {
                if (_dbService != null)
                {
                    var cols = _dbService.GetColumns(table.Name);
                    table.Columns.Clear();
                    foreach (var c in cols) 
                    {
                        var col = new ColumnSchema { Name = c.Name, DataType = c.DataType, IsSelected = false };
                        col.PropertyChanged += (sc, ec) => 
                        {
                            if (ec.PropertyName == nameof(ColumnSchema.IsGroupedBy) && col.IsGroupedBy)
                            {
                                foreach (var otherCol in table.Columns.Where(oc => oc != col))
                                    otherCol.IsGroupedBy = false;
                            }
                        };
                        table.Columns.Add(col);
                    }
                    table.ColumnsView?.Refresh();
                }
                
                table.PropertyChanged += (s, e) => {
                    if (e.PropertyName == nameof(TableSchema.X) || e.PropertyName == nameof(TableSchema.Y))
                    {
                        foreach (var rel in Relations.Where(r => r.SourceTable == table || r.TargetTable == table))
                        {
                            rel.NotifyPositionChanged();
                        }
                    }
                };

                table.ColumnsView = CollectionViewSource.GetDefaultView(table.Columns);
                table.ColumnsView.Filter = (obj) => 
                {
                    if (string.IsNullOrWhiteSpace(table.ColumnSearchText)) return true;
                    if (obj is ColumnSchema col)
                    {
                        return col.Name.Contains(table.ColumnSearchText, StringComparison.OrdinalIgnoreCase);
                    }
                    return false;
                };

                SelectedTables.Add(table);
                IdentifyForeignKeys(table);
            }
        }

        private void IdentifyForeignKeys(TableSchema newTable)
        {
            if (_dbService == null) return;
            var fks = _dbService.GetForeignKeys();
            foreach (var fk in fks)
            {
                var source = SelectedTables.FirstOrDefault(t => t.Name == fk.PkTable);
                var target = SelectedTables.FirstOrDefault(t => t.Name == fk.FkTable);

                    if (source != null && target != null)
                    {
                        if (!Relations.Any(r => r.SourceTable == source && r.TargetTable == target))
                        {
                            Relations.Add(new RelationSchema 
                            { 
                                SourceTable = source, 
                                TargetTable = target,
                                SourceColumn = fk.PkColumn,
                                TargetColumn = fk.FkColumn
                            });
                        }
                    }
                }

                // Add Manual Relations
                foreach (var mr in ManualRelations)
                {
                    var source = SelectedTables.FirstOrDefault(t => t.Name == mr.SourceTable);
                    var target = SelectedTables.FirstOrDefault(t => t.Name == mr.TargetTable);

                    if (source != null && target != null)
                    {
                        if (!Relations.Any(r => r.SourceTable == source && r.TargetTable == target && r.SourceColumn == mr.SourceColumn && r.TargetColumn == mr.TargetColumn))
                        {
                            Relations.Add(new RelationSchema
                            {
                                SourceTable = source,
                                TargetTable = target,
                                SourceColumn = mr.SourceColumn,
                                TargetColumn = mr.TargetColumn
                            });
                        }
                    }
            }
        }

        private void AddManualRelation(object? parameter)
        {
            // This will be called from a UI dialog or similar.
            // For now, let's assume parameter is a ManualRelation object
            if (parameter is ManualRelation mr)
            {
                ManualRelations.Add(mr);
                SaveConnectionConfig();
                
                // Re-evaluate relations for currently selected tables
                foreach (var table in SelectedTables) IdentifyForeignKeys(table);
            }
        }

        private void RemoveManualRelation(object? parameter)
        {
            if (parameter is ManualRelation mr)
            {
                ManualRelations.Remove(mr);
                SaveConnectionConfig();
                
                // Clean up visual relations that were based on this manual relation
                var toRemove = Relations.Where(r => 
                    r.SourceTable.Name == mr.SourceTable && 
                    r.TargetTable.Name == mr.TargetTable && 
                    r.SourceColumn == mr.SourceColumn && 
                    r.TargetColumn == mr.TargetColumn).ToList();
                
                foreach (var rel in toRemove) Relations.Remove(rel);
            }
        }

        private void GenerateReport(object? parameter)
        {
            try
            {
                var ds = BuildDataSet();
                if (ds.Tables.Count == 0)
                {
                    ErrorMessage = "Nenhuma tabela selecionada ou sem colunas marcadas.";
                    return;
                }
                _reportGenerator.GenerateReport(ds);
            }
            catch (Exception ex)
            {
                Logger.LogError("GenerateReport", ex);
                ErrorMessage = $"Erro ao gerar relatório: {ex.Message}";
            }
        }

        private void SaveProject(object? parameter)
        {
            try
            {
                var saveModel = new ProjectSaveModel
                {
                    DbType = SelectedDbType,
                    Host = Host,
                    Database = Database,
                    User = User,
                    Password = Password,
                    Tables = SelectedTables.Select(t => new TableSaveModel
                    {
                        Name = t.Name,
                        X = t.X,
                        Y = t.Y,
                        SelectedColumns = t.Columns.Where(c => c.IsSelected).Select(c => c.Name).ToList(),
                        GroupByColumn = t.Columns.FirstOrDefault(c => c.IsGroupedBy)?.Name ?? string.Empty,
                        FilterCondition = t.FilterCondition
                    }).ToList(),
                    Relations = Relations.Select(r => new RelationSaveModel
                    {
                        SourceTable = r.SourceTable.Name,
                        TargetTable = r.TargetTable.Name,
                        SourceColumn = r.SourceColumn,
                        TargetColumn = r.TargetColumn
                    }).ToList()
                };


                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog { Filter = "Projeto Relatório (*.json)|*.json" };
                if (dlg.ShowDialog() == true)
                {
                    string json = JsonSerializer.Serialize(saveModel, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(dlg.FileName, json);
                    ErrorMessage = "Projeto salvo com sucesso!";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("SaveProject", ex);
                ErrorMessage = $"Erro ao salvar projeto: {ex.Message}";
            }
        }

        private async void LoadProject(object? parameter)
        {
            try
            {
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog { Filter = "Projeto Relatório (*.json)|*.json" };
                if (dlg.ShowDialog() == true)
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var saveModel = JsonSerializer.Deserialize<ProjectSaveModel>(json);
                    if (saveModel == null) return;
                    
                    SelectedDbType = saveModel.DbType;
                    Host = saveModel.Host;
                    Database = saveModel.Database;
                    User = saveModel.User;
                    Password = saveModel.Password;

                    Connect(null); // Reconnect to fetch available tables

                    SelectedTables.Clear();
                    Relations.Clear();

                    foreach (var tSave in saveModel.Tables)
                    {
                        var table = AvailableTables.FirstOrDefault(at => at.Name == tSave.Name);
                        if (table != null)
                        {
                            table.X = tSave.X;
                            table.Y = tSave.Y;
                            table.FilterCondition = tSave.FilterCondition;
                            AddTableToCanvas(table); // This also fetches columns and identifies FKs
                            
                            // Re-apply column selection and grouping
                            foreach (var col in table.Columns)
                            {
                                col.IsSelected = tSave.SelectedColumns.Contains(col.Name);
                                col.IsGroupedBy = (col.Name == tSave.GroupByColumn);
                            }
                        }
                    }

                    // Re-apply visual relations from project save
                    if (saveModel.Relations != null)
                    {
                        foreach (var rSave in saveModel.Relations)
                        {
                            var source = SelectedTables.FirstOrDefault(t => t.Name == rSave.SourceTable);
                            var target = SelectedTables.FirstOrDefault(t => t.Name == rSave.TargetTable);
                            if (source != null && target != null)
                            {
                                if (!Relations.Any(r => r.SourceTable == source && r.TargetTable == target && r.SourceColumn == rSave.SourceColumn && r.TargetColumn == rSave.TargetColumn))
                                {
                                    Relations.Add(new RelationSchema
                                    {
                                        SourceTable = source,
                                        TargetTable = target,
                                        SourceColumn = rSave.SourceColumn,
                                        TargetColumn = rSave.TargetColumn
                                    });
                                }
                            }
                        }
                    }

                    ErrorMessage = "Projeto carregado!";
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("LoadProject", ex);
                ErrorMessage = $"Erro ao carregar projeto: {ex.Message}";
            }
        }

        private void ExportXml(object? parameter)
        {
            try
            {
                var ds = BuildDataSet();
                if (ds.Tables.Count == 0) return;

                SaveFileDialog dlg = new SaveFileDialog { Filter = "XML Data (*.xml)|*.xml", FileName = "datasource.xml" };
                if (dlg.ShowDialog() == true)
                {
                    _reportGenerator.ExportDataSourceToXml(ds, dlg.FileName);
                    ErrorMessage = "Dados exportados para XML!";
                }
            }
            catch (Exception ex) { Logger.LogError("ExportXml", ex); ErrorMessage = "Erro ao exportar XML."; }
        }

        private void ExportCsv(object? parameter)
        {
            try
            {
                DataSet ds;
                if (parameter?.ToString() == "SQL" && QueryResults != null)
                {
                    ds = new DataSet();
                    ds.Tables.Add(QueryResults.Copy());
                }
                else
                {
                    ds = BuildDataSet();
                }

                if (ds.Tables.Count == 0) return;

                SaveFileDialog dlg = new SaveFileDialog { Filter = "CSV (*.csv)|*.csv", FileName = "relatorio.csv" };
                if (dlg.ShowDialog() == true)
                {
                    _reportGenerator.ExportToCsv(ds, dlg.FileName);
                    ErrorMessage = "Dados exportados para CSV!";
                }
            }
            catch (Exception ex) { Logger.LogError("ExportCsv", ex); ErrorMessage = "Erro ao exportar CSV."; }
        }

        private void ExportExcel(object? parameter)
        {
            try
            {
                DataSet ds;
                if (parameter?.ToString() == "SQL" && QueryResults != null)
                {
                    ds = new DataSet();
                    ds.Tables.Add(QueryResults.Copy());
                }
                else
                {
                    ds = BuildDataSet();
                }

                if (ds.Tables.Count == 0) return;

                SaveFileDialog dlg = new SaveFileDialog { Filter = "Excel 97-2003 (*.xls)|*.xls", FileName = "relatorio.xls" };
                if (dlg.ShowDialog() == true)
                {
                    _reportGenerator.ExportToExcel(ds, dlg.FileName);
                    ErrorMessage = "Dados exportados para Excel!";
                }
            }
            catch (Exception ex) { Logger.LogError("ExportExcel", ex); ErrorMessage = "Erro ao exportar Excel."; }
        }

        private void ExportPdf(object? parameter)
        {
            try
            {
                var ds = BuildDataSet();
                if (ds.Tables.Count == 0) return;

                SaveFileDialog dlg = new SaveFileDialog { Filter = "PDF (*.pdf)|*.pdf", FileName = "relatorio.pdf" };
                if (dlg.ShowDialog() == true)
                {
                    _reportGenerator.ExportToPdf(ds, dlg.FileName);
                    ErrorMessage = "Relatório exportado para PDF!";
                    
                    // Proactive: Open the PDF after export
                    Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ExportPdf", ex);
                ErrorMessage = $"Erro ao exportar PDF: {ex.Message}";
            }
        }

        private void ExecuteQuery(object? parameter)
        {
            try
            {
                if (_dbService == null) { ErrorMessage = "Conecte-se ao banco primeiro."; return; }
                QueryResults = _dbService.GetRawQueryData(SqlQuery, "Resultados");
                ErrorMessage = $"Consulta executada com sucesso! ({QueryResults.Rows.Count} registros)";
            }
            catch (Exception ex)
            {
                Logger.LogError("ExecuteQuery", ex);
                ErrorMessage = $"Erro na consulta: {ex.Message}";
            }
        }

        private DataSet BuildDataSet()
        {
            var ds = new DataSet("ReportData");
            if (_dbService == null) return ds;

            if (Relations.Count > 0 && SelectedTables.Count > 1)
            {
                // Relatório CONSOLIDADO (JOINs mais robustos)
                var joinedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var sql = new StringBuilder("SELECT ");
                var columns = new List<string>();

                foreach (var table in SelectedTables)
                {
                    var selCols = table.Columns.Where(c => c.IsSelected).ToList();
                    foreach (var col in selCols)
                    {
                        // Alias: Tabela_Coluna
                        columns.Add($"{_dbService.QuoteIdentifier(table.Name)}.{_dbService.QuoteIdentifier(col.Name)} AS {_dbService.QuoteIdentifier($"{table.Name}_{col.Name}")}");
                    }
                }

                if (columns.Count == 0) return ds;

                sql.Append(string.Join(", ", columns));
                
                // Começamos com a primeira tabela selecionada
                var firstTable = SelectedTables[0];
                sql.Append($" FROM {_dbService.QuoteIdentifier(firstTable.Name)}");
                joinedTables.Add(firstTable.Name);

                // Lista de relações pendentes para processar
                var pendingRelations = new List<RelationSchema>(Relations);
                bool addedAny;

                do
                {
                    addedAny = false;
                    for (int i = pendingRelations.Count - 1; i >= 0; i--)
                    {
                        var rel = pendingRelations[i];
                        string? sourceName = rel.SourceTable?.Name;
                        string? targetName = rel.TargetTable?.Name;

                        if (sourceName == null || targetName == null) continue;

                        // Se a origem já está no SQL e o destino não, faz o JOIN
                        if (joinedTables.Contains(sourceName) && !joinedTables.Contains(targetName))
                        {
                            sql.Append($" LEFT JOIN {_dbService.QuoteIdentifier(targetName)} ON {_dbService.QuoteIdentifier(sourceName)}.{_dbService.QuoteIdentifier(rel.SourceColumn)} = {_dbService.QuoteIdentifier(targetName)}.{_dbService.QuoteIdentifier(rel.TargetColumn)}");
                            joinedTables.Add(targetName);
                            pendingRelations.RemoveAt(i);
                            addedAny = true;
                        }
                        // Se o destino já está no SQL e a origem não, faz o JOIN invertido
                        else if (joinedTables.Contains(targetName) && !joinedTables.Contains(sourceName))
                        {
                            sql.Append($" LEFT JOIN {_dbService.QuoteIdentifier(sourceName)} ON {_dbService.QuoteIdentifier(targetName)}.{_dbService.QuoteIdentifier(rel.TargetColumn)} = {_dbService.QuoteIdentifier(sourceName)}.{_dbService.QuoteIdentifier(rel.SourceColumn)}");
                            joinedTables.Add(sourceName);
                            pendingRelations.RemoveAt(i);
                            addedAny = true;
                        }
                    }
                } while (addedAny);

                // Filtros (WHERE)
                var filters = new List<string>();
                foreach (var table in SelectedTables)
                {
                    if (!string.IsNullOrWhiteSpace(table.FilterCondition))
                        filters.Add($"({_dbService.QuoteIdentifier(table.Name)}.{table.FilterCondition})");
                }
                if (filters.Count > 0) sql.Append(" WHERE " + string.Join(" AND ", filters));

                // Agrupamento/Ordenação (ORDER BY)
                var orderBy = new List<string>();
                foreach (var t in SelectedTables)
                {
                    var g = t.Columns.FirstOrDefault(c => c.IsGroupedBy);
                    if (g != null) orderBy.Add($"{_dbService.QuoteIdentifier(t.Name)}.{_dbService.QuoteIdentifier(g.Name)}");
                }
                if (orderBy.Count > 0) sql.Append(" ORDER BY " + string.Join(", ", orderBy));

                var dtResult = _dbService.GetRawQueryData(sql.ToString(), "Dados_Relatorio");
                ds.Tables.Add(dtResult.Copy());
            }
            else
            {
                foreach (var table in SelectedTables)
                {
                    var selectedCols = table.Columns.Where(c => c.IsSelected).Select(c => c.Name).ToList();
                    var groupByCol = table.Columns.FirstOrDefault(c => c.IsGroupedBy);
                    if (groupByCol != null && !selectedCols.Contains(groupByCol.Name))
                        selectedCols.Add(groupByCol.Name);
                    if (selectedCols.Count == 0) continue; 
        
                    var dt = _dbService.GetTableData(table.Name, selectedCols, table.FilterCondition);
                    if (groupByCol != null)
                    {
                        dt.DefaultView.Sort = $"{groupByCol.Name} ASC";
                        dt = dt.DefaultView.ToTable();
                    }
                    ds.Tables.Add(dt.Copy());
                }
            }
            return ds;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);
        public event EventHandler? CanExecuteChanged;
    }
}
