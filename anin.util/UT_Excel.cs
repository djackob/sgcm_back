using ClosedXML.Excel;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System.Globalization;

namespace anin.util
{
    public class UT_Excel
    {
        private static readonly CultureInfo _culture = new CultureInfo("es-PE");

        // ================= XLSX =================
        public static List<Dictionary<string, object>> LeerXlsx(Stream stream)
        {
            var resultado = new List<Dictionary<string, object>>();

            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed();

            var headers = rows.First()
                              .Cells()
                              .Select(c => c.GetValue<string>()?.Trim())
                              .ToList();

            foreach (var row in rows.Skip(1))
            {
                var obj = new Dictionary<string, object>();

                for (int i = 0; i < headers.Count; i++)
                {
                    var cell = row.Cell(i + 1);
                    obj[headers[i]] = ObtenerValorClosedXML(cell);
                }

                resultado.Add(obj);
            }

            return resultado;
        }

        // ================= XLS =================
        public static List<Dictionary<string, object>> LeerXls(Stream stream)
        {
            var resultado = new List<Dictionary<string, object>>();

            var workbook = new HSSFWorkbook(stream);
            var sheet = workbook.GetSheetAt(0);

            var headerRow = sheet.GetRow(0);
            int cellCount = headerRow.LastCellNum;

            var headers = new List<string>();
            for (int i = 0; i < cellCount; i++)
            {
                headers.Add(headerRow.GetCell(i)?.ToString()?.Trim());
            }

            for (int i = 1; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var obj = new Dictionary<string, object>();

                for (int j = 0; j < cellCount; j++)
                {
                    var cell = row.GetCell(j);
                    obj[headers[j]] = ObtenerValorNPOI(cell);
                }

                resultado.Add(obj);
            }

            return resultado;
        }

        // ================= CLOSEDXML =================
        private static object ObtenerValorClosedXML(IXLCell cell)
        {
            if (cell == null || cell.IsEmpty())
                return null;

            if (cell.DataType == XLDataType.Number)
                return cell.GetDouble();

            if (cell.DataType == XLDataType.DateTime)
                return cell.GetDateTime();

            var texto = cell.GetValue<string>();

            // Intentar convertir texto a número con cultura
            if (decimal.TryParse(texto, NumberStyles.Any, _culture, out decimal numero))
                return numero;

            return texto;
        }

        // ================= NPOI =================
        private static object ObtenerValorNPOI(ICell cell)
        {
            if (cell == null)
                return null;

            switch (cell.CellType)
            {
                case CellType.Numeric:
                    if (DateUtil.IsCellDateFormatted(cell))
                        return cell.DateCellValue;

                    return cell.NumericCellValue;

                case CellType.String:
                    var texto = cell.StringCellValue;

                    if (decimal.TryParse(texto, NumberStyles.Any, _culture, out decimal numero))
                        return numero;

                    return texto;

                case CellType.Boolean:
                    return cell.BooleanCellValue;

                case CellType.Formula:
                    return cell.ToString();

                default:
                    return cell.ToString();
            }
        }
    }
}
