using System;
using System.Windows.Documents;
using atp_enterprise_app_wpf.Models;

namespace atp_enterprise_app_wpf.Services.Reporting
{
    public interface IReportGenerator
    {
        string ReportName { get; }
        
        FlowDocument GeneratePreview(AtpReportModel data);
        byte[] GeneratePdf(AtpReportModel data);
        byte[] GenerateDocx(AtpReportModel data);
        byte[] GenerateXlsx(AtpReportModel data);
        string GenerateCsv(AtpReportModel data);
    }
}
