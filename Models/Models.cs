using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Data;

namespace Relatorio.Models
{
    public abstract class BaseMetadata : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name 
        { 
            get => _name; 
            set { _name = value; OnPropertyChanged(); } 
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class TableSchema : BaseMetadata
    {
        public ObservableCollection<ColumnSchema> Columns { get; set; } = new ObservableCollection<ColumnSchema>();
        
        private double _x;
        public double X { get => _x; set { _x = value; OnPropertyChanged(); } }

        private double _y;
        public double Y { get => _y; set { _y = value; OnPropertyChanged(); } }

        private string _filterCondition = string.Empty;
        public string FilterCondition { get => _filterCondition; set { _filterCondition = value; OnPropertyChanged(); } }

        private string _columnSearchText = string.Empty;
        public string ColumnSearchText 
        { 
            get => _columnSearchText; 
            set 
            { 
                _columnSearchText = value; 
                OnPropertyChanged();
                ColumnsView?.Refresh();
            } 
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public ICollectionView? ColumnsView { get; set; }
    }

    public class ColumnSchema : BaseMetadata
    {
        public string DataType { get; set; } = string.Empty;
        public bool IsPrimaryKey { get; set; }

        private bool _isSelected = false;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        private bool _isGroupedBy = false;
        public bool IsGroupedBy { get => _isGroupedBy; set { _isGroupedBy = value; OnPropertyChanged(); } }
    }

    public class RelationSchema : INotifyPropertyChanged
    {
        public TableSchema SourceTable { get; set; } = null!;
        public TableSchema TargetTable { get; set; } = null!;
        public string SourceColumn { get; set; } = string.Empty;
        public string TargetColumn { get; set; } = string.Empty;

        // Visual coordinates for the link line
        public double X1 => SourceTable.X + 80;
        public double Y1 => SourceTable.Y + 20;
        public double X2 => TargetTable.X + 80;
        public double Y2 => TargetTable.Y + 20;

        public event PropertyChangedEventHandler? PropertyChanged;
        public void NotifyPositionChanged()
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X1)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Y1)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(X2)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Y2)));
        }
    }

    public class ManualRelation : BaseMetadata
    {
        public string SourceTable { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public string TargetTable { get; set; } = string.Empty;
        public string TargetColumn { get; set; } = string.Empty;

        // Displays as "TABLE1.COL1 -> TABLE2.COL2"
        public string DisplayName => $"{SourceTable}.{SourceColumn} -> {TargetTable}.{TargetColumn}";
    }

    public class ProjectSaveModel
    {
        public string Host { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<TableSaveModel> Tables { get; set; } = new List<TableSaveModel>();
        public List<RelationSaveModel> Relations { get; set; } = new List<RelationSaveModel>();
    }

    public class RelationSaveModel
    {
        public string SourceTable { get; set; } = string.Empty;
        public string TargetTable { get; set; } = string.Empty;
        public string SourceColumn { get; set; } = string.Empty;
        public string TargetColumn { get; set; } = string.Empty;
    }

    public class TableSaveModel
    {
        public string Name { get; set; } = string.Empty;
        public double X { get; set; }
        public double Y { get; set; }
        public List<string> SelectedColumns { get; set; } = new List<string>();
        public string GroupByColumn { get; set; } = string.Empty;
        public string FilterCondition { get; set; } = string.Empty;
    }

    public class ConnectionConfig
    {
        public string Host { get; set; } = string.Empty;
        public string Database { get; set; } = string.Empty;
        public string User { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public List<ManualRelation> ManualRelations { get; set; } = new List<ManualRelation>();
    }
}
