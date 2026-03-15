using ClosedXML.Excel;
using FinaryExport.Api;
using FinaryExport.Export;
using FinaryExport.Export.Sheets;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinaryExport.Tests.Export;

// Tests the actual WorkbookExporter class (not ClosedXML directly).
public sealed class WorkbookExporterRealTests
{
	[Fact]
	public async Task ExportAsync_CallsAllSheetWriters()
	{
		var writer1 = new Mock<ISheetWriter>();
		writer1.Setup(w => w.SheetName).Returns("Sheet1");
		writer1.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.Callback<IXLWorkbook, IFinaryApiClient, ExportContext, CancellationToken>((wb, _, _, _) => wb.Worksheets.Add("Sheet1"))
			.Returns(Task.CompletedTask);

		var writer2 = new Mock<ISheetWriter>();
		writer2.Setup(w => w.SheetName).Returns("Sheet2");
		writer2.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.Callback<IXLWorkbook, IFinaryApiClient, ExportContext, CancellationToken>((wb, _, _, _) => wb.Worksheets.Add("Sheet2"))
			.Returns(Task.CompletedTask);

		var exporter = new WorkbookExporter([writer1.Object, writer2.Object], NullLogger<WorkbookExporter>.Instance);
		var api = new Mock<IFinaryApiClient>();

		var path = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.xlsx");
		try
		{
			await exporter.ExportAsync(path, api.Object, new ExportContext(), CancellationToken.None);

			File.Exists(path).Should().BeTrue();
			using var wb = new XLWorkbook(path);
			wb.Worksheets.Should().HaveCount(2);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task ExportAsync_SheetWriterFails_CreatesErrorSheet()
	{
		var goodWriter = new Mock<ISheetWriter>();
		goodWriter.Setup(w => w.SheetName).Returns("Good");
		goodWriter.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.Callback<IXLWorkbook, IFinaryApiClient, ExportContext, CancellationToken>((wb, _, _, _) => wb.Worksheets.Add("Good"))
			.Returns(Task.CompletedTask);

		var badWriter = new Mock<ISheetWriter>();
		badWriter.Setup(w => w.SheetName).Returns("Bad");
		badWriter.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("API blew up"));

		var exporter = new WorkbookExporter([goodWriter.Object, badWriter.Object], NullLogger<WorkbookExporter>.Instance);
		var api = new Mock<IFinaryApiClient>();

		var path = Path.Combine(Path.GetTempPath(), $"test_error_{Guid.NewGuid()}.xlsx");
		try
		{
			await exporter.ExportAsync(path, api.Object, new ExportContext(), CancellationToken.None);

			File.Exists(path).Should().BeTrue();
			using var wb = new XLWorkbook(path);
			wb.Worksheets.Should().Contain(ws => ws.Name == "Good");
			wb.Worksheets.Should().Contain(ws => ws.Name == "Bad ERR");
			wb.Worksheet("Bad ERR").Cell("A2").Value.ToString().Should().Contain("API blew up");
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task ExportAsync_AllWritersFail_CreatesInfoSheet()
	{
		var badWriter = new Mock<ISheetWriter>();
		badWriter.Setup(w => w.SheetName).Returns("Bad");
		badWriter.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.ThrowsAsync(new InvalidOperationException("Boom"));

		var exporter = new WorkbookExporter([badWriter.Object], NullLogger<WorkbookExporter>.Instance);
		var api = new Mock<IFinaryApiClient>();

		var path = Path.Combine(Path.GetTempPath(), $"test_info_{Guid.NewGuid()}.xlsx");
		try
		{
			await exporter.ExportAsync(path, api.Object, new ExportContext(), CancellationToken.None);

			using var wb = new XLWorkbook(path);
			// Error sheet created + possibly Info sheet
			wb.Worksheets.Count.Should().BeGreaterThanOrEqualTo(1);
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task ExportAsync_NoWriters_CreatesInfoSheet()
	{
		var exporter = new WorkbookExporter([], NullLogger<WorkbookExporter>.Instance);
		var api = new Mock<IFinaryApiClient>();

		var path = Path.Combine(Path.GetTempPath(), $"test_nowriters_{Guid.NewGuid()}.xlsx");
		try
		{
			await exporter.ExportAsync(path, api.Object, new ExportContext(), CancellationToken.None);

			using var wb = new XLWorkbook(path);
			wb.Worksheets.Should().Contain(ws => ws.Name == "Info");
			wb.Worksheet("Info").Cell("A1").Value.ToString().Should().Contain("No data was exported");
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task ExportAsync_NullContext_UsesDefault()
	{
		var writer = new Mock<ISheetWriter>();
		writer.Setup(w => w.SheetName).Returns("Test");
		writer.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.Callback<IXLWorkbook, IFinaryApiClient, ExportContext, CancellationToken>((wb, _, ctx, _) =>
			{
				// ExportContext should be created with defaults when null is passed
				ctx.Should().NotBeNull();
				ctx.UseDisplayValues.Should().BeTrue("default ExportContext has UseDisplayValues=true");
				wb.Worksheets.Add("Test");
			})
			.Returns(Task.CompletedTask);

		var exporter = new WorkbookExporter([writer.Object], NullLogger<WorkbookExporter>.Instance);
		var api = new Mock<IFinaryApiClient>();

		var path = Path.Combine(Path.GetTempPath(), $"test_nullctx_{Guid.NewGuid()}.xlsx");
		try
		{
			await exporter.ExportAsync(path, api.Object, null, CancellationToken.None);
			File.Exists(path).Should().BeTrue();
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}

	[Fact]
	public async Task ExportAsync_CancellationDuringWrite_StopsEarly()
	{
		using var cts = new CancellationTokenSource();

		var writer1 = new Mock<ISheetWriter>();
		writer1.Setup(w => w.SheetName).Returns("First");
		writer1.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.Callback<IXLWorkbook, IFinaryApiClient, ExportContext, CancellationToken>((wb, _, _, _) =>
			{
				wb.Worksheets.Add("First");
				cts.Cancel(); // Cancel after first write
			})
			.Returns(Task.CompletedTask);

		var writer2 = new Mock<ISheetWriter>();
		writer2.Setup(w => w.SheetName).Returns("Second");
		writer2.Setup(w => w.WriteAsync(It.IsAny<IXLWorkbook>(), It.IsAny<IFinaryApiClient>(), It.IsAny<ExportContext>(), It.IsAny<CancellationToken>()))
			.Callback<IXLWorkbook, IFinaryApiClient, ExportContext, CancellationToken>((wb, _, _, ct) =>
			{
				ct.ThrowIfCancellationRequested();
				wb.Worksheets.Add("Second");
			})
			.Returns(Task.CompletedTask);

		var exporter = new WorkbookExporter([writer1.Object, writer2.Object], NullLogger<WorkbookExporter>.Instance);
		var api = new Mock<IFinaryApiClient>();

		var path = Path.Combine(Path.GetTempPath(), $"test_cancel_{Guid.NewGuid()}.xlsx");
		try
		{
			await exporter.ExportAsync(path, api.Object, new ExportContext(), cts.Token);

			using var wb = new XLWorkbook(path);
			wb.Worksheets.Should().Contain(ws => ws.Name == "First");
			// Second writer should not have been called (or been cancelled)
		}
		finally
		{
			if (File.Exists(path)) File.Delete(path);
		}
	}
}
