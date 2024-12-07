using FastReport;
using FastReport.Export.PdfSimple;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Npgsql;
using System.Data;

namespace Bill.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReceiptController : ControllerBase
    {
        private readonly string _connectionString = "Host=localhost;Username=postgres;Password=kumarandevi;Database=Bill";

        [HttpGet("DownloadReport")]
        public IActionResult DownloadReport(string reportType, string receiptNumber)
        {
            try
            {
                // Fetch JSON data
                string jsonData = GetReceiptJson(receiptNumber);

                // Deserialize JSON into DataSet
                var dataSet = JsonConvert.DeserializeObject<DataSet>(jsonData);

                // Determine which report template to load
                string reportFileName = reportType.ToLower() == "a4" ? "BillA4.frx" : "Bill.frx";
                var reportPath = Path.Combine(Directory.GetCurrentDirectory(), "Reports", reportFileName);

                // Debug to ensure the correct file is being selected
                if (!System.IO.File.Exists(reportPath))
                {
                    throw new FileNotFoundException($"Report file not found at path: {reportPath}");
                }
                Console.WriteLine($"Loading report template: {reportPath}");

                using (var report = new Report())
                {
                    // Clear previous state to avoid cached layouts/data
                    report.Clear();
                    report.Load(reportPath);

                    // Register the DataSet with the report
                    report.RegisterData(dataSet, "Data");

                    // Enable all tables in the DataSet
                    foreach (DataTable table in dataSet.Tables)
                    {
                        var dataSource = report.GetDataSource(table.TableName);
                        if (dataSource != null)
                        {
                            dataSource.Enabled = true;
                        }
                    }

                    // Prepare and export the report
                    report.Prepare();
                    using (var pdfExport = new PDFSimpleExport())
                    using (var memoryStream = new MemoryStream())
                    {
                        report.Export(pdfExport, memoryStream);
                        memoryStream.Position = 0;

                        // Return the PDF file
                        return File(memoryStream.ToArray(), "application/pdf", $"{reportType}_Report.pdf");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return BadRequest($"An error occurred: {ex.Message}");
            }
        }


        private string GetReceiptJson(string receiptNumber)
        {
            using (var conn = new NpgsqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new NpgsqlCommand("SELECT get_receipt_details_json(@receipt_number);", conn))
                {
                    cmd.Parameters.AddWithValue("receipt_number", receiptNumber);
                    var result = cmd.ExecuteScalar();
                    return result?.ToString() ?? string.Empty;
                }
            }
        }


    }
}
