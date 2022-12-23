using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GeeksCoreLibrary.Components.Configurator.Models
{
    public class ConfigurationsModel
    {
        /// <summary>
        /// Gets or sets the name of the configurator.
        /// </summary>
        [JsonPropertyName("configurator")]
        public string Configurator { get; set; }

        /// <summary>
        /// Gets or sets the URL of the image.
        /// </summary>
        [JsonPropertyName("image")]
        public string Image { get; set; }

        /// <summary>
        /// Gets or sets the amount of times the user wants to order this configuration.
        /// </summary>
        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Gets or sets all items/steps. The keys are either the index or the ID of the item (depending on which model was sent via javascript).
        /// </summary>
        /// 
        [JsonPropertyName("items")]
        public Dictionary<string, StepsModel> Items { get; set; }

        /// <summary>
        /// Gets or sets Custom variables
        /// </summary>
        /// 
        [JsonPropertyName("customValues")]
        public Dictionary<string, string> CustomValues { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("qsItems")]
        public Dictionary<string, string> QueryStringItems { get; set; } = new Dictionary<string, string>();
    }
}
