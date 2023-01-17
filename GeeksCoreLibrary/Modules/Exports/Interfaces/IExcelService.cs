using Newtonsoft.Json.Linq;

namespace GeeksCoreLibrary.Modules.Exports.Interfaces
{
    public interface IExcelService
    {
        /// <summary>
        /// Create an Excel file from a Json array.
        /// </summary>
        /// <param name="data">The Json array containing the information to be shown in the Excel file.</param>
        /// <param name="sheetName">The name of the worksheet in the Excel file.</param>
        /// <param name="columnNameDelimiter">
        /// Optional: The delimiter to use for columns. Default value is '_'.
        /// This will be used for sub objects. For example if an object contains a property "address" and that address is another object with street and house number,
        /// then you will get an Excel file with columns "address_street" and "address_number", if you use the default value for this parameter.
        /// </param>
        /// <returns></returns>
        byte[] JsonArrayToExcel(JArray data, string sheetName = "Data", string columnNameDelimiter = "_");
    }
}
