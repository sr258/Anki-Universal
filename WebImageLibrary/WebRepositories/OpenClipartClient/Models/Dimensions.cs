using Newtonsoft.Json;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Models
{
    class Dimensions
    {
        [JsonProperty("png_thumb")]
        public Dimension PngThumb { get; set; }

        [JsonProperty("png_full_lossy")]
        public Dimension PngFullLossy { get; set; }
    }
}