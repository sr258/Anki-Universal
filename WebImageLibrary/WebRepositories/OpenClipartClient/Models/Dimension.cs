using Newtonsoft.Json;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Models
{
    class Dimension
    {
        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }
    }
}