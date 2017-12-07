using Newtonsoft.Json;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Models
{
    class ResultInfo
    {
        [JsonProperty("results")]
        public int Results { get; set; }

        [JsonProperty("pages")]
        public int Pages { get; set; }

        [JsonProperty("current_page")]
        public int CurrentPage { get; set; }
    }
}