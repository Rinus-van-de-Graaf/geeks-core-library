namespace GeeksCoreLibrary.Core.Enums;

public enum EntityStoreTypes
{
    /// <summary>
    /// Items of this type will be saved in the normal Wiser item tables.
    /// </summary>
    Normal,
    /// <summary>
    /// Items of this type will be saved in the document store.
    /// </summary>
    DocumentStore,
    /// <summary>
    /// Items are both saved in the normal Wiser item tables, and in the document store.
    /// </summary>
    Hybrid
}