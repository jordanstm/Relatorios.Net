using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using Relatorio.Models;
using Relatorio.ViewModels;

namespace Relatorio
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void TablesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (TablesList.SelectedItem is TableSchema selectedTable)
            {
                var vm = (MainViewModel)this.DataContext;
                
                if (!vm.SelectedTables.Contains(selectedTable))
                {
                    selectedTable.X = vm.SelectedTables.Count * 210 + 20;
                    selectedTable.Y = 20;
                    vm.AddTableToCanvas(selectedTable);
                }
            }
        }

        private void Table_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is TableSchema table)
            {
                table.X += e.HorizontalChange;
                table.Y += e.VerticalChange;
            }
        }

        private string? _pendingSourceTable;
        private string? _pendingSourceColumn;

        private void MenuItem_SetSource_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is ColumnSchema col)
            {
                // We need the table name. In the DataTemplate, the Grid's DataContext is ColumnSchema.
                // We need to find the parent TableSchema.
                var grid = (mi.Parent as ContextMenu)?.PlacementTarget as FrameworkElement;
                var table = FindParent<Border>(grid)?.DataContext as TableSchema;
                
                if (table != null)
                {
                    _pendingSourceTable = table.Name;
                    _pendingSourceColumn = col.Name;
                    ((MainViewModel)DataContext).ErrorMessage = $"Origem definida: {table.Name}.{col.Name}. Agora clique com o botão direito no destino.";
                }
            }
        }

        private void MenuItem_SetTarget_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem mi && mi.DataContext is ColumnSchema col)
            {
                var grid = (mi.Parent as ContextMenu)?.PlacementTarget as FrameworkElement;
                var table = FindParent<Border>(grid)?.DataContext as TableSchema;

                if (table != null && _pendingSourceTable != null && _pendingSourceColumn != null)
                {
                    var vm = (MainViewModel)DataContext;
                    var mr = new ManualRelation
                    {
                        SourceTable = _pendingSourceTable,
                        SourceColumn = _pendingSourceColumn,
                        TargetTable = table.Name,
                        TargetColumn = col.Name
                    };
                    
                    vm.AddManualRelationCommand.Execute(mr);
                    vm.ErrorMessage = $"Relação manual adicionada com sucesso!";
                    
                    _pendingSourceTable = null;
                    _pendingSourceColumn = null;
                }
                else if (_pendingSourceTable == null)
                {
                    ((MainViewModel)DataContext).ErrorMessage = "Selecione primeiro a ORIGEM da relação.";
                }
            }
        }

        private void BtnAddManualRelation_Click(object sender, RoutedEventArgs e)
        {
            var vm = (MainViewModel)DataContext;
            if (string.IsNullOrWhiteSpace(TxtSourceTable.Text) || string.IsNullOrWhiteSpace(TxtSourceColumn.Text) ||
                string.IsNullOrWhiteSpace(TxtTargetTable.Text) || string.IsNullOrWhiteSpace(TxtTargetColumn.Text))
            {
                vm.ErrorMessage = "Preencha todos os campos da relação.";
                return;
            }

            var mr = new ManualRelation
            {
                SourceTable = TxtSourceTable.Text,
                SourceColumn = TxtSourceColumn.Text,
                TargetTable = TxtTargetTable.Text,
                TargetColumn = TxtTargetColumn.Text
            };

            vm.AddManualRelationCommand.Execute(mr);
            
            TxtSourceTable.Clear();
            TxtSourceColumn.Clear();
            TxtTargetTable.Clear();
            TxtTargetColumn.Clear();
        }

        private void BtnRemoveManualRelation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ManualRelation mr)
            {
                var vm = (MainViewModel)DataContext;
                vm.RemoveManualRelationCommand.Execute(mr);
            }
        }

        private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
