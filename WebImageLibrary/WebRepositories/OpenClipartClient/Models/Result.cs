using System.Collections.Generic;
using Newtonsoft.Json;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Models
{
    class Result
    {
        [JsonProperty("msg")]
        public string Message { get; set; }

        [JsonProperty("info")]
        public ResultInfo Info { get; set; }

        [JsonProperty("payload")]
        public IList<Image> Payload { get; set; }
    }
}
