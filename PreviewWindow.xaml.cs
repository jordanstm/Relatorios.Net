using System;
using System.Windows;

namespace Relatorio
{
    public partial class PreviewWindow : Window
    {
        public PreviewWindow(string filePath)
        {
            InitializeComponent();
            ReportBrowser.Navigate(new Uri(filePath));
        }

        private void Print_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Trigger native print dialog for the HTML content
                dynamic doc = ReportBrowser.Document;
                doc?.parentWindow?.print();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Erro ao abrir diálogo de impressão: " + ex.Message);
            }
        }
    }
}
