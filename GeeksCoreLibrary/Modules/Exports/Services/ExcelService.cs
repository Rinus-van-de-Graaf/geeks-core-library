using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Modules.Exports.Enumerations;
using GeeksCoreLibrary.Modules.Exports.Interfaces;
using GeeksCoreLibrary.Modules.Exports.Models;
using Newtonsoft.Json.Linq;

namespace GeeksCoreLibrary.Modules.Exports.Services
{
    public class ExcelService : IExcelService, IScopedService
    {
        /// <inheritdoc />
        public byte[] JsonArrayToExcel(JArray data, string sheetName = "Data", string columnNameDelimiter = "_")
        {
            using var memoryStream = new MemoryStream();

            using (var spreadsheetDocument = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook))
            {
                var spreadsheetDocumentReferences = PrepareSpreadsheetDocument(spreadsheetDocument, sheetName);

                if (data != null)
                {
                    // Add column names.
                    var rowColumnNames = new List<object>();

                    foreach (var jsonToken in (JObject)data.First)
                    {
                        rowColumnNames.Add(jsonToken.Key);
                    }

                    AddRow(rowColumnNames, 1, spreadsheetDocumentReferences);

                    // Add data.
                    uint currentRow = 2;

                    foreach (var jToken in data)
                    {
                        var jsonObject = (JObject)jToken;
                        /*var rowColumnValues = new List<object>();

                        foreach (var jsonToken in jsonObject)
                        {
                            rowColumnValues.Add(jsonToken.Value);
                        }

                        AddRow(rowColumnValues, currentRow, spreadsheetDocumentReferences);*/

                        AddJsonObjectToRow(spreadsheetDocumentReferences, currentRow, 0, jsonObject, columnNameDelimiter);

                        currentRow++;
                    }

                    // Add filters on the columns.
                    var autoFilter = new AutoFilter
                    {
                        Reference = StringValue.FromString($"A1:{GetColumnNameFromIndex(rowColumnNames.Count - 1)}{currentRow - 1}")
                    };

                    spreadsheetDocumentReferences.Worksheet.Append(autoFilter);
                }
            }

            return memoryStream.ToArray();
        }

        private uint AddJsonObjectToRow(SpreadsheetDocumentReferencesModel spreadsheet, uint rowNumber, uint columnNumber, JObject jsonObject, string columnNameDelimiter = "_", string columnPrefix = "")
        {
            var subArrays = new Dictionary<string, JArray>();

            var worksheetRow = new Row { RowIndex = UInt32Value.FromUInt32(rowNumber) };
            foreach (var jsonProperty in jsonObject.Properties())
            {
                var dataCell = new Cell
                {
                    CellReference = GetColumnNameFromIndex(columnNumber) + rowNumber
                };
                
                switch (jsonProperty.Value.Type)
                {
                    case JTokenType.Object:
                        // If the property contains another object, add that object as extra columns with a prefix.
                        // So for example, if this property is called "address" and it contains an object with the properties "street" and "housenumber",
                        // then the columns "address_street" and "address_housenumber" will be added with the values from the sub properties.
                        AddJsonObjectToRow(spreadsheet, rowNumber, columnNumber, (JObject)jsonProperty.Value, columnNameDelimiter, $"{columnPrefix}{jsonProperty.Name}{columnNameDelimiter}");
                        break;
                    case JTokenType.Array:
                        // If the property contains an array, save that array to remember it for later. After the current for each all these arrays will be added.
                        // This needs to be done because we need to know which the biggest array is, because we need to add that many rows to Excel.
                        subArrays.Add(jsonProperty.Name, (JArray)jsonProperty.Value);
                        // Reduce the cell number counter, because we won't be adding a column for this property yet and if we don't reduce it, we will have an empty column.
                        columnNumber -= 1;
                        break;
                    case JTokenType.Null:
                        dataCell.CellValue = new CellValue(String.Empty);
                        dataCell.DataType = CellValues.String;
                        break;
                    case JTokenType.Date:
                        var dateTime = jsonProperty.Value.Value<DateTime>();
                        dataCell.CellValue = new CellValue(dateTime);
                        dataCell.DataType = CellValues.Date;

                        if (dateTime is { Hour: 0, Minute: 0, Second: 0 })
                        {
                            // No time part.
                            dataCell.StyleIndex = (int)ExcelWorkbookStyles.Date;
                        }
                        else
                        {
                            // Include time part.
                            dataCell.StyleIndex = (int)ExcelWorkbookStyles.DateTime;
                        }

                        break;
                    case JTokenType.Integer:
                        dataCell.CellValue = new CellValue(jsonProperty.Value.Value<int>());
                        dataCell.DataType = CellValues.Number;
                        dataCell.StyleIndex = (int)ExcelWorkbookStyles.Default;
                        break;
                    case JTokenType.Float:
                        dataCell.CellValue = new CellValue(jsonProperty.Value.Value<decimal>());
                        dataCell.DataType = CellValues.Number;
                        dataCell.StyleIndex = (int)ExcelWorkbookStyles.Default;
                        break;
                    default:
                        dataCell.CellValue = new CellValue(jsonProperty.Value.ToString());
                        break;
                }

                worksheetRow.Append(dataCell);
            }

            // Add the row now.
            spreadsheet.SheetData.Append(worksheetRow);

            // Handle all arrays that we saved in the dictionary and add rows and columns for the values in the arrays.
            var newRowNumber = rowNumber;
            var startColumnNumber = columnNumber;
            foreach (var subArray in subArrays)
            {
                var arrayRowNumber = rowNumber;
                var amountOfProperties = 0U;

                var previousRow = worksheetRow;
                var currentRow = new Row { RowIndex = UInt32Value.FromUInt32(arrayRowNumber) };
                foreach (var jToken in subArray.Value)
                {
                    var jsonArrayObject = (JObject)jToken;

                    // Keep track of the object with the highest amount of properties, so we don't put values in the wrong cells if the next iteration has a different amount of properties.
                    var currentAmountOfProperties = (uint)jsonArrayObject.Properties().Count();
                    if (currentAmountOfProperties > amountOfProperties) amountOfProperties = currentAmountOfProperties;

                    arrayRowNumber += 1;

                    // If we started a new row, remember the index of that row for later and copy the required values from the previous row.
                    if (arrayRowNumber > newRowNumber)
                    {
                        newRowNumber = arrayRowNumber;
                        for (var i = 0; i < startColumnNumber; i++)
                        {
                            var previousRowCell = (Cell)previousRow.ChildElements[1];
                            currentRow.Append(new Cell
                            {
                                CellValue = new CellValue(previousRowCell.CellValue?.Text ?? String.Empty),
                                DataType = previousRowCell.DataType,
                                StyleIndex = previousRowCell.StyleIndex
                            });
                        }

                        spreadsheet.SheetData.Append(currentRow);
                        previousRow = currentRow;
                        currentRow = new Row { RowIndex = UInt32Value.FromUInt32(arrayRowNumber) };;
                    }

                    AddJsonObjectToRow(spreadsheet, arrayRowNumber, startColumnNumber, jsonArrayObject, columnNameDelimiter, $"{columnPrefix}{subArray.Key}{columnNameDelimiter}");

                    // Reset the start cell number for the next iteration.
                    startColumnNumber -= currentAmountOfProperties;
                }

                startColumnNumber = columnNumber + amountOfProperties;
            }

            return newRowNumber;
        }

        /// <summary>
        /// Prepare the spreadsheet document.
        /// </summary>
        /// <param name="spreadsheetDocument">The spreadsheet document that needs to be filled.</param>
        /// <param name="sheetName">The name of the sheet.</param>
        /// <returns></returns>
        private SpreadsheetDocumentReferencesModel PrepareSpreadsheetDocument(SpreadsheetDocument spreadsheetDocument, string sheetName)
        {
            var spreadsheetDocumentReferences = new SpreadsheetDocumentReferencesModel();

            // WorkbookPart > Workbook > Sheets > Sheet
            var workbookPart = spreadsheetDocument.AddWorkbookPart();
            var worksheetPart = spreadsheetDocument.WorkbookPart.AddNewPart<WorksheetPart>();
            var fileVersion = new FileVersion();
            var sheet = new Sheet();
            var sharedStringTablePart = spreadsheetDocument.WorkbookPart.AddNewPart<SharedStringTablePart>();
            sharedStringTablePart.SharedStringTable = new SharedStringTable();

            sheet.Name = sheetName;
            sheet.SheetId = 1;
            sheet.Id = workbookPart.GetIdOfPart(worksheetPart);

            fileVersion.ApplicationName = "Microsoft Office Excel";

            worksheetPart.Worksheet = new Worksheet(new SheetData());
            worksheetPart.Worksheet.Save();

            spreadsheetDocument.WorkbookPart.Workbook = new Workbook(fileVersion, new Sheets(sheet));
            spreadsheetDocument.WorkbookPart.Workbook.Save();

            // Store the references for quick access later.
            spreadsheetDocumentReferences.Sheet = sheet;
            spreadsheetDocumentReferences.WorkbookPart = spreadsheetDocument.WorkbookPart;
            spreadsheetDocumentReferences.Worksheet = worksheetPart.Worksheet;
            spreadsheetDocumentReferences.SheetData = spreadsheetDocumentReferences.Worksheet.GetFirstChild<SheetData>();
            spreadsheetDocumentReferences.SharedStringTable = spreadsheetDocumentReferences.WorkbookPart.SharedStringTablePart.SharedStringTable;
            spreadsheetDocumentReferences.LastSharedStringIndex = spreadsheetDocumentReferences.SharedStringTable.Elements<SharedStringItem>().Count() - 1; // Used to keep track of the index of the last shared string while adding rows.

            // Set style sheet.
            var workbookStylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            workbookStylesPart.Stylesheet = CreateStylesheet();
            workbookStylesPart.Stylesheet.Save();

            return spreadsheetDocumentReferences;
        }

        private static Stylesheet CreateStylesheet()
        {
            var result = new Stylesheet();

            // Built-in formats range from 0 to 163, so have to start at 164.
            var dateFormat = new NumberingFormat
            {
                NumberFormatId = UInt32Value.FromUInt32(164),
                FormatCode = StringValue.FromString("yyyy-mm-dd")
            };
            var dateTimeFormat = new NumberingFormat
            {
                NumberFormatId = UInt32Value.FromUInt32(165),
                FormatCode = StringValue.FromString("yyyy-mm-dd HH:mm:ss")
            };

            result.NumberingFormats ??= new NumberingFormats();
            result.NumberingFormats.AppendChild(dateFormat);
            result.NumberingFormats.AppendChild(dateTimeFormat);
            
            // Create cell formats that can be referenced by style index.
            var cellFormats = new CellFormats();

            // Index 0 - Default style that works for most things, but not strings (apparently).
            cellFormats.Append(new CellFormat
            {
                BorderId = 0,
                FillId = 0,
                FontId = 0,
                NumberFormatId = 0,
                FormatId = 0,
                ApplyNumberFormat = true
            });

            // Index 1 - DateTime (only date part).
            cellFormats.Append(new CellFormat
            {
                BorderId = 0,
                FillId = 0,
                FontId = 0,
                NumberFormatId = 164,
                FormatId = 0,
                ApplyNumberFormat = true
            });

            // Index 1 - DateTime (date and time).
            cellFormats.Append(new CellFormat
            {
                BorderId = 0,
                FillId = 0,
                FontId = 0,
                NumberFormatId = 165,
                FormatId = 0,
                ApplyNumberFormat = true
            });

            result.Append(cellFormats);

            return result;
        }

        /// <summary>
        /// Get the name of the column.
        /// Limited to index 0 - 675 / A - ZZ.
        /// </summary>
        /// <param name="columnIndex">THe index of the column.</param>
        /// <returns></returns>
        private static string GetColumnNameFromIndex(uint columnIndex)
        {
            string columnName;

            if (columnIndex >= 26) // Two letter column.
            {
                var tempIndex = Convert.ToUInt32(Math.Floor(columnIndex / 26d) - 1);
                columnName = GetColumnNameFromIndex(tempIndex);
                columnName += Char.ConvertFromUtf32((int)(columnIndex - ((tempIndex + 1) * 26) + 65));
            }
            else // One letter column.
            {
                columnName = ((char)(columnIndex + 65)).ToString();
            }

            return columnName;
        }

        /// <summary>
        /// Add a row to the Excel file.
        /// </summary>
        /// <param name="rowColumnValues">The values of the columns.</param>
        /// <param name="rowNumber">THe number of the row.</param>
        /// <param name="spreadsheetDocumentReferences">The references in the spreadsheet document.</param>
        private static void AddRow(List<object> rowColumnValues, uint rowNumber, SpreadsheetDocumentReferencesModel spreadsheetDocumentReferences)
        {
            if (spreadsheetDocumentReferences.Sheet == null) return;

            var row = new Row
            {
                RowIndex = rowNumber
            };

            for (var i = 0U; i < rowColumnValues.Count; i++)
            {
                var cell = new Cell();
                cell.CellReference = GetColumnNameFromIndex(i) + rowNumber;

                var columnType = rowColumnValues[i].GetType().ToString().ToLower();
                if (rowColumnValues[i] is JValue jsonValue) // If the type is JValue get the original type.
                {
                    columnType = jsonValue.Type.ToString().ToLower();
                }

                switch (columnType)
                {
                    case "system.int32":
                    case "system.int64":
                    case "system.double":
                    case "system.sbyte":
                    case "integer":
                    case "float":
                        cell.CellValue = new CellValue(rowColumnValues[0].ToString().Replace(",", "."));
                        cell.DataType = CellValues.Number;
                        break;

                    case "system.datetime":
                        var dateTimeEpoch = new DateTime(1900, 1, 1, 0, 0, 0, 0);
                        var dateTime = Convert.ToDateTime(rowColumnValues[i]);
                        var timeSpan = dateTime - dateTimeEpoch;
                        double excelDateTime;

                        if (timeSpan.Days >= 59)
                        {
                            excelDateTime = timeSpan.TotalDays + 2.0;
                        }
                        else
                        {
                            excelDateTime = timeSpan.TotalDays + 1.0;
                        }

                        cell.StyleIndex = 2;
                        cell.CellValue = new CellValue(excelDateTime.ToString().Replace(",", "."));
                        break;
                    default:
                        spreadsheetDocumentReferences.SharedStringTable.Append(new SharedStringItem(new Text(rowColumnValues[i].ToString())));
                        spreadsheetDocumentReferences.LastSharedStringIndex++;
                        cell.CellValue = new CellValue(spreadsheetDocumentReferences.LastSharedStringIndex.ToString());
                        cell.DataType = CellValues.SharedString;
                        break;
                }

                row.Append(cell);
            }

            spreadsheetDocumentReferences.SheetData.Append(row);
        }
    }
}
