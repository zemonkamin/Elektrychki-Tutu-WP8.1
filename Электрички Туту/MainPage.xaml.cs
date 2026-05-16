using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading; // Add this using directive
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.ObjectModel;


namespace Электрички_Туту
{

    public sealed partial class MainPage : Page
    {
        private string selectedFromStationId = "";
        private string selectedToStationId = "";
        private string selectedFromStationName = "";
        private string selectedToStationName = "";
        private List<StationSuggestion> currentFromSuggestions = new List<StationSuggestion>();
        private List<StationSuggestion> currentToSuggestions = new List<StationSuggestion>();
        private List<RouteHistoryItem> searchHistory = new List<RouteHistoryItem>();
        private CancellationTokenSource searchCancellationTokenSource;

        public MainPage()
        {
            this.InitializeComponent();

            LoadSettings();

            currentFromSuggestions = new List<StationSuggestion>();
            currentToSuggestions = new List<StationSuggestion>();

            LoadSearchHistory();

            LoadLastSearch();

            searchCancellationTokenSource = new CancellationTokenSource();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {

        }

        private async void FromTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = FromTextBox.Text.Trim();
            if (searchText.Length >= 2) // Only search when at least 2 characters are entered
            {
                var suggestions = await GetStationSuggestions(searchText);
                if (suggestions != null && suggestions.Count > 0)
                {
                    currentFromSuggestions = suggestions;
                    FromSuggestionsList.ItemsSource = suggestions.Select(s => s.label).ToList();
                    FromSuggestionsBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    FromSuggestionsBorder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                FromSuggestionsBorder.Visibility = Visibility.Collapsed;
                selectedFromStationId = "";
            }
        }

        private async void ToTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = ToTextBox.Text.Trim();
            if (searchText.Length >= 2) // Only search when at least 2 characters are entered
            {
                var suggestions = await GetStationSuggestions(searchText);
                if (suggestions != null && suggestions.Count > 0)
                {
                    currentToSuggestions = suggestions;
                    ToSuggestionsList.ItemsSource = suggestions.Select(s => s.label).ToList();
                    ToSuggestionsBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    ToSuggestionsBorder.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ToSuggestionsBorder.Visibility = Visibility.Collapsed;
                selectedToStationId = "";
            }
        }

        private async Task<List<StationSuggestion>> GetStationSuggestions(string searchText)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {

                    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    client.DefaultRequestHeaders.Add("Accept", "application/json, text/javascript, */*; q=0.01");
                    client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
                    client.DefaultRequestHeaders.Add("Referer", "https://www.tutu.ru/");

                    string url = $"https://www.tutu.ru/station/suggest.php?name={Uri.EscapeDataString(searchText)}";
                    HttpResponseMessage response = await client.GetAsync(url);
                    if (response.IsSuccessStatusCode)
                    {
                        string json = await response.Content.ReadAsStringAsync();
                        var suggestions = JsonConvert.DeserializeObject<List<StationSuggestion>>(json);
                        return suggestions;
                    }
                }
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.WriteLine($"Error fetching suggestions: {ex.Message}");
            }
            return new List<StationSuggestion>();
        }

        private void FromSuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FromSuggestionsList.SelectedItem != null)
            {
                string selectedLabel = FromSuggestionsList.SelectedItem.ToString();
                FromTextBox.Text = selectedLabel;
                FromSuggestionsBorder.Visibility = Visibility.Collapsed;

                var selectedStation = currentFromSuggestions.FirstOrDefault(s => s.label == selectedLabel);
                if (selectedStation != null)
                {

                    string[] parts = selectedStation.value.Split('|');
                    string firstPart = parts[0];

                    int dashIndex = firstPart.IndexOf('-');
                    if (dashIndex > 0)
                    {
                        selectedFromStationId = firstPart.Substring(0, dashIndex);
                        selectedFromStationName = selectedLabel; // Store the name for history
                    }
                    else
                    {

                        selectedFromStationId = firstPart;
                        selectedFromStationName = selectedLabel; // Store the name for history
                    }

                    SaveLastSearch();
                }

                FromSuggestionsList.SelectedIndex = -1;
            }
        }

        private void ToSuggestionsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ToSuggestionsList.SelectedItem != null)
            {
                string selectedLabel = ToSuggestionsList.SelectedItem.ToString();
                ToTextBox.Text = selectedLabel;
                ToSuggestionsBorder.Visibility = Visibility.Collapsed;

                var selectedStation = currentToSuggestions.FirstOrDefault(s => s.label == selectedLabel);
                if (selectedStation != null)
                {

                    string[] parts = selectedStation.value.Split('|');
                    string firstPart = parts[0];

                    int dashIndex = firstPart.IndexOf('-');
                    if (dashIndex > 0)
                    {
                        selectedToStationId = firstPart.Substring(0, dashIndex);
                        selectedToStationName = selectedLabel; // Store the name for history
                    }
                    else
                    {

                        selectedToStationId = firstPart;
                        selectedToStationName = selectedLabel; // Store the name for history
                    }

                    SaveLastSearch();
                }

                ToSuggestionsList.SelectedIndex = -1;
            }
        }

        private void ViewScheduleButton_Click(object sender, RoutedEventArgs e)
        {

            System.Diagnostics.Debug.WriteLine($"From Station ID: {selectedFromStationId}");
            System.Diagnostics.Debug.WriteLine($"To Station ID: {selectedToStationId}");

            if (!string.IsNullOrEmpty(selectedFromStationId) && !string.IsNullOrEmpty(selectedToStationId))
            {

                SaveLastSearch();

                AddToHistory(selectedFromStationName, selectedToStationName, selectedFromStationId, selectedToStationId);

                var parameters = new Dictionary<string, string>
                {
                    { "fromId", selectedFromStationId },
                    { "toId", selectedToStationId },
                    { "fromName", selectedFromStationName },  // Add station names
                    { "toName", selectedToStationName }       // Add station names
                };

                Frame.Navigate(typeof(rasp), parameters);
            }
            else
            {
                var dialog = new ContentDialog()
                {
                    Title = "Ошибка",
                    Content = "Пожалуйста, выберите станции отправления и назначения",
                    PrimaryButtonText = "OK"
                };
                var task = dialog.ShowAsync();
            }
        }

        private void Page_Tapped(object sender, TappedRoutedEventArgs e)
        {
            FromSuggestionsBorder.Visibility = Visibility.Collapsed;
            ToSuggestionsBorder.Visibility = Visibility.Collapsed;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsDialog();
        }

        private async void InfoButton_Click(object sender, RoutedEventArgs e)
        {

            var textBlock = new TextBlock
            {
                Text = "Приложение работает на парсере API сервиса Туту.\n\nКоманда: LegacyProjects\nРазработчик: Zemonkamin",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var dialog = new ContentDialog
            {
                Title = "О программе",
                Content = textBlock,
                PrimaryButtonText = "OK"
            };

            await dialog.ShowAsync();
        }

        private async void ShowSettingsDialog()
        {

            var urlTextBox = new TextBox
            {
                Text = Config.BaseUrl,
                Margin = new Thickness(0, 10, 0, 0),
                PlaceholderText = "Введите базовый URL"
            };

            var stackPanel = new StackPanel();
            stackPanel.Children.Add(new TextBlock { Text = "Базовый URL:" });
            stackPanel.Children.Add(urlTextBox);

            var dialog = new ContentDialog
            {
                Title = "Настройки",
                Content = stackPanel,
                PrimaryButtonText = "Сохранить",
                SecondaryButtonText = "Отмена"
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {

                string newUrl = urlTextBox.Text;
                if (!string.IsNullOrEmpty(newUrl))
                {

                    if (!newUrl.EndsWith("/"))
                    {
                        newUrl += "/";
                    }

                    Config.BaseUrl = newUrl;

                    var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
                    localSettings.Values["BaseUrl"] = newUrl;
                }
            }
        }

        private void LoadSettings()
        {
            var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey("BaseUrl"))
            {
                string savedUrl = localSettings.Values["BaseUrl"] as string;
                if (!string.IsNullOrEmpty(savedUrl))
                {
                    Config.BaseUrl = savedUrl;
                }
            }
        }

        private void AddToHistory(string fromName, string toName, string fromId, string toId)
        {

            var existingItem = searchHistory.FirstOrDefault(h => 
                h.FromStationName == fromName && h.ToStationName == toName);
            
            if (existingItem != null)
            {

                searchHistory.Remove(existingItem);
                searchHistory.Insert(0, existingItem);
            }
            else
            {

                var newItem = new RouteHistoryItem
                {
                    FromStationName = fromName,
                    ToStationName = toName,
                    FromStationId = fromId,
                    ToStationId = toId
                };
                searchHistory.Insert(0, newItem);

                if (searchHistory.Count > 10)
                {
                    searchHistory.RemoveAt(searchHistory.Count - 1);
                }
            }
            
            SaveSearchHistory();
            UpdateHistoryUI();
        }
        
        private void LoadSearchHistory()
        {
            try
            {
                var historyJson = Windows.Storage.ApplicationData.Current.LocalSettings.Values["SearchHistory"] as string;
                if (!string.IsNullOrEmpty(historyJson))
                {
                    searchHistory = JsonConvert.DeserializeObject<List<RouteHistoryItem>>(historyJson);
                }
                else
                {
                    searchHistory = new List<RouteHistoryItem>();
                }
            }
            catch
            {
                searchHistory = new List<RouteHistoryItem>();
            }
            
            UpdateHistoryUI();
        }
        
        private void SaveSearchHistory()
        {
            try
            {
                var historyJson = JsonConvert.SerializeObject(searchHistory);
                Windows.Storage.ApplicationData.Current.LocalSettings.Values["SearchHistory"] = historyJson;
            }
            catch
            {

            }
        }
        
        private void UpdateHistoryUI()
        {
            HistoryPanel.Children.Clear();
            
            foreach (var item in searchHistory)
            {
                var button = new Button
                {
                    Content = $"{item.FromStationName} → {item.ToStationName}",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 0, 0, 5)
                };

                button.Tag = item;
                button.Click += HistoryItem_Click;
                
                HistoryPanel.Children.Add(button);
            }
        }
        
        private void HistoryItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var item = button.Tag as RouteHistoryItem;

            FromTextBox.Text = item.FromStationName;
            ToTextBox.Text = item.ToStationName;

            selectedFromStationId = item.FromStationId;
            selectedToStationId = item.ToStationId;
            selectedFromStationName = item.FromStationName;
            selectedToStationName = item.ToStationName;

            SaveLastSearch();
        }

        private void SaveLastSearch()
        {
            try
            {
                var lastSearch = new LastSearchItem
                {
                    FromStationName = selectedFromStationName,
                    ToStationName = selectedToStationName,
                    FromStationId = selectedFromStationId,
                    ToStationId = selectedToStationId
                };
                
                var lastSearchJson = JsonConvert.SerializeObject(lastSearch);
                Windows.Storage.ApplicationData.Current.LocalSettings.Values["LastSearch"] = lastSearchJson;
            }
            catch
            {

            }
        }

        private void LoadLastSearch()
        {
            try
            {
                var lastSearchJson = Windows.Storage.ApplicationData.Current.LocalSettings.Values["LastSearch"] as string;
                if (!string.IsNullOrEmpty(lastSearchJson))
                {
                    var lastSearch = JsonConvert.DeserializeObject<LastSearchItem>(lastSearchJson);

                    FromTextBox.Text = lastSearch.FromStationName;
                    ToTextBox.Text = lastSearch.ToStationName;

                    selectedFromStationId = lastSearch.FromStationId;
                    selectedToStationId = lastSearch.ToStationId;
                    selectedFromStationName = lastSearch.FromStationName;
                    selectedToStationName = lastSearch.ToStationName;
                }
            }
            catch
            {

            }
        }

        private void SwapStationsButton_Click(object sender, RoutedEventArgs e)
        {

            string tempText = FromTextBox.Text;
            FromTextBox.Text = ToTextBox.Text;
            ToTextBox.Text = tempText;

            string tempId = selectedFromStationId;
            selectedFromStationId = selectedToStationId;
            selectedToStationId = tempId;

            string tempName = selectedFromStationName;
            selectedFromStationName = selectedToStationName;
            selectedToStationName = tempName;

            SaveLastSearch();
        }
    }

    public class StationSuggestion
    {
        public int geoId { get; set; }
        public string value { get; set; }
        public string label { get; set; }
    }
    
    public class RouteHistoryItem
    {
        public string FromStationName { get; set; }
        public string ToStationName { get; set; }
        public string FromStationId { get; set; }
        public string ToStationId { get; set; }
    }
    
    public class LastSearchItem
    {
        public string FromStationName { get; set; }
        public string ToStationName { get; set; }
        public string FromStationId { get; set; }
        public string ToStationId { get; set; }
    }
}