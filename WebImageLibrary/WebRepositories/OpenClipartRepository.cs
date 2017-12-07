using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebImageLibrary.WebRepositories.OpenClipartClient.Utility;

namespace WebImageLibrary.WebRepositories
{
    class OpenClipartRepository : IWebRepository
    {
        private readonly OpenClipartClient.OpenClipartClient _client;

        public OpenClipartRepository()
        {
            _client = new OpenClipartClient.OpenClipartClient();
        }

        public async Task<Tuple<IEnumerable<IRepositoryImage>, int>> Query(string query, int pageSize)
        {
            return await QueryPage(query, pageSize, 1);
        }

        public async Task<Tuple<IEnumerable<IRepositoryImage>, int>> QueryPage(string query, int pageSize, int pageNr)
        {
            if (String.IsNullOrEmpty(query))
                return Tuple.Create(new List<IRepositoryImage>() as IEnumerable<IRepositoryImage>, 0);
            try
            {
                var result = await _client.QueryImagesAsync(new QueryBuilder()
                {                    
                    Query = query,
                    Amount = pageSize,
                    Page = pageNr,
                    Sort = SortModes.Downloads
                });

                if (result == null || result.Payload == null || !result.Payload.Any() || result.Message != "success")
                    return Tuple.Create(new List<IRepositoryImage>() as IEnumerable<IRepositoryImage>, 0);

                return Tuple.Create(result.Payload.Select(i => new BaseImage()
                    {
                        Author = i.Uploader,
                        HighResURL = i.ImageLinks.Png2400px,
                        MediumURL = i.ImageLinks.PngFullLossy,
                        SmallURL = i.ImageLinks.PngThumb,
                        Licence = "CC0 1.0 Universal (CC0 1.0)",
                        SourceURL = i.DetailLink,
                        Title = i.Title
                    }) as IEnumerable<IRepositoryImage>,
                    result.Info.Results);
            }
            catch (Exception e)
            {
                return Tuple.Create(new List<IRepositoryImage>() as IEnumerable<IRepositoryImage>, 0);
            }
        }

        public string SourceReference => "Images provided by";
        public string SourceURL => "https://openclipart.org";
        public string SourceImageURL => "https://openclipart.org/assets/images/images/openclipart-banner.png";
    }
}
