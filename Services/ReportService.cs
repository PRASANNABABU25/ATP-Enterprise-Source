using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services
{
    public class ReportService
    {
        public string GenerateCSVReport(SystemInfo sys, List<TestLog> logs, string serial, string project, string customer, string operatorName, string status)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ACCEPTANCE TEST PROCEDURE (ATP) REPORT");
            sb.AppendLine("----------------------------------------");
            sb.AppendLine($"Serial Number,{serial}");
            sb.AppendLine($"Project ID,{project}");
            sb.AppendLine($"Customer Agency,{customer}");
            sb.AppendLine($"Operator ID,{operatorName}");
            sb.AppendLine($"Overall Status,{status}");
            sb.AppendLine($"Date/Time,{DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("TEST EXECUTION LOGS");
            sb.AppendLine("Test ID,Test Name,Measured Value,Specification,Tolerance,Status,Timestamp");

            foreach (var log in logs)
            {
                sb.AppendLine($"\"{log.Id}\",\"{log.Name}\",\"{log.MeasuredValue}\",\"{log.Specification}\",\"{log.Tolerance}\",\"{log.Status}\",\"{log.Timestamp}\"");
            }

            return sb.ToString();
        }

        public byte[] GeneratePDFReport(SystemInfo sys, List<TestLog> logs, string serial, string project, string customer, string operatorName, string status)
        {
            var ms = new MemoryStream();
            var writer = new StreamWriter(ms, Encoding.ASCII);
            var offsets = new List<long>();

            void WriteObj(string data)
            {
                writer.Flush();
                offsets.Add(ms.Position);
                writer.Write(data);
                writer.Flush();
            }

            writer.Write("%PDF-1.4\n%\xE2\xE3\xCF\xD3\n");
            writer.Flush();

            WriteObj("1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            WriteObj("2 0 obj\n<< /Type /Pages /Kids [ 3 0 R ] /Count 1 >>\nendobj\n");

            var sb = new StringBuilder();
            sb.AppendLine("q 0.08 0.1 0.15 rg 0 742 595 100 re f Q");
            sb.AppendLine("BT /F2 18 Tf 1 1 1 rg 30 795 Td (ACCEPTANCE TEST PROCEDURE (ATP) CERTIFICATE) Tj ET");
            sb.AppendLine("BT /F1 10 Tf 1 1 1 rg 30 775 Td (Defense & Industrial Electronics Manufacturing Environment) Tj ET");

            if (status == "PASS")
            {
                sb.AppendLine("q 0.1 0.5 0.2 rg 450 782 110 32 re f Q");
                sb.AppendLine("BT /F2 14 Tf 1 1 1 rg 485 792 Td (PASS) Tj ET");
            }
            else
            {
                sb.AppendLine("q 0.7 0.1 0.1 rg 450 782 110 32 re f Q");
                sb.AppendLine("BT /F2 14 Tf 1 1 1 rg 485 792 Td (FAIL) Tj ET");
            }

            sb.AppendLine("q 0.15 0.51 0.9 RG 1.5 w 30 720 m 565 720 l S Q");
            sb.AppendLine("BT /F2 11 Tf 0.15 0.51 0.9 rg 30 725 Td (UNIT UNDER TEST (UUT) PROFILE) Tj ET");

            int yPos = 695;
            void DrawTextMeta(string label, string val, int xOffset = 30)
            {
                sb.AppendLine($"BT /F2 9 Tf 0.1 0.1 0.1 rg {xOffset} {yPos} Td ({label}) Tj ET");
                sb.AppendLine($"BT /F1 9 Tf 0.1 0.1 0.1 rg {xOffset + 95} {yPos} Td ({val}) Tj ET");
            }

            DrawTextMeta("Serial Number:", serial); yPos -= 15;
            DrawTextMeta("Project ID:", project); yPos -= 15;
            DrawTextMeta("Customer Agency:", customer); yPos -= 15;
            DrawTextMeta("Operator ID:", operatorName); yPos -= 15;
            DrawTextMeta("Execution Time:", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

            yPos = 695;
            DrawTextMeta("CPU Model:", sys.CpuName.Length > 25 ? sys.CpuName.Substring(0, 25) + "..." : sys.CpuName, 310); yPos -= 15;
            DrawTextMeta("RAM Memory:", sys.RamInstalled, 310); yPos -= 15;
            DrawTextMeta("OS Version:", sys.OsCaption.Length > 25 ? sys.OsCaption.Substring(0, 25) + "..." : sys.OsCaption, 310); yPos -= 15;
            DrawTextMeta("Hostname:", sys.Hostname, 310); yPos -= 15;
            DrawTextMeta("Board Type:", sys.MotherboardProduct, 310);

            yPos = 600;
            sb.AppendLine("q 0.15 0.51 0.9 RG 1.5 w 30 605 m 565 605 l S Q");
            sb.AppendLine($"BT /F2 11 Tf 0.15 0.51 0.9 rg 30 612 Td (MEASUREMENT LOG DATA RECORDS) Tj ET");

            sb.AppendLine($"q 0.08 0.1 0.15 rg 30 {yPos - 5} 535 20 re f Q");
            sb.AppendLine($"BT /F2 9 Tf 1 1 1 rg 35 {yPos} Td (Test Identification) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 1 1 1 rg 195 {yPos} Td (Measured Value) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 1 1 1 rg 335 {yPos} Td (Specification) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 1 1 1 rg 445 {yPos} Td (Tolerance) Tj ET");
            sb.AppendLine($"BT /F2 9 Tf 1 1 1 rg 525 {yPos} Td (Result) Tj ET");

            yPos -= 22;

            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                if (yPos < 140) break; 

                if (i % 2 == 1)
                {
                    sb.AppendLine($"q 0.95 0.96 0.98 rg 30 {yPos - 3} 535 15 re f Q");
                }

                string safeName = log.Name.Length > 32 ? log.Name.Substring(0, 32) : log.Name;
                string safeVal = log.MeasuredValue.Length > 28 ? log.MeasuredValue.Substring(0, 28) : log.MeasuredValue;
                string safeSpec = log.Specification.Length > 22 ? log.Specification.Substring(0, 22) : log.Specification;

                sb.AppendLine($"BT /F1 8.5 Tf 0.1 0.1 0.1 rg 35 {yPos} Td ({safeName}) Tj ET");
                sb.AppendLine($"BT /F1 8.5 Tf 0.1 0.1 0.1 rg 195 {yPos} Td ({safeVal}) Tj ET");
                sb.AppendLine($"BT /F1 8.5 Tf 0.1 0.1 0.1 rg 335 {yPos} Td ({safeSpec}) Tj ET");
                sb.AppendLine($"BT /F1 8.5 Tf 0.1 0.1 0.1 rg 445 {yPos} Td ({log.Tolerance}) Tj ET");

                if (log.Status == "PASS")
                    sb.AppendLine($"BT /F2 8.5 Tf 0.1 0.5 0.2 rg 525 {yPos} Td (PASS) Tj ET");
                else
                    sb.AppendLine($"BT /F2 8.5 Tf 0.7 0.1 0.1 rg 525 {yPos} Td (FAIL) Tj ET");

                sb.AppendLine($"q 0.85 0.85 0.85 RG 0.5 w 30 {yPos - 3} m 565 {yPos - 3} l S Q");

                yPos -= 16;
            }

            yPos = 120;
            sb.AppendLine("q 0.15 0.51 0.9 RG 1 w 30 130 m 565 130 l S Q");
            sb.AppendLine($"BT /F2 10 Tf 0.15 0.51 0.9 rg 30 135 Td (OFFICIAL SIGNATURES & VERIFICATION) Tj ET");

            sb.AppendLine($"q 0.5 0.5 0.5 RG 0.8 w 35 {yPos} m 155 {yPos} l S Q");
            sb.AppendLine($"BT /F1 8 Tf 0.1 0.1 0.1 rg 35 {yPos - 10} Td (Test Operator) Tj ET");

            sb.AppendLine($"q 0.5 0.5 0.5 RG 0.8 w 215 {yPos} m 335 {yPos} l S Q");
            sb.AppendLine($"BT /F1 8 Tf 0.1 0.1 0.1 rg 215 {yPos - 10} Td (QA Test Engineer) Tj ET");

            sb.AppendLine($"q 0.5 0.5 0.5 RG 0.8 w 395 {yPos} m 515 {yPos} l S Q");
            sb.AppendLine($"BT /F1 8 Tf 0.1 0.1 0.1 rg 395 {yPos - 10} Td (Customer Acceptance) Tj ET");

            sb.AppendLine($"q 0 0 0 rg 528 55 32 60 re f Q");
            sb.AppendLine($"BT /F1 7 Tf 0.4 0.4 0.4 rg 525 45 Td (Scan code) Tj ET");
            sb.AppendLine("BT /F1 7 Tf 0.4 0.4 0.4 rg 30 20 Td (This document certifies compliance of the listed UUT with official industrial limits.) Tj ET");

            string contents = sb.ToString();
            byte[] contentBytes = Encoding.ASCII.GetBytes(contents);

            WriteObj("3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [ 0 0 595 842 ] /Contents 4 0 R /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> >>\nendobj\n");
            WriteObj($"4 0 obj\n<< /Length {contentBytes.Length} >>\nstream\n");
            writer.Flush();
            ms.Write(contentBytes, 0, contentBytes.Length);
            writer.Write("\nendstream\nendobj\n");
            writer.Flush();

            WriteObj("5 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>\nendobj\n");
            WriteObj("6 0 obj\n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold >>\nendobj\n");
            writer.Flush();

            long xrefPos = ms.Position;
            writer.Write("xref\n0 7\n0000000000 65535 f \n");
            for (int i = 0; i < offsets.Count; i++)
            {
                writer.Write($"{offsets[i]:D10} 00000 n \n");
            }
            writer.Flush();

            writer.Write($"trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n{xrefPos}\n%%EOF\n");
            writer.Flush();

            return ms.ToArray();
        }

        public void SaveSessionLog(string serial, string project, string operatorName, List<TestDefinition> sequence, Dictionary<string, TestRunResult> results)
        {
            try
            {
                var sessionData = new
                {
                    SessionId = Guid.NewGuid().ToString(),
                    Timestamp = DateTime.Now,
                    SerialNumber = serial,
                    Project = project,
                    Operator = operatorName,
                    Tests = new List<object>()
                };

                foreach (var test in sequence)
                {
                    if (results.TryGetValue(test.TestId, out var result))
                    {
                        sessionData.Tests.Add(new
                        {
                            test.TestId,
                            test.Name,
                            test.Category,
                            test.ExpectedSpecification,
                            Result = new
                            {
                                result.Outcome,
                                result.MeasuredValue,
                                result.Duration,
                                result.HealthEvents,
                                result.ErrorDetails
                            }
                        });
                    }
                }

                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string sessionDir = Path.Combine(appDir, "Logs", "Sessions");
                if (!Directory.Exists(sessionDir))
                {
                    Directory.CreateDirectory(sessionDir);
                }

                string filePath = Path.Combine(sessionDir, $"Session_{serial}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                string json = JsonSerializer.Serialize(sessionData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(filePath, json);
            }
            catch {}
        }
    }
}
