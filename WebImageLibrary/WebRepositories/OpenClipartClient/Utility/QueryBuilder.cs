using System;
using System.Text;

namespace WebImageLibrary.WebRepositories.OpenClipartClient.Utility
{
    class QueryBuilder
    {
        public string Query { get; set; }   
        public int? Amount { get; set; }
        public int? Page { get; set; }
        public SortModes? Sort { get; set; }

        public string GetQueryString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            if(String.IsNullOrEmpty(Query))
                throw new ArgumentException("Property Query must not be empty.");

            stringBuilder.Append($"?query={HttpHelper.UriEncode(Query)}");

            if (Amount.HasValue)
                stringBuilder.Append($"&amount={Amount}");

            if (Page.HasValue)
                stringBuilder.Append($"&page={Page}");

            if (Sort.HasValue)
            {
                var str = "";
                switch (Sort)
                {
                    case SortModes.Date:
                        str = "date";
                        break;
                    case SortModes.Downloads:
                        str = "downloads";
                        break;
                    case SortModes.Favorites:
                        str = "favorites";
                        break;
                }
                if (!string.IsNullOrEmpty(str))
                    stringBuilder.Append($"&sort={str}");
            }

            return stringBuilder.ToString();
        }
    }
}
