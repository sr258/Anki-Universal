using System.Threading.Tasks;
using WebImageLibrary.WebRepositories.OpenClipartClient.Models;
using WebImageLibrary.WebRepositories.OpenClipartClient.Utility;

namespace WebImageLibrary.WebRepositories.OpenClipartClient
{
    class OpenClipartClient
    {
        private static string _baseUri = "https://openclipart.org/search/json/";
        private readonly HttpHelper _httpHelper = new HttpHelper(_baseUri);

        public async Task<Result> QueryImagesAsync(string search)
        {
            var s = HttpHelper.UriEncode(search);
            return await _httpHelper.GetRequestAsync<Result>($"?query={search}").ConfigureAwait(false);
        }

        public async Task<Result> QueryImagesAsync(QueryBuilder query)
        {
            var q = query.GetQueryString();
            return await _httpHelper.GetRequestAsync<Result>(q).ConfigureAwait(false);
        }
    }
}
