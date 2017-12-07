using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebImageLibrary.WebRepositories
{
    internal interface IWebRepository
    {
        Task<Tuple<IEnumerable<IRepositoryImage>, int>> Query(string query, int pageSize);
        Task<Tuple<IEnumerable<IRepositoryImage>, int>> QueryPage(string query, int pageSize, int pageNr);

        string SourceReference { get; }
        string SourceURL { get; }
        string SourceImageURL { get; }
    }
}
