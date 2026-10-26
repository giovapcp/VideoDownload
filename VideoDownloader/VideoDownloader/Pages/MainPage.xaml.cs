using CommunityToolkit.Maui.Storage;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Converter;

namespace VideoDownloader.Pages;

public partial class MainPage : ContentPage
{
    // default route
    string carpetaDestino = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
    private readonly YoutubeClient youtube = new();

    public MainPage()
    {
        InitializeComponent();
        PathLabel.Text = carpetaDestino;
    }

    private async void OnDownloadMp4Clicked(object sender, EventArgs e) => await Descargar(esMp3: false);
    private async void OnDownloadMp3Clicked(object sender, EventArgs e) => await Descargar(esMp3: true);

    private async void OnSelectPathClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (result.IsSuccessful)
            {
                carpetaDestino = result.Folder.Path;
                PathLabel.Text = carpetaDestino;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Was not possible to open selector: {ex.Message}", "OK");
        }
    }

    private async Task Descargar(bool esMp3)
    {
        var url = UrlEntry.Text;

        if (string.IsNullOrWhiteSpace(url))
        {
            await DisplayAlert("Attention", "Please, paste a YouTube video Link.", "OK");
            return;
        }

        try
        {
            Cargando.IsRunning = true;
            StatusLabel.Text = "Analyzing video...";

            var video = await youtube.Videos.GetAsync(url);
            
            // clean tittle from non valid charcaters 
            string nombreLimpio = string.Join("_", video.Title.Split(Path.GetInvalidFileNameChars()));
            string ext = esMp3 ? "mp3" : "mp4";
            string rutaFinal = Path.Combine(carpetaDestino, $"{nombreLimpio}.{ext}");

            var streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

            if (esMp3)
            {
                StatusLabel.Text = "Analyzing audio quality...";

                string selectedBitrate = AudioQualityPicker?.SelectedItem?.ToString() ?? "Best Available";

                streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
                var audioStreams = streamManifest.GetAudioOnlyStreams().ToList();

                IStreamInfo? audioStreamInfo = null;

                // manual quality
                if (selectedBitrate.Contains("320"))
                {
                    audioStreamInfo = audioStreams.OrderByDescending(s => s.Bitrate).FirstOrDefault();
                }
                else if (selectedBitrate.Contains("256"))
                {
                    audioStreamInfo = audioStreams.Where(s => s.Bitrate.KiloBitsPerSecond <= 256)
                                                  .OrderByDescending(s => s.Bitrate).FirstOrDefault();
                }
                else if (selectedBitrate.Contains("128"))
                {
                    audioStreamInfo = audioStreams.Where(s => s.Bitrate.KiloBitsPerSecond <= 128)
                                                  .OrderByDescending(s => s.Bitrate).FirstOrDefault();
                }

                // if exact quality not founded, get the best one.
                audioStreamInfo ??= audioStreams.GetWithHighestBitrate();

                if (audioStreamInfo != null)
                {
                    double actualKbps = Math.Round(audioStreamInfo.Bitrate.KiloBitsPerSecond);
                    StatusLabel.Text = $"Downloading at ~{actualKbps} kbps...";

                    // direct download from audio stream .mp3 without converter
                    await youtube.Videos.Streams.DownloadAsync(audioStreamInfo, rutaFinal);

                    StatusLabel.Text = "Audio download complete!";
                }
            }
            else
            {
                StatusLabel.Text = "Analyzing video channels...";

                streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);

                string calidadDeseada = QualityPicker.SelectedItem?.ToString() ?? "1080p";

                var videoOnlyStreams = streamManifest.GetVideoOnlyStreams()
                    .OrderByDescending(s => s.VideoQuality.MaxHeight)
                    .ToList();

                // video qualities
                int? alturaDeseada = calidadDeseada switch
                {
                    "1080p" => 1080,
                    "720p" => 720,
                    "480p" => 480,
                    "360p" => 360,
                    _ => null
                };

                // search for exact quality
                var videoStreamInfo = (alturaDeseada.HasValue)
                    ? videoOnlyStreams.FirstOrDefault(s => s.VideoQuality.MaxHeight == alturaDeseada.Value)
                    : videoOnlyStreams.FirstOrDefault();

                // If the selected quality is not founded, get the best one aviable.
                // to avoid nulls
                videoStreamInfo ??= videoOnlyStreams.FirstOrDefault();

                var audioStreamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();

                var calidadLabel = videoStreamInfo?.VideoQuality.Label ?? "Best aviable";
                StatusLabel.Text = $"Downloading and joining {calidadLabel}...";

                if (videoStreamInfo != null && audioStreamInfo != null)
                {
                    var streamInfos = new IStreamInfo[] { audioStreamInfo, videoStreamInfo };
                    var conversionRequest = new ConversionRequestBuilder(rutaFinal).Build();

                    await youtube.Videos.DownloadAsync(streamInfos, conversionRequest);
                }
                else
                {
                    throw new Exception("Was not posible to find audio or video channels.");
                }
            }

            StatusLabel.Text = "DONE!";
            await DisplayAlert("Success", $"Saved at:\n{nombreLimpio}.{ext}", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
            StatusLabel.Text = "Error in the download.";
        }
        finally
        {
            Cargando.IsRunning = false;
        }
    }
}