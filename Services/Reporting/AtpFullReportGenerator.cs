using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using atp_enterprise_app_wpf.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace atp_enterprise_app_wpf.Services.Reporting
{
    public class AtpFullReportGenerator : IReportGenerator
    {
        public string ReportName => "Full ATP Compliance Certificate";

        public FlowDocument GeneratePreview(AtpReportModel data)
        {
            var doc = new FlowDocument
            {
                FontFamily = new System.Windows.Media.FontFamily("Segoe UI"),
                FontSize = 12,
                PagePadding = new Thickness(40),
                Background = Brushes.White,
                Foreground = Brushes.Black
            };

            // Cover
            var title = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("ACCEPTANCE TEST PROCEDURE (ATP) CERTIFICATE"))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                TextAlignment = System.Windows.TextAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            doc.Blocks.Add(title);

            var subTitle = new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run("Defense & Industrial Electronics Manufacturing Environment"))
            {
                FontSize = 14,
                TextAlignment = System.Windows.TextAlignment.Center,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0, 0, 0, 30)
            };
            doc.Blocks.Add(subTitle);

            // Unit Info
            doc.Blocks.Add(CreateHeader("Unit Under Test (UUT) Profile"));
            var unitTable = new System.Windows.Documents.Table { CellSpacing = 0 };
            unitTable.Columns.Add(new TableColumn { Width = new GridLength(150) });
            unitTable.Columns.Add(new TableColumn { Width = new GridLength(300) });
            var unitGroup = new TableRowGroup();
            unitGroup.Rows.Add(CreateRow("Serial Number:", data.Session.UnitSerialNumber));
            unitGroup.Rows.Add(CreateRow("Project ID:", data.Session.ProjectNumber));
            unitGroup.Rows.Add(CreateRow("Customer Agency:", data.Session.Customer));
            unitGroup.Rows.Add(CreateRow("CPU Model:", data.HardwareSnapshot.CpuName));
            unitGroup.Rows.Add(CreateRow("RAM Memory:", data.HardwareSnapshot.RamInstalled));
            unitGroup.Rows.Add(CreateRow("OS Version:", data.HardwareSnapshot.OsCaption));
            unitTable.RowGroups.Add(unitGroup);
            doc.Blocks.Add(unitTable);

            // Test Results
            doc.Blocks.Add(CreateHeader("Measurement Log Data Records"));
            var testTable = new System.Windows.Documents.Table { CellSpacing = 0 };
            testTable.Columns.Add(new TableColumn { Width = new GridLength(100) }); // ID
            testTable.Columns.Add(new TableColumn { Width = new GridLength(200) }); // Name
            testTable.Columns.Add(new TableColumn { Width = new GridLength(120) }); // Measured
            testTable.Columns.Add(new TableColumn { Width = new GridLength(80) });  // Outcome

            var testGroup = new TableRowGroup();
            testGroup.Rows.Add(CreateHeaderRow("Test ID", "Name", "Measured Value", "Outcome"));
            foreach (var test in data.Tests)
            {
                testGroup.Rows.Add(CreateRow(test.TestId, test.TestName, test.MeasuredValue, test.Outcome));
            }
            testTable.RowGroups.Add(testGroup);
            doc.Blocks.Add(testTable);

            // Audit
            doc.Blocks.Add(CreateHeader("Audit Trail"));
            var auditTable = new System.Windows.Documents.Table { CellSpacing = 0 };
            auditTable.Columns.Add(new TableColumn { Width = new GridLength(150) }); // Time
            auditTable.Columns.Add(new TableColumn { Width = new GridLength(100) }); // Actor
            auditTable.Columns.Add(new TableColumn { Width = new GridLength(250) }); // Event

            var auditGroup = new TableRowGroup();
            auditGroup.Rows.Add(CreateHeaderRow("Timestamp", "Actor", "Event"));
            foreach (var audit in data.Audits)
            {
                auditGroup.Rows.Add(CreateRow(audit.Timestamp.ToString("MM/dd/yy HH:mm:ss"), audit.Actor, audit.EventType));
            }
            auditTable.RowGroups.Add(auditGroup);
            doc.Blocks.Add(auditTable);

            return doc;
        }

        private System.Windows.Documents.Paragraph CreateHeader(string text)
        {
            return new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(text))
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 20, 0, 10),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(0, 0, 0, 5)
            };
        }

        private System.Windows.Documents.TableRow CreateHeaderRow(params string[] cells)
        {
            var row = new System.Windows.Documents.TableRow { Background = Brushes.LightGray, FontWeight = FontWeights.Bold };
            foreach (var cell in cells)
            {
                row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(cell)) { Margin = new Thickness(5) }));
            }
            return row;
        }

        private System.Windows.Documents.TableRow CreateRow(params string[] cells)
        {
            var row = new System.Windows.Documents.TableRow();
            foreach (var cell in cells)
            {
                row.Cells.Add(new System.Windows.Documents.TableCell(new System.Windows.Documents.Paragraph(new System.Windows.Documents.Run(cell ?? "")) { Margin = new Thickness(5) }));
            }
            return row;
        }

        public byte[] GeneratePdf(AtpReportModel data)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = QuestPDF.Fluent.Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily(QuestPDF.Helpers.Fonts.Arial));

                    page.Header().Element(c => ComposeHeader(c, data));
                    page.Content().Element(c => ComposeContent(c, data));
                    page.Footer().Element(c => ComposeFooter(c, data));
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, AtpReportModel data)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("ATP COMPLIANCE CERTIFICATE").FontSize(20).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                    column.Item().Text($"Session ID: {data.Session.SessionId}");
                    column.Item().Text($"Date: {data.Session.StartTime:yyyy-MM-dd HH:mm}");
                });
                
                row.ConstantItem(100).AlignRight().Text(data.Session.OverallResult).FontSize(24).SemiBold()
                    .FontColor(data.Session.OverallResult == "PASS" ? QuestPDF.Helpers.Colors.Green.Medium : QuestPDF.Helpers.Colors.Red.Medium);
            });
        }

        private void ComposeContent(IContainer container, AtpReportModel data)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(20);

                column.Item().Text("Unit Profile").FontSize(14).SemiBold().Underline();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(120);
                        columns.RelativeColumn();
                    });

                    table.Cell().Text("Serial Number:"); table.Cell().Text(data.Session.UnitSerialNumber);
                    table.Cell().Text("Project Number:"); table.Cell().Text(data.Session.ProjectNumber);
                    table.Cell().Text("Customer:"); table.Cell().Text(data.Session.Customer);
                    table.Cell().Text("CPU Model:"); table.Cell().Text(data.HardwareSnapshot.CpuName);
                });

                column.Item().Text("Test Results").FontSize(14).SemiBold().Underline();
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80);
                        columns.RelativeColumn();
                        columns.ConstantColumn(120);
                        columns.ConstantColumn(60);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Background(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(2).Text("ID");
                        header.Cell().Background(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(2).Text("Name");
                        header.Cell().Background(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(2).Text("Measured");
                        header.Cell().Background(QuestPDF.Helpers.Colors.Grey.Lighten2).Padding(2).Text("Result");
                    });

                    foreach (var test in data.Tests)
                    {
                        table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(2).Text(test.TestId);
                        table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(2).Text(test.TestName);
                        table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(2).Text(test.MeasuredValue);
                        table.Cell().BorderBottom(1).BorderColor(QuestPDF.Helpers.Colors.Grey.Lighten3).Padding(2).Text(test.Outcome)
                            .FontColor(test.Outcome == "Pass" ? QuestPDF.Helpers.Colors.Green.Medium : QuestPDF.Helpers.Colors.Red.Medium);
                    }
                });
            });
        }

        private void ComposeFooter(IContainer container, AtpReportModel data)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
                x.Span($" | Generated by {data.ReportGeneratedBy} at {data.ReportGeneratedAt}");
            });
        }

        public byte[] GenerateDocx(AtpReportModel data)
        {
            using var ms = new MemoryStream();
            using (var wordDocument = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = mainPart.Document.AppendChild(new Body());

                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new Text("ATP COMPLIANCE CERTIFICATE"))));
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new Text($"Session ID: {data.Session.SessionId}"))));
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new Text($"Overall Result: {data.Session.OverallResult}"))));
                
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new Text("Test Results:"))));
                foreach (var test in data.Tests)
                {
                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(new DocumentFormat.OpenXml.Wordprocessing.Run(new Text($"{test.TestId} - {test.TestName}: {test.MeasuredValue} [{test.Outcome}]"))));
                }
            }
            return ms.ToArray();
        }

        public byte[] GenerateXlsx(AtpReportModel data)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("ATP Session Report");
            
            ws.Cell("A1").Value = "ATP COMPLIANCE CERTIFICATE";
            ws.Cell("A2").Value = "Session ID"; ws.Cell("B2").Value = data.Session.SessionId;
            ws.Cell("A3").Value = "Serial"; ws.Cell("B3").Value = data.Session.UnitSerialNumber;
            ws.Cell("A4").Value = "Verdict"; ws.Cell("B4").Value = data.Session.OverallResult;
            
            ws.Cell("A6").Value = "Test ID";
            ws.Cell("B6").Value = "Name";
            ws.Cell("C6").Value = "Measured Value";
            ws.Cell("D6").Value = "Result";
            
            int row = 7;
            foreach (var test in data.Tests)
            {
                ws.Cell(row, 1).Value = test.TestId;
                ws.Cell(row, 2).Value = test.TestName;
                ws.Cell(row, 3).Value = test.MeasuredValue;
                ws.Cell(row, 4).Value = test.Outcome;
                row++;
            }
            
            ws.Columns().AdjustToContents();
            
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        public string GenerateCsv(AtpReportModel data)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Test ID,Test Name,Measured Value,Result");
            foreach (var test in data.Tests)
            {
                sb.AppendLine($"\"{test.TestId}\",\"{test.TestName}\",\"{test.MeasuredValue}\",\"{test.Outcome}\"");
            }
            return sb.ToString();
        }
    }
}
