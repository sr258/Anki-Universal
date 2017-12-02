using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using WebImageLibrary.WebRepositories;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace WebImageLibrary
{
    public sealed partial class WebImageLibraryDialog : ContentDialog
    {
        private readonly IEnumerable<IWebRepository> _webRepositories = RepositoryDirectory.GetWebRepositories();

        public WebImageLibraryDialog()
        {
            InitializeComponent();
        }

        private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var selectedImage = Results.SelectedItem as IRepositoryImage;
            if (selectedImage == null)
            {
                InvokeCancel();
            }

            var tempFile = await DownloadToTempFile(selectedImage);

            Finished?.Invoke(this, new WebImageLibraryClosedEventArgs(tempFile, selectedImage));
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            InvokeCancel();
        }

        public event EventHandler<WebImageLibraryClosedEventArgs> Finished;

        public async Task Open(string query)
        {
            SearchField.Text = query;
            await Search();
        }

        private async Task Search()
        {
            var result = await _webRepositories.First().Query(SearchField.Text);
            Results.ItemsSource = result;
            IsPrimaryButtonEnabled = false;
        }

        private async void SearchButton_Tapped(object sender, TappedRoutedEventArgs e)
        {
            await Search();
        }

        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            await Search();
        }

        private async void SearchField_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter)
                await Search();
        }

        private void InvokeCancel()
        {
            Finished?.Invoke(this, new WebImageLibraryClosedEventArgs());
        }        

        private void Results_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = e.AddedItems.Any();
        }

        private async Task<StorageFile> DownloadToTempFile(IRepositoryImage selectedImage)
        {
            var extension = Path.GetExtension(selectedImage.MediumURL);
            StorageFile tempFile =
                await ApplicationData.Current.TemporaryFolder.CreateFileAsync("temp_image" + extension,
                    CreationCollisionOption.ReplaceExisting);

            var uri = new Uri(selectedImage.MediumURL);
            HttpClient client = new HttpClient();
            var data = await client.GetByteArrayAsync(uri);
            await FileIO.WriteBytesAsync(tempFile, data);
            return tempFile;
        }
    }
}
