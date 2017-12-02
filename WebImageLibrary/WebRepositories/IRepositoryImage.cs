using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebImageLibrary.WebRepositories
{
    public interface IRepositoryImage
    {
        string SmallURL { get; set; }
        string MediumURL { get; set; }
        string HighResURL { get; set; }
        string Title { get; set; }
        string Licence { get; set; }
        string Author { get; set; }
        string SourceURL { get; set; }
    }
}
