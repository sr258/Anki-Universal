using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Nito.AsyncEx;
using WebImageLibrary.WebRepositories;

// The Content Dialog item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace WebImageLibrary
{
    public sealed partial class WebImageLibraryDialog : ContentDialog
    {
        private const double TOLERANCE = 0.00001d;

        private readonly IEnumerable<IWebRepository> _webRepositories = RepositoryDirectory.GetWebRepositories();
        private IWebRepository _lastRepository = null;
        private int _lastPage = 0;
        private string _lastQuery = "";
        private bool _canShowMore = false;
        private readonly ObservableCollection<IRepositoryImage> _results = new ObservableCollection<IRepositoryImage>();
        private bool _isQuerying = false;
        private readonly AsyncLock _queryMutex = new AsyncLock();

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
            using (await _queryMutex.LockAsync())
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
                Debug.WriteLine($"Queried {SearchField.Text}. Got {result.Item2} results.");
                foreach (var image in result.Item1)
                {
                    _results.Add(image);
                }
                _lastPage = 0;
                _lastQuery = SearchField.Text;

                IsPrimaryButtonEnabled = false;
                SourceHyperlinkButton.NavigateUri = new Uri(_lastRepository.SourceURL);
                SourceTextBlock.Text = _lastRepository.SourceReference;
                SourceImage.Source = new BitmapImage(new Uri(_lastRepository.SourceImageURL));

                if (result.Item2 > 0)
                    SetHasResultsState();
                else
                    SetNoResultsState();

                EvaluateShowMore(result.Item2);                
            }
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

        private void EvaluateShowMore(int totalCount)
        {
            _canShowMore = _results.Count < totalCount;
        }

        private async void ResultsViewer_OnViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            using (await _queryMutex.LockAsync())
            {
                if (!_canShowMore)
                    return;

                var scrollViewer = (ScrollViewer)sender;
                if (Math.Abs(scrollViewer.VerticalOffset - scrollViewer.ScrollableHeight) > TOLERANCE)
                    return;

                if (_lastRepository == null)
                {
                    SetErrorState();
                    return;
                }
                LoadingMoreIndicator.Visibility = Visibility.Visible;

                var result = await _lastRepository.QueryPage(_lastQuery, ++_lastPage);
                Debug.WriteLine(
                    $"Added entries for {_lastQuery} (page {_lastPage}). Got {result.Item1.Count()} entries.");
                foreach (var image in result.Item1)
                {
                    _results.Add(image);
                }

                LoadingMoreIndicator.Visibility = Visibility.Collapsed;

                EvaluateShowMore(result.Item2);
            }
        }
    }
}
