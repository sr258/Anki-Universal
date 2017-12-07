using Newtonsoft.Json;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Models
{
    class ImageLinks
    {
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("png_thumb")]
        public string PngThumb { get; set; }

        [JsonProperty("png_full_lossy")]
        public string PngFullLossy { get; set; }

        [JsonProperty("png_2400px")]
        public string Png2400px { get; set; }
    }
}