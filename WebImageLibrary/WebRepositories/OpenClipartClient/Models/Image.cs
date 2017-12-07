using System.Collections.Generic;
using Newtonsoft.Json;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Models
{
    class Image
    {
        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("uploader")]
        public string Uploader { get; set; }

        [JsonProperty("total_favorites")]
        public int TotalFavorites { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("tags")]
        public string Tags { get; set; }

        [JsonProperty("tags_array")]
        public IList<string> TagsArray { get; set; }

        [JsonProperty("svg_filesize")]
        public int SvgFilesize { get; set; }

        [JsonProperty("downloaded_by")]
        public int DownloadedBy { get; set; }

        [JsonProperty("detail_link")]
        public string DetailLink { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("created")]
        public string Created { get; set; }

        [JsonProperty("svg")]
        public ImageLinks ImageLinks { get; set; }

        [JsonProperty("dimensions")]
        public Dimensions Dimensions { get; set; }
    }
}
