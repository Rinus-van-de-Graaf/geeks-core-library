using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using GeeksCoreLibrary.Modules.Databases.Models;
using MySqlX.XDevAPI;
using MySqlX.XDevAPI.Common;
using MySqlX.XDevAPI.CRUD;

namespace GeeksCoreLibrary.Modules.Databases.Interfaces;

public interface IDocumentStoreConnection : IDisposable
{
    /// <summary>
    /// Gets the name of the database that the connection is currently connected to.
    /// </summary>
    string ConnectedDatabase { get; }

    /// <summary>
    /// Creates a new collection in the document store.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="indexes">The indexes the store will get.</param>
    /// <returns>The newly created <see cref="Collection"/>.</returns>
    Collection CreateCollection(string name, List<(string Name, DocumentStoreIndexModel)> indexes = null);

    /// <summary>
    /// Gets a document collection from the document store.
    /// </summary>
    /// <param name="name">The name of the collection.</param>
    /// <param name="validateExistence">Whether to throw an exception if the collection is missing.</param>
    /// <returns>A <see cref="Collection"/> of documents.</returns>
    Collection GetCollection(string name, bool validateExistence = false);

    /// <summary>
    /// Retrieves documents from a collection, filtered by a condition.
    /// </summary>
    /// <param name="collectionName"></param>
    /// <param name="condition"></param>
    /// <returns></returns>
    Task<ReadOnlyCollection<DbDoc>> GetDocumentsAsync(string collectionName, string condition);

    /// <summary>
    /// Retrieves a single document from a collection by ID.
    /// </summary>
    /// <param name="collectionName"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    DbDoc GetDocumentById(string collectionName, object id);

    /// <summary>
    /// Add a parameter that will be used in the <see cref="FindStatement"/> to safely use user input in a query.
    /// </summary>
    /// <param name="key"></param>
    /// <param name="value"></param>
    void AddParameter(string key, object value);

    /// <summary>
    /// Clear all previously added parameters.
    /// </summary>
    void ClearParameters();

    /// <summary>
    /// Inserts a new document, or updates an existing one. Note that if the item with the given ID doesn't exist yet,
    /// the new item will be inserted with the given ID.
    /// </summary>
    /// <param name="collectionName">The name of the collection to insert the new item into.</param>
    /// <param name="item">The item that will be serialized.</param>
    /// <param name="id">The document ID. If the item doesn't exist yet, this ID will be assigned to that document.</param>
    /// <returns></returns>
    Result InsertOrUpdateRecord(string collectionName, object item, ulong id = 0);

    /// <summary>
    /// Attempts to start a transaction.
    /// </summary>
    void StartTransaction();

    /// <summary>
    /// Commits the active transaction.
    /// </summary>
    void CommitTransaction();

    /// <summary>
    /// Rolls back all changes in the active transaction.
    /// </summary>
    void RollbackTransaction();

    /// <summary>
    /// Gets a new ID for adding a new document to a collection. This is done by taking the current highest ID and increasing
    /// the value by one.
    /// </summary>
    /// <param name="collectionName"></param>
    /// <returns></returns>
    Task<ulong> GetNewIdAsync(string collectionName);
}