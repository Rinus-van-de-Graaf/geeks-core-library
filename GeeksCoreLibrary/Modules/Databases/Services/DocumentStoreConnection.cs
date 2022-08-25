using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Helpers;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Modules.Databases.Interfaces;
using GeeksCoreLibrary.Modules.Databases.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlX.XDevAPI;
using MySqlX.XDevAPI.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace GeeksCoreLibrary.Modules.Databases.Services;

public class DocumentStoreConnection : IDocumentStoreConnection, IScopedService
{
    private readonly IHttpContextAccessor httpContextAccessor;
    private readonly ILogger<DocumentStoreConnection> logger;

    private Session Session { get; set; }

    private readonly GclSettings gclSettings;

    private readonly ConcurrentDictionary<string, object> parameters = new();
    private readonly JsonSerializerSettings jsonSerializerSettings;

    /// <summary>
    /// Creates a new instance of <see cref="DocumentStoreConnection"/>.
    /// </summary>
    public DocumentStoreConnection(IOptions<GclSettings> gclSettings, IHttpContextAccessor httpContextAccessor, ILogger<DocumentStoreConnection> logger)
    {
        this.httpContextAccessor = httpContextAccessor;
        this.logger = logger;
        this.gclSettings = gclSettings.Value;

        jsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            },
            Formatting = Formatting.Indented
        };
    }

    /// <inheritdoc />
    public string ConnectedDatabase { get; private set; }

    /// <inheritdoc />
    public Collection CreateCollection(string name, List<(string Name, DocumentStoreIndexModel)> indexes)
    {
        EnsureOpenSession();

        var collection = Session.Schema.CreateCollection(name, true);
        if (indexes == null)
        {
            return collection;
        }

        // Create indexes.
        foreach (var index in indexes)
        {
            collection.CreateIndex(index.Name, JsonConvert.SerializeObject(index, jsonSerializerSettings));
        }

        return collection;
    }

    /// <inheritdoc />
    public Collection GetCollection(string name, bool validateExistence = false)
    {
        EnsureOpenSession();
        return Session.Schema.GetCollection(name, validateExistence);
    }

    /// <inheritdoc />
    public async Task<ReadOnlyCollection<DbDoc>> GetDocumentsAsync(string collectionName, string condition)
    {
        EnsureOpenSession();

        var findParameters = new DbDoc();
        if (!parameters.IsEmpty)
        {
            foreach (var parameter in parameters)
            {
                findParameters.SetValue(parameter.Key, parameter.Value);
            }
        }

        var collection = Session.Schema.GetCollection(collectionName, true);

        // Create the FindStatement first.
        var findStatement = collection.Find(condition);
        // Bind the parameters.
        foreach (var parameter in parameters)
        {
            findStatement.Bind(parameter.Key, parameter.Value);
        }

        var documents = await findStatement.ExecuteAsync();

        // Return the ReadOnlyCollection of documents.
        return documents.FetchAll();
    }

    /// <inheritdoc />
    public DbDoc GetDocumentById(string collectionName, object id)
    {
        if (id == null)
        {
            return null;
        }

        EnsureOpenSession();

        var collection = Session.Schema.GetCollection(collectionName, true);
        return collection.GetOne(id);
    }

    /// <inheritdoc />
    public void AddParameter(string key, object value)
    {
        if (parameters.ContainsKey(key))
        {
            parameters.TryRemove(key, out _);
        }

        parameters.TryAdd(key, value);
    }

    /// <inheritdoc />
    public void ClearParameters()
    {
        parameters.Clear();
    }

    /// <summary>
    /// Will make sure the Session is open and is connected to the correct Schema.
    /// </summary>
    private void EnsureOpenSession()
    {
        if (Session != null)
        {
            return;
        }

        Session = MySQLX.GetSession(gclSettings.DocumentStoreConnectionString);
        // Session has a property that points to the current schema, so there's no need to use the return value.
        Session.GetSchema(gclSettings.DocumentStoreDatabaseName);
        ConnectedDatabase = Session.Schema.Name;
    }

    /// <inheritdoc />
    public Result InsertOrUpdateRecord(string collectionName, object item, ulong id = 0)
    {
        var collection = GetCollection(collectionName);
        return collection.AddOrReplaceOne(id > 0 ? id : null, JsonConvert.SerializeObject(item, jsonSerializerSettings));
    }

    /// <inheritdoc />
    public void StartTransaction()
    {
        EnsureOpenSession();
        Session.StartTransaction();
    }

    /// <inheritdoc />
    public void CommitTransaction()
    {
        Session.Commit();
    }

    /// <inheritdoc />
    public void RollbackTransaction()
    {
        Session.Rollback();
    }

    /// <inheritdoc />
    public async Task<ulong> GetNewIdAsync(string collectionName)
    {
        var collection = GetCollection(collectionName);
        var doc = (await collection.Find().Sort("_id ASC").ExecuteAsync()).LastOrDefault();

        // The new ID is the current highest ID plus one.
        return (doc == null ? 0UL : Convert.ToUInt64(doc.Id)) + 1UL;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        logger.LogTrace($"Disposing instance of DocumentStoreConnection on URL {HttpContextHelpers.GetOriginalRequestUri(httpContextAccessor.HttpContext)}");
        Session?.Dispose();
        GC.SuppressFinalize(this);
    }
}