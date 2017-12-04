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

        public async Task<Tuple<IEnumerable<IRepositoryImage>, int>> Query(string query)
        {
            return await QueryPage(query, 1);
        }

        public async Task<Tuple<IEnumerable<IRepositoryImage>, int>> QueryPage(string query, int pageNr)
        {
            if (String.IsNullOrEmpty(query))
                return Tuple.Create(new List<IRepositoryImage>() as IEnumerable<IRepositoryImage>, 0);
            try
            {
                var result = await _pixabaySharpClient.QueryImagesAsync(new ImageQueryBuilder
                {
                    ImageType = ImageType.Illustration | ImageType.Vector,
                    Query = query,
                    Page = pageNr,
                    Order = Order.Popular
                });

                if (result == null || result.Images == null || !result.Images.Any())
                    return Tuple.Create(new List<IRepositoryImage>() as IEnumerable<IRepositoryImage>, 0);

                return Tuple.Create(result.Images.Select(i => new PixabayImage()
                {
                    Author = i.User,
                    HighResURL = i.FullHDImageURL,
                    MediumURL = i.PreviewURL,
                    SmallURL = i.WebformatURL,
                    Licence = "CC0 Creative Commons",
                    SourceURL = i.PageURL,
                    Title = i.Tags
                }) as IEnumerable<IRepositoryImage>,
                result.TotalHits);
            }
            catch (Exception e)
            {
                return Tuple.Create(new List<IRepositoryImage>() as IEnumerable<IRepositoryImage>, 0);
            }
        }

        public string SourceReference => "Images provided by";
        public string SourceURL => "https://pixabay.com";
        public string SourceImageURL => "https://pixabay.com/static/img/logo.png";
    }
}
