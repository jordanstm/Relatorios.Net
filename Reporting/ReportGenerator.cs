using System;
using System.Data;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Linq;
using Relatorio.Core;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Relatorio.Reporting
{
    public class ReportGenerator
    {
        static ReportGenerator()
        {
            // QuestPDF Community License
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public void GenerateReport(DataSet dataSet, string? reportPath = null)
        {
            try
            {
                string html = BuildHtmlReport(dataSet);
                string tempFile = Path.Combine(Path.GetTempPath(), $"Report_{DateTime.Now:yyyyMMddHHmmss}.html");
                File.WriteAllText(tempFile, html, Encoding.UTF8);

                if (File.Exists(tempFile))
                {
                    var dispatcher = System.Windows.Application.Current?.Dispatcher;
                    if (dispatcher != null)
                    {
                        dispatcher.Invoke(() => 
                        {
                            var preview = new PreviewWindow(tempFile);
                            preview.Show();
                        });
                    }
                    else
                    {
                        Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("ReportGenerator.GenerateReport", ex);
                throw;
            }
        }

        private string BuildHtmlReport(DataSet dataSet)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang='pt-br'>");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset='UTF-8'>");
            sb.AppendLine("    <title>Relatório de Dados - Universal Data Reporter</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #1E1E1E; color: #D4D4D4; margin: 40px; }");
            sb.AppendLine("        h1 { color: #FFFFFF; border-bottom: 2px solid #007ACC; padding-bottom: 10px; margin-bottom: 30px; }");
            sb.AppendLine("        .table-container { margin-bottom: 50px; background: #252526; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.5); }");
            sb.AppendLine("        .table-header { background: #007ACC; color: #FFFFFF; padding: 15px 20px; font-weight: bold; font-size: 1.2em; display: flex; justify-content: space-between; }");
            sb.AppendLine("        table { width: 100%; border-collapse: collapse; }");
            sb.AppendLine("        th { background: #37373D; color: #FFFFFF; text-align: left; padding: 12px 15px; border-bottom: 2px solid #3E3E42; font-size: 0.9em; text-transform: uppercase; }");
            sb.AppendLine("        td { padding: 10px 15px; border-bottom: 1px solid #3E3E42; font-size: 0.95em; }");
            sb.AppendLine("        tr:nth-child(even) { background: rgba(255, 255, 255, 0.02); }");
            sb.AppendLine("        tr:hover { background: rgba(0, 122, 204, 0.1); }");
            sb.AppendLine("        .footer { margin-top: 40px; font-size: 0.8em; color: #888; text-align: center; border-top: 1px solid #3E3E42; padding-top: 20px; }");
            sb.AppendLine("        .badge { background: #444; padding: 2px 8px; border-radius: 4px; font-size: 0.7em; vertical-align: middle; }");
            
            // Determinar orientação baseada no número máximo de colunas
            int maxCols = 0;
            foreach (DataTable dt in dataSet.Tables) if (dt.Columns.Count > maxCols) maxCols = dt.Columns.Count;
            string orientation = maxCols > 3 ? "landscape" : "portrait";
            float zoom = maxCols > 5 ? 75 : (maxCols > 3 ? 85 : 100);

            sb.AppendLine("        @media print { ");
            sb.AppendLine($"            @page {{ size: {orientation}; margin: 1cm; }}");
            sb.AppendLine($"            body {{ background-color: #FFFFFF !important; color: #000000 !important; margin: 0; padding: 0; zoom: {zoom}%; }}");
            sb.AppendLine("            .table-container { background: #FFF !important; color: #000 !important; box-shadow: none !important; border: 1px solid #DDD !important; page-break-inside: auto; overflow: visible !important; }");
            sb.AppendLine("            .table-header { background: #EEE !important; color: #000 !important; border-bottom: 2px solid #000 !important; page-break-after: avoid; }");
            sb.AppendLine("            table { page-break-inside: auto; border: 1px solid #CCC; }");
            sb.AppendLine("            thead { display: table-header-group; }");
            sb.AppendLine("            tr { page-break-inside: avoid !important; page-break-after: auto; }");
            sb.AppendLine("            th { background: #F5F5F5 !important; color: #000 !important; border-bottom: 2px solid #000 !important; border-right: 1px solid #DDD !important; }");
            sb.AppendLine("            td { border-bottom: 1px solid #EEE !important; color: #000 !important; border-right: 1px solid #EEE !important; }");
            sb.AppendLine("            h1 { color: #000 !important; border-bottom: 2px solid #000 !important; page-break-after: avoid; }");
            sb.AppendLine("            .badge { background: #EEE !important; color: #000 !important; border: 1px solid #999; }");
            sb.AppendLine("        }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            
            sb.AppendLine($"    <h1>Relatório de Dados <span style='font-size: 0.5em; float: right; font-weight: normal; color: #888;'>Gerado em: {DateTime.Now}</span></h1>");

            foreach (DataTable table in dataSet.Tables)
            {
                string displayName = table.TableName == "Dados_Relatorio" ? "Dados Consolidados" : table.TableName;
                
                sb.AppendLine("    <div class='table-container'>");
                sb.AppendLine("        <div class='table-header'>");
                sb.AppendLine($"            <span>{displayName}</span>");
                sb.AppendLine($"            <span class='badge'>{table.Rows.Count} Registros</span>");
                sb.AppendLine("        </div>");
                sb.AppendLine("        <table>");
                sb.AppendLine("            <thead>");
                sb.AppendLine("                <tr>");
                foreach (DataColumn col in table.Columns)
                {
                    string colHeader = col.ColumnName;
                    if (colHeader.Contains("_"))
                    {
                        var parts = colHeader.Split('_');
                        if (parts.Length >= 2)
                        {
                            // Formato: Tabela.Coluna
                            colHeader = $"{parts[0]}.{string.Join("_", parts.Skip(1))}";
                        }
                    }
                    sb.AppendLine($"                    <th>{colHeader}</th>");
                }
                sb.AppendLine("                </tr>");
                sb.AppendLine("            </thead>");
                sb.AppendLine("            <tbody>");
                
                foreach (DataRow row in table.Rows)
                {
                    sb.AppendLine("                <tr>");
                    foreach (DataColumn col in table.Columns)
                    {
                        string val = row[col]?.ToString() ?? "";
                        if (string.IsNullOrEmpty(val)) val = "<i style='color:#555'>null</i>";
                        sb.AppendLine($"                    <td>{val}</td>");
                    }
                    sb.AppendLine("                </tr>");
                }
                
                sb.AppendLine("            </tbody>");
                sb.AppendLine("        </table>");
                sb.AppendLine("    </div>");
            }

            sb.AppendLine("    <div class='footer'>");
            sb.AppendLine("        Universal Data Reporter - Gerado Automaticamente");
            sb.AppendLine("    </div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        public void ExportDataSourceToXml(DataSet dataSet, string filePath)
        {
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));
            dataSet.WriteXml(filePath, XmlWriteMode.WriteSchema);
        }

        public void ExportToCsv(DataSet dataSet, string filePath)
        {
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));
            StringBuilder sb = new StringBuilder();

            foreach (DataTable table in dataSet.Tables)
            {
                // Título da Tabela
                sb.AppendLine($"--- TABELA: {table.TableName} ---");
                
                // Header
                var columnNames = table.Columns.Cast<DataColumn>().Select(column => column.ColumnName);
                sb.AppendLine(string.Join(";", columnNames));

                // Rows
                foreach (DataRow row in table.Rows)
                {
                    var fields = row.ItemArray.Select(field => field?.ToString()?.Replace(";", " "));
                    sb.AppendLine(string.Join(";", fields));
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportToExcel(DataSet dataSet, string filePath)
        {
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));
            
            // Excel can open a simple HTML table if saved as .xls
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:x='urn:schemas-microsoft-com:office:excel' xmlns='http://www.w3.org/TR/REC-html40'>");
            sb.AppendLine("<head><meta charset='utf-8'></head><body>");
            
            foreach (DataTable table in dataSet.Tables)
            {
                sb.AppendLine($"<h3>Tabela: {table.TableName}</h3>");
                sb.AppendLine("<table border='1'>");
                
                // Header
                sb.AppendLine("<tr>");
                foreach (DataColumn col in table.Columns)
                {
                    sb.AppendLine($"<th style='background-color:#eee;'>{col.ColumnName}</th>");
                }
                sb.AppendLine("</tr>");

                // Data
                foreach (DataRow row in table.Rows)
                {
                    sb.AppendLine("<tr>");
                    foreach (DataColumn col in table.Columns)
                    {
                        sb.AppendLine($"<td>{row[col]}</td>");
                    }
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</table><br/>");
            }
            
            sb.AppendLine("</body></html>");
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        public void ExportToPdf(DataSet dataSet, string filePath)
        {
            if (dataSet == null) throw new ArgumentNullException(nameof(dataSet));

            Document.Create(container =>
            {
                int maxCols = 0;
                foreach (DataTable dt in dataSet.Tables) if (dt.Columns.Count > maxCols) maxCols = dt.Columns.Count;
                var pageSize = maxCols > 3 ? PageSizes.A4.Landscape() : PageSizes.A4.Portrait();

                container.Page(page =>
                {
                    page.Size(pageSize);
                    page.Margin(1, Unit.Centimetre);

                    page.PageColor(Colors.White);
                    
                    // Dynamic font size based on column count
                    float baseFontSize = 8;
                    page.DefaultTextStyle(x => x.FontSize(baseFontSize).FontFamily(Fonts.SegoeUI));


                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("Relatório de Dados - Universal Data Reporter").FontSize(18).Bold().FontColor(Colors.Blue.Medium);
                            col.Item().Text($"Gerado em: {DateTime.Now}").FontSize(8).FontColor(Colors.Grey.Medium);
                        });
                    });

                    page.Content().PaddingVertical(10).Column(column =>
                    {
                        foreach (DataTable table in dataSet.Tables)
                        {
                            string displayName = table.TableName == "Dados_Relatorio" ? "Dados Consolidados" : table.TableName;
                            column.Item().PaddingTop(10).Text(displayName).Bold().FontSize(12).Underline();
                            
                            column.Item().Table(tab =>
                            {
                                int colCount = table.Columns.Count;
                                tab.ColumnsDefinition(c =>
                                {
                                    foreach (DataColumn col in table.Columns)
                                    {
                                        string name = col.ColumnName.ToUpper();
                                        if (name.Contains("DESCRICAO") || name.Contains("NOME") || name.Contains("OBS"))
                                            c.RelativeColumn(3); // Mais espaço para descrições
                                        else if (name.Contains("ID") || name.Contains("CODIGO") || name.Contains("DATA"))
                                            c.RelativeColumn(1); // Menos espaço para IDs/Códigos
                                        else
                                            c.RelativeColumn(1.5f);
                                    }
                                });


                                // Header
                                tab.Header(header =>
                                {
                                    foreach (DataColumn col in table.Columns)
                                    {
                                        string colHeader = col.ColumnName;
                                        if (colHeader.Contains("_"))
                                        {
                                            var parts = colHeader.Split('_');
                                            if (parts.Length >= 2) colHeader = $"{parts[0]}.{string.Join("_", parts.Skip(1))}";
                                        }
                                        header.Cell().Background(Colors.Grey.Lighten3).Padding(4).Text(colHeader).Bold().FontSize(8);
                                    }
                                });

                                // Data
                                foreach (DataRow dr in table.Rows)
                                {
                                    foreach (DataColumn col in table.Columns)
                                    {
                                        string val = dr[col]?.ToString() ?? "";
                                        tab.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten4).Padding(4).Text(val).FontSize(8);
                                    }
                                }
                            });
                        }
                    });

                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf(filePath);
        }
    }
}
