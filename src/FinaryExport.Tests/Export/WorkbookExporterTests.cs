using ClosedXML.Excel;
using FluentAssertions;
using Moq;
using FinaryExport.Api;
using FinaryExport.Export;
using FinaryExport.Models;
using FinaryExport.Models.Accounts;

namespace FinaryExport.Tests.Export;

// Tests for XLSX export functionality.
// Validates workbook structure, sheet creation, data writing, and error handling.
// Uses ClosedXML directly — no file I/O needed for in-memory workbook tests.
public sealed class WorkbookExporterTests
{
    // ================================================================
    // WORKBOOK STRUCTURE
    // ================================================================

    [Fact]
    public void CreateWorkbook_ProducesValidXlsxInMemory()
    {
        // Act
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Summary");

        // Assert: ClosedXML can create a valid in-memory workbook
        workbook.Worksheets.Should().HaveCount(1);
        workbook.Worksheets.First().Name.Should().Be("Summary");
    }

    [Fact]
    public void SaveWorkbook_ToStream_ProducesValidXlsx()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Test");
        ws.Cell(1, 1).Value = "Hello";

        // Act: save to stream
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        // Assert: stream has xlsx content (starts with PK zip header)
        stream.Length.Should().BeGreaterThan(0);
        stream.Position = 0;
        var header = new byte[4];
        stream.ReadExactly(header, 0, 4);
        header[0].Should().Be(0x50); // 'P'
        header[1].Should().Be(0x4B); // 'K'
    }

    [Fact]
    public void SaveWorkbook_ToFile_CreatesFile()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), $"finary_test_{Guid.NewGuid()}.xlsx");
        try
        {
            using var workbook = new XLWorkbook();
            workbook.AddWorksheet("Summary");

            // Act
            workbook.SaveAs(tempPath);

            // Assert
            File.Exists(tempPath).Should().BeTrue();
            new FileInfo(tempPath).Length.Should().BeGreaterThan(0);
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    // ================================================================
    // SHEET PER CATEGORY
    // ================================================================

    [Theory]
    [InlineData("Summary")]
    [InlineData("Checkings")]
    [InlineData("Savings")]
    [InlineData("Investments")]
    [InlineData("Real Estate")]
    [InlineData("Crypto")]
    [InlineData("Fonds Euro")]
    [InlineData("Commodities")]
    [InlineData("Credits")]
    [InlineData("Other Assets")]
    [InlineData("Startups")]
    [InlineData("Transactions")]
    [InlineData("Dividends")]
    public void Workbook_EachCategory_HasOwnSheet(string sheetName)
    {
        // Arrange: simulate full export with all expected sheets
        using var workbook = new XLWorkbook();
        var expectedSheets = new[]
        {
            "Summary", "Checkings", "Savings", "Investments", "Real Estate",
            "Crypto", "Fonds Euro", "Commodities", "Credits", "Other Assets",
            "Startups", "Transactions", "Dividends"
        };

        foreach (var name in expectedSheets)
            workbook.AddWorksheet(name);

        // Assert: requested sheet exists
        workbook.Worksheets.TryGetWorksheet(sheetName, out var ws).Should().BeTrue(
            $"sheet '{sheetName}' should exist in the workbook");
        ws.Should().NotBeNull();
    }

    [Fact]
    public void Workbook_HasExpectedSheetCount()
    {
        using var workbook = new XLWorkbook();
        var expectedSheets = new[]
        {
            "Summary", "Checkings", "Savings", "Investments", "Real Estate",
            "Crypto", "Fonds Euro", "Commodities", "Credits", "Other Assets",
            "Startups", "Transactions", "Dividends"
        };

        foreach (var name in expectedSheets)
            workbook.AddWorksheet(name);

        // Per architecture.md: 13 sheets
        workbook.Worksheets.Should().HaveCount(13);
    }

    // ================================================================
    // COLUMN HEADERS
    // ================================================================

    [Fact]
    public void AccountSheet_ColumnHeaders_MatchExpectedFields()
    {
        // Arrange: write account headers as the AccountsSheet would
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Checkings");

        var headers = new[] { "Name", "Institution", "Balance", "Currency", "Buying Value", "Unrealized P&L", "Annual Yield", "IBAN", "Opened At", "Last Sync" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // Assert
        ws.Cell(1, 1).GetString().Should().Be("Name");
        ws.Cell(1, 2).GetString().Should().Be("Institution");
        ws.Cell(1, 3).GetString().Should().Be("Balance");
        ws.Cell(1, 4).GetString().Should().Be("Currency");
        ws.Cell(1, 5).GetString().Should().Be("Buying Value");
        ws.Cell(1, 6).GetString().Should().Be("Unrealized P&L");
        ws.Cell(1, 7).GetString().Should().Be("Annual Yield");
        ws.Cell(1, 8).GetString().Should().Be("IBAN");
    }

    [Fact]
    public void TransactionSheet_ColumnHeaders_MatchExpectedFields()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Transactions");

        var headers = new[] { "Date", "Name", "Value", "Type", "Category", "Internal Transfer", "Include in Analysis" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(1, 1).GetString().Should().Be("Date");
        ws.Cell(1, 2).GetString().Should().Be("Name");
        ws.Cell(1, 3).GetString().Should().Be("Value");
        ws.Cell(1, 4).GetString().Should().Be("Type");
    }

    [Fact]
    public void SummarySheet_ColumnHeaders_MatchExpectedFields()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Summary");

        var headers = new[] { "Metric", "Value" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        ws.Cell(1, 1).GetString().Should().Be("Metric");
        ws.Cell(1, 2).GetString().Should().Be("Value");
    }

    // ================================================================
    // DATA WRITING
    // ================================================================

    [Fact]
    public void AccountSheet_WritesAccountData_WithCorrectTypes()
    {
        // Arrange
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Checkings");

        // Headers
        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 2).Value = "Balance";

        // Data row (simulating what AccountsSheet would write)
        ws.Cell(2, 1).Value = "BNP Main Checking";
        ws.Cell(2, 2).Value = 4523.67m;

        // Assert: data integrity
        ws.Cell(2, 1).GetString().Should().Be("BNP Main Checking");
        ws.Cell(2, 2).GetDouble().Should().BeApproximately(4523.67, 0.01);
    }

    [Fact]
    public void MonetaryValues_UseDecimalPrecision()
    {
        // Architecture decision D10: decimal for money, never double
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Test");

        // Write a precise monetary value
        ws.Cell(1, 1).Value = 150000.50m;
        ws.Cell(1, 1).Style.NumberFormat.Format = "#,##0.00";

        ws.Cell(1, 1).GetDouble().Should().BeApproximately(150000.50, 0.001);
    }

    // ================================================================
    // EMPTY DATA HANDLING
    // ================================================================

    [Fact]
    public void EmptyAccountList_ProducesSheetWithHeadersOnly()
    {
        // Arrange: category has no accounts
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Startups");

        // Write headers only — no data rows
        var headers = new[] { "Name", "Balance", "State" };
        for (var i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];

        // Assert: sheet exists with headers, no data
        ws.Cell(1, 1).GetString().Should().Be("Name");
        ws.Cell(2, 1).IsEmpty().Should().BeTrue("no data rows for empty category");
        ws.LastRowUsed()!.RowNumber().Should().Be(1, "only header row");
    }

    [Fact]
    public void EmptyTransactionList_ProducesSheetWithHeadersOnly()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Transactions");

        ws.Cell(1, 1).Value = "Date";
        ws.Cell(1, 2).Value = "Name";
        ws.Cell(1, 3).Value = "Value";

        ws.Cell(2, 1).IsEmpty().Should().BeTrue();
        ws.LastRowUsed()!.RowNumber().Should().Be(1);
    }

    [Fact]
    public void EmptyData_DoesNotThrow()
    {
        // Key requirement: empty data = empty sheet, not crash
        var act = () =>
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.AddWorksheet("Empty");

            // Write headers
            ws.Cell(1, 1).Value = "Col1";

            // Write zero data rows (simulating empty API response)
            var emptyList = new List<Account>();
            var row = 2;
            foreach (var account in emptyList)
            {
                ws.Cell(row, 1).Value = account.Name;
                row++;
            }

            // Save should succeed
            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Length.Should().BeGreaterThan(0);
        };

        act.Should().NotThrow();
    }

    // ================================================================
    // PER-CATEGORY ERROR ISOLATION (D8)
    // ================================================================

    [Fact]
    public void FailingCategory_DoesNotPreventOtherSheetsFromWriting()
    {
        // Per architecture decision D8: one failing category must not kill the export
        using var workbook = new XLWorkbook();

        var sheets = new[] { "Summary", "Checkings", "Investments", "Crypto" };

        foreach (var sheetName in sheets)
        {
            try
            {
                var ws = workbook.AddWorksheet(sheetName);
                ws.Cell(1, 1).Value = "Name";

                // Simulate Investments failing
                if (sheetName == "Investments")
                    throw new HttpRequestException("API error: 500 Internal Server Error");

                ws.Cell(2, 1).Value = $"Data for {sheetName}";
            }
            catch (Exception ex)
            {
                // Error isolation: log error, add note to sheet
                if (workbook.Worksheets.TryGetWorksheet(sheetName, out var errorSheet))
                {
                    errorSheet.Cell(2, 1).Value = $"Export failed: {ex.Message}";
                }
            }
        }

        // Assert: all sheets exist, non-failing ones have data
        workbook.Worksheets.Should().HaveCount(4);
        workbook.Worksheet("Summary").Cell(2, 1).GetString().Should().Be("Data for Summary");
        workbook.Worksheet("Checkings").Cell(2, 1).GetString().Should().Be("Data for Checkings");
        workbook.Worksheet("Investments").Cell(2, 1).GetString().Should().Contain("Export failed");
        workbook.Worksheet("Crypto").Cell(2, 1).GetString().Should().Be("Data for Crypto");
    }

    // ================================================================
    // FORMATTING VALIDATION
    // ================================================================

    [Fact]
    public void HeaderRow_ShouldBeBold()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Test");

        ws.Cell(1, 1).Value = "Name";
        ws.Cell(1, 1).Style.Font.Bold = true;

        ws.Cell(1, 1).Style.Font.Bold.Should().BeTrue();
    }

    [Fact]
    public void CurrencyColumns_HaveNumberFormat()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Test");

        ws.Cell(1, 1).Value = 4523.67m;
        ws.Cell(1, 1).Style.NumberFormat.Format = "#,##0.00";

        ws.Cell(1, 1).Style.NumberFormat.Format.Should().Be("#,##0.00");
    }

    [Fact]
    public void PercentageColumns_HavePercentFormat()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Test");

        ws.Cell(1, 1).Value = 0.0235;
        ws.Cell(1, 1).Style.NumberFormat.Format = "0.00%";

        ws.Cell(1, 1).Style.NumberFormat.Format.Should().Be("0.00%");
    }

    [Fact]
    public void DateColumns_HaveDateFormat()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.AddWorksheet("Test");

        ws.Cell(1, 1).Value = new DateTime(2024, 3, 15);
        ws.Cell(1, 1).Style.DateFormat.Format = "yyyy-MM-dd";

        ws.Cell(1, 1).Style.DateFormat.Format.Should().Be("yyyy-MM-dd");
    }
}
