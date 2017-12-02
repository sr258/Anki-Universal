using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebImageLibrary.WebRepositories
{
    internal interface IWebRepository
    {
        Task<IEnumerable<IRepositoryImage>> Query(string query);
        Task<IEnumerable<IRepositoryImage>> QueryPage(string query, int pageNr);

        string SourceReference { get; }
        string SourceURL { get; }
        string SourceImageURL { get; }
    }
}
