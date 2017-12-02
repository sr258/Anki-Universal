using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using WebImageLibrary.WebRepositories;

namespace WebImageLibrary
{
    public class WebImageLibraryClosedEventArgs : EventArgs
    {
        public enum Results
        {
            Selected,
            Aborted
        }

        public Results Result { get; }
        public StorageFile File { get; }
        public IRepositoryImage Metadata { get; }

        public WebImageLibraryClosedEventArgs()
        {
            Result = Results.Aborted;
        }

        public WebImageLibraryClosedEventArgs(StorageFile file, IRepositoryImage metadata)
        {
            Result = Results.Selected;
            File = file;
            Metadata = metadata;
        }
    }
}
