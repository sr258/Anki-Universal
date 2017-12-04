using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebImageLibrary.WebRepositories
{
    internal static class RepositoryDirectory
    {
        public static IEnumerable<IWebRepository> GetWebRepositories()
        {
            return new List<IWebRepository>()
            {
                new MemoryCachedRepository(new PixabayRepository("3741913-00f7fa69ee5cead32bb590d1b"))
            };
        }
    }
}
