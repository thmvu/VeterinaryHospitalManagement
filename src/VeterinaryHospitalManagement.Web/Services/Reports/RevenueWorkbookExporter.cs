using System.Globalization;
using System.IO.Compression;
using System.Xml;
using VeterinaryHospitalManagement.Web.Models.Enums;

namespace VeterinaryHospitalManagement.Web.Services.Reports;

public static class RevenueWorkbookExporter
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Create(RevenueReport report, TimeSpan localOffset)
    {
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
                writer.WriteStartElement("sheet", SpreadsheetNs); writer.WriteAttributeString("name", "Doanh thu"); writer.WriteAttributeString("sheetId", "1"); writer.WriteAttributeString("r", "id", RelationshipNs, "rId1"); writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/_rels/workbook.xml.rels", writer =>
            {
                writer.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                writer.WriteStartElement("Relationship"); writer.WriteAttributeString("Id", "rId1"); writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"); writer.WriteAttributeString("Target", "worksheets/sheet1.xml"); writer.WriteEndElement();
                writer.WriteEndElement();
            });

            WriteXml(archive, "xl/worksheets/sheet1.xml", writer => WriteWorksheet(writer, report, localOffset));
        }
        return stream.ToArray();
    }

    private static void WriteWorksheet(XmlWriter writer, RevenueReport report, TimeSpan localOffset)
    {
        writer.WriteStartElement("worksheet", SpreadsheetNs);
        writer.WriteStartElement("cols", SpreadsheetNs);
        WriteColumn(writer, 1, 1, 22); WriteColumn(writer, 2, 2, 22); WriteColumn(writer, 3, 4, 28); WriteColumn(writer, 5, 5, 18); WriteColumn(writer, 6, 6, 18);
        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNs);
        WriteTextRow(writer, 1, ("A", "Báo cáo doanh thu"));
        WriteTextRow(writer, 2, ("A", $"Từ {report.From:dd/MM/yyyy} đến {report.To:dd/MM/yyyy} (giờ Việt Nam)"));
        WriteTextRow(writer, 4, ("A", "Ngày thanh toán"), ("B", "Số hóa đơn"), ("C", "Chủ nuôi"), ("D", "Thú cưng"), ("E", "Phương thức"), ("F", "Số tiền (VND)"));

        for (var index = 0; index < report.Rows.Count; index++)
        {
            var row = report.Rows[index];
            var rowNumber = index + 5;
            writer.WriteStartElement("row", SpreadsheetNs); writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
            WriteTextCell(writer, $"A{rowNumber}", row.PaidAt.ToOffset(localOffset).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            WriteTextCell(writer, $"B{rowNumber}", row.InvoiceNumber);
            WriteTextCell(writer, $"C{rowNumber}", row.OwnerName);
            WriteTextCell(writer, $"D{rowNumber}", row.PetName);
            WriteTextCell(writer, $"E{rowNumber}", row.PaymentMethod == PaymentMethod.Cash ? "Tiền mặt" : "Chuyển khoản");
            WriteNumberCell(writer, $"F{rowNumber}", row.TotalAmount);
            writer.WriteEndElement();
        }

        var totalRow = report.Rows.Count + 5;
        writer.WriteStartElement("row", SpreadsheetNs); writer.WriteAttributeString("r", totalRow.ToString(CultureInfo.InvariantCulture));
        WriteTextCell(writer, $"E{totalRow}", "Tổng doanh thu");
        WriteNumberCell(writer, $"F{totalRow}", report.TotalAmount);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteColumn(XmlWriter writer, int min, int max, int width)
    {
        writer.WriteStartElement("col", SpreadsheetNs);
        writer.WriteAttributeString("min", min.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("max", max.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static void WriteTextRow(XmlWriter writer, int rowNumber, params (string Column, string Text)[] cells)
    {
        writer.WriteStartElement("row", SpreadsheetNs); writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        foreach (var (column, text) in cells) WriteTextCell(writer, $"{column}{rowNumber}", text);
        writer.WriteEndElement();
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
