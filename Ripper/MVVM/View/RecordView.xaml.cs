﻿using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Collections.Generic;
using System.Windows.Media.Imaging;

using Downloader;
using WebPWrapper;
using Javi.FFmpeg;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;

namespace Ripper.MVVM.View
{
    public partial class RecordView : UserControl
    {
        #region Variables...
        private readonly string input;
        private readonly string output;
        private readonly string format;
        private readonly int resolution;
        private readonly bool audio;
        private readonly string epoch1;

        private readonly CancellationTokenSource source;
        private CancellationToken token;

        private DownloadService downloader;
        private FFmpeg ffmpeg;

        private string title;
        private string uploader;
        private float length;
        private string thumbnail;
        private FormatData[] records;
        private string formatraw;
        private FormatData record;
        private string video;

        private string temp;
        private string size;
        private bool downloading;

        private string final;

        private string epoch2;
        private string file1;
        private string file2;
        private bool processing;
        #endregion
         
        public RecordView(string i)
        {
            InitializeComponent();

            input = i;
            output = Globals.Output;
            format = Globals.Format;
            resolution = Globals.Resolution;

            if (format != "mp4" && format != "webm" && format != "mov" && format != "avi" && format != "flv")
            {
                audio = true;
            }

            epoch1 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

            source = new CancellationTokenSource();
            token = source.Token;

            try
            {
                Task.Factory.StartNew(() => Processor(), token);
            }
            catch (Exception ex)
            {
                Utilities.Error("Could not start worker process...", "Executable Error", "022", false, ex);
            }
        }

        private void Remove_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            source.Cancel();
        }

        #region Processor...
        private async void Processor()
        {
            #pragma warning disable CS4014 // This is the wanted functionality

            try
            {
                token.ThrowIfCancellationRequested();

                Dispatcher.Invoke(delegate ()
                {
                    Status.Text = "Starting...";
                });

                #region Systems and Events...
                // Set downloader (package) configuration and events from settings...

                DownloadConfiguration DownloadOption = new DownloadConfiguration()
                {
                    BufferBlockSize = Globals.Buffer,
                    ChunkCount = Globals.Chunks,
                    MaximumBytesPerSecond = Globals.Bytes,
                    MaxTryAgainOnFailover = Globals.Tries,
                    OnTheFlyDownload = Globals.OnFly,
                    ParallelDownload = true,
                    TempDirectory = Globals.Temp,
                    Timeout = Globals.Timeout,
                    RequestConfiguration = {
                        Accept = "*/*",
                        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                        CookieContainer =  new CookieContainer(),
                        Headers = new WebHeaderCollection(),
                        KeepAlive = false,
                        ProtocolVersion = HttpVersion.Version11,
                        UseDefaultCredentials = false,
                        UserAgent = $"DownloaderSample/{Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}"
                    }
                };

                downloader = new DownloadService(DownloadOption);

                try
                {
                    downloader.DownloadProgressChanged += DownloadProgression;
                }
                catch (Exception ex)
                {
                    Utilities.Error("Could not start a progress event listener...", "Error", "023", false, ex);
                }

                // Set ffmpeg (package) events from settings...

                ffmpeg = new FFmpeg(Globals.Real + @"FFmpeg.exe");

                try
                {
                    ffmpeg.OnProgress += ProcessProgression;
                }
                catch (Exception ex)
                {
                    Utilities.Error("Could not start a progress event listener...", "Error", "024", false, ex);
                }
                #endregion

                token.ThrowIfCancellationRequested();

                #region Get Metadata...
                try
                {
                    // Set YoutubeDL wrapper settings and get metadata...

                    YoutubeDL y = new YoutubeDL
                    {
                        YoutubeDLPath = Globals.Real + "YTDLP.exe"
                    };

                    RunResult<VideoData> r = await y.RunVideoDataFetch(input);
                    VideoData v = r.Data;
                    title = v.Title;
                    uploader = v.Uploader;
                    length = v.Duration ?? default;
                    thumbnail = v.Thumbnail;
                    records = v.Formats;
                }
                catch (Exception ex)
                {
                    source.Cancel();

                    Utilities.Error($"Could not fetch data regarding the URL: {input} ...", "Worker Error", "025", false, ex);
                }

                try
                {
                    // Get the requested format object (from the full selection)...

                    if (audio)
                    {
                        if (records.ToList().FindAll(a => a.Extension == format).Count < 1)
                        {
                            // If there are no records with native format, download in m4a...

                            records.ToList().FindAll(a => a.Extension == "m4a").ForEach(a =>
                            {
                                formatraw = "m4a";
                                record = a;
                            });
                        }
                        else
                        {
                            records.ToList().FindAll(a => a.Extension == format).ForEach(a =>
                            {
                                formatraw = format;
                                record = a;
                            });
                        }
                    }
                    else
                    {
                        if (records.ToList().FindAll(d => d.Extension == format).Count < 1)
                        {
                            // If there are no records with native format, download in mp4...

                            List<int> l = new List<int>() { 0 };
                            l = records.ToList().FindAll(a => a.Extension == "mp4").Select(a => a.Height ?? 0).ToList();
                            int scoped = l.Min(i => (Math.Abs(resolution - i), i)).i;

                            records.ToList().FindAll(a => a.Height == scoped).ForEach(a =>
                            {
                                formatraw = "mp4";
                                record = a;
                            });
                        }
                        else
                        {
                            List<int> l = new List<int>() { 0 };
                            l = records.ToList().FindAll(a => a.Extension == format).Select(a => a.Height ?? 0).ToList();
                            int scoped = l.Min(i => (Math.Abs(resolution - i), i)).i;

                            records.ToList().FindAll(a => a.Height == scoped).ForEach(a =>
                            {
                                formatraw = format;
                                record = a;
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    source.Cancel();

                    Utilities.Error("Could not find a viable media record...", "Worker Error", "026", false, ex);
                }

                video = record.Url;
                #endregion

                token.ThrowIfCancellationRequested();

                #region Post Metadata...
                // Post video metadata to record UI...

                try
                {
                    // Convert online thumbnail to a WPF compatible form...

                    BitmapImage bitmapimage = new BitmapImage();

                    if (thumbnail.Split('.').Last().ToString() == "webp")
                    {
                        Bitmap bitmap;

                        byte[] image = new WebClient().DownloadData(thumbnail);
                        using (WebP webp = new WebP())
                        {
                            bitmap = webp.Decode(image);
                        }

                        using (MemoryStream stream = new MemoryStream())
                        {
                            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                            stream.Position = 0;

                            bitmapimage.BeginInit();
                            bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapimage.StreamSource = stream;
                            bitmapimage.EndInit();
                        }
                    }
                    else
                    {
                        bitmapimage.BeginInit();
                        bitmapimage.UriSource = new Uri(thumbnail, UriKind.Absolute);
                        bitmapimage.EndInit();
                    }

                    Dispatcher.Invoke(delegate ()
                    {
                        Thumbnail.ImageSource = bitmapimage;
                    });
                }
                catch (Exception ex)
                {
                    Utilities.Error("Could not display thumbnail image...", "Worker Error", "027", false, ex);
                }

                Dispatcher.Invoke(delegate ()
                {
                    Title.Text = title;

                    Author.Text = uploader;

                    Length.Text = TimeSpan.FromSeconds(length).ToString(@"hh\:mm\:ss");
                });
                #endregion

                token.ThrowIfCancellationRequested();

                #region Get Base...
                // Download the base requested media...

                temp = Globals.Temp + "\\" + title + "." + epoch1 + "." + formatraw;

                size = FileSize(new Uri(video));

                Json.Read();

                downloading = true;

                Task.Factory.StartNew(async () =>
                {
                    try
                    {
                        await downloader.DownloadFileTaskAsync(video, temp);
                    }
                    catch (Exception ex)
                    {
                        source.Cancel();

                        Utilities.Error("Could not download base media files...", "Worker Error", "028", false, ex);
                    }

                    downloading = false;
                });

                while (downloading)
                {
                    token.ThrowIfCancellationRequested();

                    Thread.Sleep(10);
                }
                #endregion

                token.ThrowIfCancellationRequested();

                if (!audio)
                {
                    #region Get Additional...
                    // Download additional media (like audio files for the base silent video file)...

                    try
                    {
                        records.ToList().FindAll(a => a.Extension == "m4a").ForEach(a =>
                        {
                            record = a;
                        });
                    }
                    catch (Exception ex)
                    {
                        source.Cancel();

                        Utilities.Error("Could not find a viable media record...", "Worker Error", "029", false, ex);
                    }

                    video = record.Url;

                    temp = Globals.Temp + "\\" + title + "." + epoch1 + ".m4a";

                    size = FileSize(new Uri(video));

                    Json.Read();

                    downloading = true;

                    Task.Factory.StartNew(async () =>
                    {
                        try
                        {
                            await downloader.DownloadFileTaskAsync(video, temp);
                        }
                        catch (Exception ex)
                        {
                            source.Cancel();

                            Utilities.Error("Could not download addition media files...", "Worker Error", "030", false, ex);
                        }

                        downloading = false;
                    });

                    while (downloading)
                    {
                        token.ThrowIfCancellationRequested();

                        Thread.Sleep(10);
                    }
                    #endregion

                    token.ThrowIfCancellationRequested();
                }

                final = Globals.Temp + "\\" + title + "." + epoch1 + "." + formatraw;

                if (formatraw != format || !audio)
                {
                    #region Get Processing...
                    // If the file needs multiplexing or converting then process...

                    epoch2 = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();

                    Dispatcher.Invoke(delegate ()
                    {
                        Status.Text = "Processing...";

                        Progress.IsIndeterminate = true;
                    });

                    Json.Read();

                    file1 = final;

                    if (!audio)
                    {
                        // If media need multiplexing then multiplex...

                        file2 = Globals.Temp + "\\" + title + "." + epoch1 + ".m4a";

                        Dispatcher.Invoke(delegate ()
                        {
                            Status.Text = "Multiplexing...";
                        });

                        processing = true;

                        final = Globals.Temp + "\\" + title + "." + epoch2 + "." + formatraw;

                        Task.Factory.StartNew(() => GetMultiplex());

                        while (processing)
                        {
                            token.ThrowIfCancellationRequested();

                            Thread.Sleep(10);
                        }

                        token.ThrowIfCancellationRequested();
                    }

                    if (format != formatraw)
                    {
                        // If media need converting then convert...

                        Dispatcher.Invoke(delegate ()
                        {
                            Status.Text = "Converting...";
                        });

                        processing = true;

                        file1 = final;

                        final = Globals.Temp + "\\" + title + "." + epoch2 + "." + format;

                        Task.Factory.StartNew(() => GetConvert());

                        while (processing)
                        {
                            token.ThrowIfCancellationRequested();

                            Thread.Sleep(10);
                        }

                        token.ThrowIfCancellationRequested();
                    }
                    #endregion

                    token.ThrowIfCancellationRequested();
                }

                #region Completed...
                Dispatcher.Invoke(delegate ()
                {
                    Status.Text = "Completing...";
                });

                try
                {
                    // Move final files to the output directory...

                    string outputted = output + "\\" + title + "." + format;

                    File.Delete(outputted);
                    File.Move(final, outputted);
                }
                catch (Exception ex)
                {
                    source.Cancel();

                    Utilities.Error("Could not transfer completed files...", "Storage Error", "031", false, ex);
                }

                try
                {
                    // Deleted all created temperary files...

                    File.Delete(Globals.Temp + "\\" + title + "." + epoch1 + "." + formatraw);

                    if (audio)
                    {
                        File.Delete(Globals.Temp + "\\" + title + "." + epoch1 + ".m4a");
                        File.Delete(Globals.Temp + "\\" + title + "." + epoch2 + "." + formatraw);
                    }

                    if (format != formatraw)
                    {
                        File.Delete(Globals.Temp + "\\" + title + "." + epoch2 + "." + format);
                    }
                }
                catch (Exception ex)
                {
                    Utilities.Error("Could not delete temperary files...", "Storage Error", "032", false, ex);
                }

                Dispatcher.Invoke(delegate ()
                {
                    Status.Text = "Completed!";

                    Progress.IsIndeterminate = false;
                    Progress.Opacity = 0;
                    Progress.Value = 0;
                });
                #endregion
            }
            catch
            {
                if (downloading)
                {
                    downloader.CancelAsync();
                }
            }
        }
        #endregion

        #region FFmpeg Action...
        private void GetMultiplex()
        {
            try
            {
                // Set and run FFmpeg commands and get output...

                string console;

                switch (format)
                {
                    case "webm":
                        console = string.Format($"-i \"{file1}\"  -i \"{file2}\" -c:v copy -c:a libvorbis \"{final}\"");
                        break;
                    default:
                        console = string.Format($"-i \"{file1}\" -i \"{file2}\" -c:v copy -c:a aac \"{final}\"");
                        break;
                }

                ffmpeg.Run(file1, final, console, token);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    source.Cancel();

                    Utilities.Error("Could not run multiplexer without error...", "Worker Error", "033", false, ex);
                }
                
            }

            processing = false;
        }

        private void GetConvert()
        {
            try
            {
                // Set and run FFmpeg commands and get output...

                string console;

                switch (format)
                {
                    case "webm":
                        console = string.Format($"-i \"{file1}\" -c:v vp9 -c:a libvorbis \"{final}\"");
                        break;
                    case "flv":
                        console = string.Format($"-i \"{file1}\" -c:v libx264 -ar 22050 -crf 28 \"{final}\"");
                        break;
                    case "mov":
                        console = string.Format($"-i \"{file1}\" -f mov \"{final}\"");
                        break;
                    case "mp3":
                        console = string.Format($"-i \"{file1}\" -c:a libmp3lame \"{final}\"");
                        break;
                    case "wav":
                        console = string.Format($"-i \"{file1}\" -c:a pcm_s16le \"{final}\"");
                        break;
                    case "ogg":
                        console = string.Format($"-i \"{file1}\" -c:a libvorbis \"{final}\"");
                        break;
                    case "pcm":
                        console = string.Format($"-i \"{file1}\" -c:a pcm_s16le -f s16le -ac 1 -ar 16000 \"{final}\"");
                        break;
                    default:
                        console = string.Format($"-i \"{file1}\" -c:v copy -c:a copy \"{final}\"");
                        break;
                }

                ffmpeg.Run(file1, final, console, token);
            }
            catch (Exception ex)
            {
                if (!token.IsCancellationRequested)
                {
                    source.Cancel();

                    Utilities.Error("Could not run converter without error...", "Worker Error", "034", false, ex);
                }
            }

            processing = false;
        }
        #endregion

        #region Auxiliary...
        protected virtual bool FileLocked(FileInfo f)
        {
            // Check if a file is locked by Windows...

            try
            {
                using (FileStream s = f.Open(FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    s.Close();
                }
            }
            catch (IOException)
            {
                return true;
            }

            return false;
        }

        private static string FileSize(Uri u)
        {
            // Get internet file size...

            try
            {
                WebRequest w = WebRequest.Create(u);
                w.Method = "HEAD";

                using (WebResponse r = w.GetResponse())
                {
                    string f = r.Headers.Get("Content-Length");
                    double m = Math.Round(Convert.ToDouble(f) / 1024.0 / 1024.0, 0);
                    return m.ToString();
                }
            }
            catch (Exception ex)
            {
                Utilities.Error("Could not make HEAD request for remote files...", "Network Error", "035", false, ex);
            }

            return "0";
        }

        private void DownloadProgression(object sender, Downloader.DownloadProgressChangedEventArgs e)
        {
            // Post the downloader progression...

            try
            {
                Dispatcher.Invoke(delegate () {
                    Status.Text = Math.Round(Convert.ToDouble(e.ReceivedBytesSize) / 1024.0 / 1024.0, 0) + 
                    $"/{size}MB ({Math.Round(Convert.ToDouble(e.BytesPerSecondSpeed) / 1024.0 / 1024.0, 1)}MB/s)";

                    Progress.IsIndeterminate = false;
                    Progress.Value = (Int32)e.ProgressPercentage;
                });
            }
            catch (Exception ex)
            {
                Utilities.Error("Could not invoke UI dispatcher on progress event...", "Worker Error", "036", false, ex);
            }
        }

        private void ProcessProgression(object sender, FFmpegProgressEventArgs e)
        {
            // Post the ffmpeg progression...

            try
            {
                Dispatcher.Invoke(delegate () {
                    Progress.IsIndeterminate = false;
                    Progress.Value = e.ProcessedDuration.TotalMilliseconds / e.TotalDuration.TotalMilliseconds * 100;
                });
            }
            catch (Exception ex)
            {
                Utilities.Error("Could not invoke UI dispatcher on progress event...", "Worker Error", "037", false, ex);
            }
        }
        #endregion
    }
}
