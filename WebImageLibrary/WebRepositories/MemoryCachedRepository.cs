using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebImageLibrary.WebRepositories
{
    internal class MemoryCachedRepository : IWebRepository
    {
        private readonly IWebRepository _repository;
        private readonly Dictionary<string, Tuple<IEnumerable<IRepositoryImage>, int>> _cache = new Dictionary<string, Tuple<IEnumerable<IRepositoryImage>, int>>();

        public MemoryCachedRepository(IWebRepository repository)
        {
            _repository = repository;
        }

        public Task<Tuple<IEnumerable<IRepositoryImage>, int>> Query(string query, int pageSize)
        {
            return QueryPage(query, pageSize, 1);
        }

        public async Task<Tuple<IEnumerable<IRepositoryImage>, int>> QueryPage(string query, int pageSize, int pageNr)
        {
            var cacheId = query + "-" + pageNr;
            Tuple<IEnumerable<IRepositoryImage>, int> value;
            if (_cache.TryGetValue(cacheId, out value))
            {
                return value;
            }

            value = await _repository.QueryPage(query, pageSize, pageNr);
            _cache.Add(cacheId, value);
            return value;
        }

        public string SourceReference => _repository.SourceReference;
        public string SourceURL => _repository.SourceURL;
        public string SourceImageURL => _repository.SourceImageURL;
    }
}
