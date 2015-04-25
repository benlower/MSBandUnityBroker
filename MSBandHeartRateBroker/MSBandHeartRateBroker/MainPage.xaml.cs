using Microsoft.Band;
using Microsoft.Band.Sensors;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace MSBandHeartRateBroker
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();

            this.NavigationCacheMode = NavigationCacheMode.Required;
        }

        /// <summary>
        /// Connect to Microsoft Band and read HeartRate data.
        /// </summary>
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Get the list of Microsoft Bands paired to the phone.
                IBandInfo[] pairedBands = await BandClientManager.Instance.GetBandsAsync();
                if (pairedBands.Length < 1)
                {
                    this.textBlock.Text = "This app requires a Microsoft Band paired to your phone. Also make sure that you have the latest firmware installed on your Band, as provided by the latest Microsoft Health app.";
                    return;
                }

                // Connect to Microsoft Band.
                using (IBandClient bandClient = await BandClientManager.Instance.ConnectAsync(pairedBands[0]))
                {
                    (sender as Button).IsEnabled = false;

                    // hook up to the HeartRate sensor ReadingChanged event
                    bandClient.SensorManager.HeartRate.ReadingChanged += HeartRate_ReadingChanged;
                    await bandClient.SensorManager.HeartRate.StartReadingsAsync();

                    this.Storyboard1.Begin();
                    this.textBlockHeart.Foreground = new SolidColorBrush(Colors.Red);
                    this.textBlockNumber.Foreground = new SolidColorBrush(Colors.White);

                    // Get data for a minute
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    await bandClient.SensorManager.HeartRate.StopReadingsAsync();
                }
            }
            catch (Exception ex)
            {
                this.textBlock.Text = ex.ToString();
            }
        }

        private async void HeartRate_ReadingChanged(object sender, BandSensorReadingEventArgs<IBandHeartRateReading> e)
        {
            IBandHeartRateReading hr = e.SensorReading;
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Send(hr.HeartRate.ToString());
            }).AsTask();
        }

        private void SampleButton_Click(object sender, RoutedEventArgs e)
        {
            Send(new Random((int)DateTime.Now.Ticks).Next(30, 145).ToString());
        }

        async void Send(string value)
        {
            try
            {
                this.progressBar.Visibility = Visibility.Visible;

                this.textBlockError.Text = string.Empty;
                this.textBlockNumber.Text = value;
                this.textBlock.Text = string.Format("Sending {0}...", value);

                var url = new Uri(this.textBox.Text);
                var connection = new Microsoft.AspNet.SignalR.Client.HubConnection(url.ToString());

                var proxy = connection.CreateHubProxy("HeartRateHub");
                await connection.Start();

                await proxy.Invoke("SendHeartRate", value);
                this.textBlock.Text = "Sent.";
            }
            catch (Exception ex)
            {
                this.textBlockError.Text = ex.Message.ToString();
            }
            finally
            {
                this.progressBar.Visibility = Visibility.Collapsed;
            }
        }
    }
}
