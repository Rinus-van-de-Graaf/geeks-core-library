﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Extensions;
using GeeksCoreLibrary.Core.Interfaces;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Modules.DataSelector.Helpers;
using GeeksCoreLibrary.Modules.DataSelector.Models;

namespace GeeksCoreLibrary.Modules.DataSelector.Services;

public class NewDataSelectorsService : IScopedService
{
    private readonly IWiserItemsService wiserItemsService;

    public NewDataSelectorsService(IWiserItemsService wiserItemsService)
    {
        this.wiserItemsService = wiserItemsService;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="connections"></param>
    /// <param name="previousConnectionRow">The connection that is adding the new connections.</param>
    /// <param name="iterationCounts"></param>
    /// <returns></returns>
    private async Task<IList<ConnectionForQuery>> CreateQueryJoinsAsync(ICollection<Connection> connections, ConnectionRow previousConnectionRow, int[] iterationCounts)
    {
        if (connections == null || !connections.Any())
        {
            return null;
        }

        var result = new List<ConnectionForQuery>();

        foreach (var connection in connections)
        {
            if (connection?.ConnectionRows == null || connection.ConnectionRows.Length == 0) continue;

            foreach (var connectionRow in connection.ConnectionRows)
            {
                if (connectionRow == null) continue;

                var connectionForQuery = new ConnectionForQuery();
                var joinStringBuilder = new StringBuilder();

                // Always create a wiser_item JOIN. Determine if parent_item_id is used or if wiser_itemlink is used.
                var connectionIsParent = connectionRow.Modes.Contains("up", StringComparer.OrdinalIgnoreCase);
                var sourceEntityType = connectionIsParent
                    ? connectionRow.EntityName
                    : previousConnectionRow.EntityName;
                var destinationEntityType = connectionIsParent
                    ? previousConnectionRow.EntityName
                    : connectionRow.EntityName;
                
                var tablePrefix = await wiserItemsService.GetTablePrefixForEntityAsync(connectionRow.EntityName);
                var linkTablePrefix = await wiserItemsService.GetTablePrefixForLinkAsync(connectionRow.TypeNumber, sourceEntityType, destinationEntityType);
                var joinPrefix = connectionRow.Modes.Contains("optional", StringComparer.OrdinalIgnoreCase) ? "LEFT " : String.Empty;
                var tableAlias = $"item_{String.Join("_", iterationCounts)}";
                var linkTableAlias = $"itemlink_{String.Join("_", iterationCounts)}";

                string previousItemTableAlias;
                if (iterationCounts.Length == 1)
                {
                    previousItemTableAlias = "item_main";
                }
                else
                {
                    var iterationCountsPrevious = new int[iterationCounts.Length - 1];
                    Array.Copy(iterationCounts, iterationCountsPrevious, iterationCountsPrevious.Length);
                    previousItemTableAlias = $"item_{String.Join("_", iterationCountsPrevious)}";
                }

                var linkSettings = await wiserItemsService.GetLinkTypeSettingsAsync(connectionRow.TypeNumber, sourceEntityType, destinationEntityType);
                if (linkSettings.UseItemParentId)
                {
                    var sourceColumnName = connectionIsParent ? "id" : "parent_item_id";
                    var destinationColumnName = connectionIsParent ? "parent_item_id" : "id";

                    joinStringBuilder.AppendLine($"{joinPrefix}JOIN `{tablePrefix}{WiserTableNames.WiserItem}` AS `{tableAlias}` ON `{tableAlias}`.`{destinationColumnName}` = `{previousItemTableAlias}`.`{sourceColumnName}`");
                }
                else
                {
                    var sourceColumnName = connectionIsParent ? "destination_item_id" : "item_id";
                    var destinationColumnName = connectionIsParent ? "item_id" : "destination_item_id";

                    joinStringBuilder.AppendLine($"{linkTablePrefix}JOIN `{tablePrefix}{WiserTableNames.WiserItemLink}` AS `{linkTableAlias}` ON `{linkTableAlias}`.`{destinationColumnName}` = `{previousItemTableAlias}`.`id`");
                    joinStringBuilder.AppendLine($"{linkTablePrefix}JOIN `{tablePrefix}{WiserTableNames.WiserItem}` AS `{tableAlias}` ON `{tableAlias}`.`id` = `{linkTableAlias}`.`{sourceColumnName}`");
                }

                connectionForQuery.JoinQueryPart = joinStringBuilder.ToString();
                connectionForQuery.Fields = await CreateQueryJoinsForConnectionFieldsAsync(connectionRow, iterationCounts);
            }
        }

        return result;
    }

    private async Task<List<FieldForQuery>> CreateQueryJoinsForConnectionFieldsAsync(ConnectionRow connectionRow, int[] iterationCounts)
    {
        var result = new List<FieldForQuery>(connectionRow.Fields.Length);

        var tablePrefix = await wiserItemsService.GetTablePrefixForEntityAsync(connectionRow.EntityName);
        var joinPrefix = connectionRow.Modes.Contains("optional", StringComparer.OrdinalIgnoreCase) ? "LEFT " : String.Empty;

        var fieldIteration = 0;
        foreach (var field in connectionRow.Fields)
        {
            fieldIteration++;

            var fieldForQuery = new FieldForQuery();

            // Check which table should be used, wiser_item or wiser_itemdetail.
            var tableName =
                field.FieldName.InList("id", "idencrypted", "unique_uuid", "itemtitle", "changed_on", "changed_by")
                ? WiserTableNames.WiserItem
                : WiserTableNames.WiserItemDetail;

            var languageCodes = new List<string>(field.LanguageCodes ?? Array.Empty<string>());
            if (languageCodes.Count == 0)
            {
                languageCodes.Add(String.Empty);
            }

            if (tableName.Equals(WiserTableNames.WiserItem))
            {
                var tableAlias = $"item_{String.Join("_", iterationCounts)}";
                fieldForQuery.SelectQueryPart = $"`{tableAlias}`.`{field.FieldName}` AS `{field.FieldAlias}`";
            }
            else
            {
                foreach (var tableAlias in languageCodes.Select(languageCode => FieldHelpers.CreateTableJoinAlias(field, iterationCounts, fieldIteration, languageCode)))
                {
                    fieldForQuery.SelectQueryPart = $"CONCAT_WS('', `{tableAlias}`.`value`, `{tableAlias}`.`long_value`) AS `{field.FieldAlias}`";
                    fieldForQuery.JoinQueryPart = $"{joinPrefix}JOIN `{tablePrefix}{tableName}` AS `{tableAlias}`";
                }
            }

            result.Add(fieldForQuery);
        }

        return result;
    }
}