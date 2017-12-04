using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private IWebRepository _lastRepository = null;
        private int _lastPage = 0;
        private readonly ObservableCollection<IRepositoryImage> _results = new ObservableCollection<IRepositoryImage>();

        public WebImageLibraryDialog()
        {
            InitializeComponent();
            SearchProvider.ItemsSource = _webRepositories;
            SearchProvider.SelectedIndex = 0;
            SetStartState();
            Results.ItemsSource = _results;
        }

        private void SetStartState()
        {
            ProgressIndicator.Visibility = Visibility.Collapsed;
            ResultsViewer.Visibility = Visibility.Collapsed;
            NoResultsLabel.Visibility = Visibility.Collapsed;
        }

        private void SetSearchingState()
        {
            ProgressIndicator.Visibility = Visibility.Visible;
            ResultsViewer.Visibility = Visibility.Collapsed;
            NoResultsLabel.Visibility = Visibility.Collapsed;
        }

        private void SetHasResultsState()
        {
            ProgressIndicator.Visibility = Visibility.Collapsed;
            ResultsViewer.Visibility = Visibility.Visible;
            NoResultsLabel.Visibility = Visibility.Collapsed;
        }

        private void SetNoResultsState()
        {
            ProgressIndicator.Visibility = Visibility.Collapsed;
            ResultsViewer.Visibility = Visibility.Collapsed;
            NoResultsLabel.Visibility = Visibility.Visible;
        }

        private void SetErrorState()
        {
            ProgressIndicator.Visibility = Visibility.Collapsed;
            ResultsViewer.Visibility = Visibility.Collapsed;
            NoResultsLabel.Visibility = Visibility.Collapsed;
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

        public async Task SetQuery(string query)
        {
            SearchField.Text = query;
            await Search();
        }

        private async Task Search()
        {
            SetSearchingState();
            _results.Clear();
            _lastRepository = (SearchProvider.SelectedItem as IWebRepository);
            if (_lastRepository == null)
            {
                SetErrorState();
                return;
            }

            var result = await _lastRepository.Query(SearchField.Text);
            foreach (var image in result.Item1)
            {
                _results.Add(image);
            }
            _lastPage = 0;

            IsPrimaryButtonEnabled = false;
            
            if(result.Item2 > 0)
                SetHasResultsState();
            else
                SetNoResultsState();

            DisplayShowMore(result.Item2);
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

        private string CreateFileName(IRepositoryImage image)
        {
            return image.Title.Replace(" ", "_");
        }

        private async Task<StorageFile> DownloadToTempFile(IRepositoryImage selectedImage)
        {
            var extension = Path.GetExtension(selectedImage.MediumURL);
            StorageFile tempFile =
                await ApplicationData.Current.TemporaryFolder.CreateFileAsync(CreateFileName(selectedImage) + extension,
                    CreationCollisionOption.ReplaceExisting);

            var uri = new Uri(selectedImage.MediumURL);
            HttpClient client = new HttpClient();
            var data = await client.GetByteArrayAsync(uri);
            await FileIO.WriteBytesAsync(tempFile, data);
            return tempFile;
        }

        private async void MoreButton_OnClick(object sender, RoutedEventArgs e)
        {
            if (_lastRepository == null)
            {
                SetErrorState();
                return;
            }

            var result = await _lastRepository.QueryPage(SearchField.Text, ++_lastPage);
            foreach (var image in result.Item1)
            {
                _results.Add(image);
            }            

            DisplayShowMore(result.Item2);
        }

        private void DisplayShowMore(int totalCount)
        {
            MoreButton.Visibility = _results.Count < totalCount ? Visibility.Visible : Visibility.Collapsed;
            MoreCount.Text = "(" + (totalCount - _results.Count) + ")";
        }
    }
}
