using DocumentFormat.OpenXml.Spreadsheet;

namespace GeeksCoreLibrary.Modules.Exports.Enumerations;

/// <summary>
/// Indexes that can be used as the value for <see cref="Cell.StyleIndex"/> for a <see cref="Cell"/> object.
/// Note: For string values, set <see cref="Cell.StyleIndex"/> to <see langword="null"/> instead of <see cref="Default"/>.
/// </summary>
public enum ExcelWorkbookStyles
{
    /// <summary>
    /// A style sheet for dates without the time part.
    /// </summary>
    Date = 0,
    /// <summary>
    /// A style sheet for dates that include the time part.
    /// </summary>
    DateTime = 1
}