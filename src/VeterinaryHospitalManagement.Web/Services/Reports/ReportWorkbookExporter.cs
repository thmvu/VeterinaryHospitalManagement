using System.Globalization;
using System.IO.Compression;
using System.Xml;

namespace VeterinaryHospitalManagement.Web.Services.Reports;

public sealed record WorkbookCell(string? Text = null, decimal? Number = null)
{
    public static WorkbookCell FromText(string value) => new(value);
    public static WorkbookCell FromNumber(decimal value) => new(Number: value);
}

public static class ReportWorkbookExporter
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Create(
        string sheetName,
        string title,
        string period,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<WorkbookCell>> rows,
        string totalLabel,
        decimal total)
    {
        if (headers.Count == 0 || rows.Any(row => row.Count != headers.Count))
            throw new ArgumentException("Các dòng Excel phải khớp số cột tiêu đề.");

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteXml(archive, "[Content_Types].xml", writer =>
            {
                writer.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                writer.WriteStartElement("Default"); writer.WriteAttributeString("Extension", "rels"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml"); writer.WriteEndElement();
                writer.WriteStartElement("Default"); writer.WriteAttributeString("Extension", "xml"); writer.WriteAttributeString("ContentType", "application/xml"); writer.WriteEndElement();
                writer.WriteStartElement("Override"); writer.WriteAttributeString("PartName", "/xl/workbook.xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"); writer.WriteEndElement();
                writer.WriteStartElement("Override"); writer.WriteAttributeString("PartName", "/xl/worksheets/sheet1.xml"); writer.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"); writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "_rels/.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                writer.WriteStartElement("Relationship"); writer.WriteAttributeString("Id", "rId1"); writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"); writer.WriteAttributeString("Target", "xl/workbook.xml"); writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/workbook.xml", writer =>
            {
                writer.WriteStartElement("workbook", SpreadsheetNs);
                writer.WriteAttributeString("xmlns", "r", null, RelationshipNs);
                writer.WriteStartElement("sheets", SpreadsheetNs);
                writer.WriteStartElement("sheet", SpreadsheetNs); writer.WriteAttributeString("name", sheetName); writer.WriteAttributeString("sheetId", "1"); writer.WriteAttributeString("r", "id", RelationshipNs, "rId1"); writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                writer.WriteStartElement("Relationship"); writer.WriteAttributeString("Id", "rId1"); writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"); writer.WriteAttributeString("Target", "worksheets/sheet1.xml"); writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/worksheets/sheet1.xml", writer =>
                WriteWorksheet(writer, title, period, headers, rows, totalLabel, total));
        }
        return stream.ToArray();
    }

    private static void WriteWorksheet(
        XmlWriter writer,
        string title,
        string period,
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<WorkbookCell>> rows,
        string totalLabel,
        decimal total)
    {
        writer.WriteStartElement("worksheet", SpreadsheetNs);
        writer.WriteStartElement("cols", SpreadsheetNs);
        for (var index = 1; index <= headers.Count; index++) WriteColumn(writer, index);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNs);
        WriteTextRow(writer, 1, ("A", title));
        WriteTextRow(writer, 2, ("A", period));
        WriteTextRow(writer, 4, headers.Select((header, index) => (ColumnName(index + 1), header)).ToArray());

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var rowNumber = rowIndex + 5;
            writer.WriteStartElement("row", SpreadsheetNs); writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            for (var cellIndex = 0; cellIndex < headers.Count; cellIndex++)
                WriteCell(writer, $"{ColumnName(cellIndex + 1)}{rowNumber}", rows[rowIndex][cellIndex]);
            writer.WriteEndElement();
        }

        var totalRow = rows.Count + 5;
        writer.WriteStartElement("row", SpreadsheetNs); writer.WriteAttributeString("r", totalRow.ToString(CultureInfo.InvariantCulture));
        var labelColumn = Math.Max(1, headers.Count - 1);
        WriteTextCell(writer, $"{ColumnName(labelColumn)}{totalRow}", totalLabel);
        WriteNumberCell(writer, $"{ColumnName(headers.Count)}{totalRow}", total);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteColumn(XmlWriter writer, int index)
    {
        writer.WriteStartElement("col", SpreadsheetNs);
        writer.WriteAttributeString("min", index.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("max", index.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", "22");
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static string ColumnName(int number)
    {
        var result = string.Empty;
        while (number > 0)
        {
            number--;
            result = (char)('A' + number % 26) + result;
            number /= 26;
        }
        return result;
    }

    private static void WriteTextRow(XmlWriter writer, int rowNumber, params (string Column, string Text)[] cells)
    {
        writer.WriteStartElement("row", SpreadsheetNs); writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        foreach (var (column, text) in cells) WriteTextCell(writer, $"{column}{rowNumber}", text);
        writer.WriteEndElement();
    }

    private static void WriteCell(XmlWriter writer, string reference, WorkbookCell cell)
    {
        if (cell.Number.HasValue) WriteNumberCell(writer, reference, cell.Number.Value);
        else WriteTextCell(writer, reference, cell.Text ?? string.Empty);
    }

    private static void WriteTextCell(XmlWriter writer, string reference, string value)
    {
        writer.WriteStartElement("c", SpreadsheetNs); writer.WriteAttributeString("r", reference); writer.WriteAttributeString("t", "inlineStr");
        writer.WriteStartElement("is", SpreadsheetNs); writer.WriteElementString("t", SpreadsheetNs, value); writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteNumberCell(XmlWriter writer, string reference, decimal value)
    {
        writer.WriteStartElement("c", SpreadsheetNs); writer.WriteAttributeString("r", reference);
        writer.WriteElementString("v", SpreadsheetNs, value.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
    }

    private static void WriteXml(ZipArchive archive, string path, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false, CloseOutput = false });
        writer.WriteStartDocument();
        write(writer);
        writer.WriteEndDocument();
    }
}
