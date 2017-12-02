using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PixabaySharp;
using PixabaySharp.Enums;
using PixabaySharp.Utility;

namespace WebImageLibrary.WebRepositories
{
    internal class PixabayRepository : IWebRepository
    {
        private readonly PixabaySharpClient _pixabaySharpClient;

        public PixabayRepository(string apiKey)
        {
            _pixabaySharpClient = new PixabaySharpClient(apiKey);
        }

        public async Task<IEnumerable<IRepositoryImage>> Query(string query)
        {
            return await QueryPage(query, 1);
        }

        public async Task<IEnumerable<IRepositoryImage>> QueryPage(string query, int pageNr)
        {
            if (String.IsNullOrEmpty(query))
                return new List<IRepositoryImage>();
            try
            {
                Debug.WriteLine($"querying for {query}...");
                var result = await _pixabaySharpClient.QueryImagesAsync(new ImageQueryBuilder
                {
                    ImageType = ImageType.Illustration | ImageType.Vector,
                    Query = query,
                    Page = pageNr
                });

                if (result == null || result.Images == null || !result.Images.Any())
                    return new List<IRepositoryImage>();

                return result.Images.Select(i => new PixabayImage()
                {
                    Author = i.User,
                    HighResURL = i.FullHDImageURL,
                    MediumURL = i.PreviewURL,
                    SmallURL = i.WebformatURL,
                    Licence = "CC0 Creative Commons",
                    SourceURL = i.PageURL,
                    Title = i.Tags
                });
            }
            catch (Exception e)
            {
                return new List<IRepositoryImage>();
            }
        }

        public string SourceReference => "from pixabay";
        public string SourceURL => "https://pixabay.com";
        public string SourceImageURL => "https://pixabay.com/static/img/logo.png";
    }
}
