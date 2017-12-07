using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebImageLibrary.WebRepositories
{
    internal class BaseImage : IRepositoryImage
    {
        public string SmallURL { get; set; }
        public string MediumURL { get; set; }
        public string HighResURL { get; set; }
        public string Title { get; set; }
        public string Licence { get; set; }
        public string Author { get; set; }
        public string SourceURL { get; set; }
    }
}
