using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Utility
{
    class HttpHelper
    {
        private readonly string _baseUri;

        public HttpHelper(string baseUri)
        {
            _baseUri = baseUri;
        }

        /// <summary>
        /// Make an get webrequest to the OpenClipart api and return the deserialized object.
        /// </summary>
        /// <typeparam name="TClass">Class which should be returned</typeparam>
        /// <param name="query">Query to search for</param>
        /// <returns>Result of type TClass</returns>
        internal async Task<TClass> GetRequestAsync<TClass>(string query)
            where TClass : class
        {
            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.ExpectationFailed);

            using (var client = new HttpClient())
            {
                try
                {                    
                    response = await client.GetAsync(_baseUri + query).ConfigureAwait(false);
                    var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                        return JsonConvert.DeserializeObject<TClass>(responseString);

                    Debug.WriteLine(responseString);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("exception thrown: " + ex.Message);
                }
            }

            return default(TClass);
        }

        /// <summary>
        /// Encode an URI.
        /// </summary>
        /// <param name="s">String to encode</param>
        /// <returns>Encoded string</returns>
        public static string UriEncode(string s)
        {
            return Uri.EscapeUriString(s).Replace("%20", "+");
        }
    }
}
