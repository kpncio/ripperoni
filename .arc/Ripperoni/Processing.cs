using System.Windows.Forms;
using System.Threading.Tasks;

using Javi.FFmpeg;

namespace Ripperoni
{
    public partial class Processing : UserControl
    {
        private readonly string first_epoch;
        private readonly string second_epoch;

        private string first_file;
        private string second_file;

        private readonly string real_format;
        private readonly string down_format;
        private readonly string title;
        private readonly string epoch;
        private readonly Processor process;

        private string temp_multiplex;
        private string temp_convert;
        private readonly int progress = 0;
        private readonly int steps = 0;
        private int step = 0;

        public Processing(Processor p, string e1, string e2, string r, string d, string t, string e)
        {
            first_epoch = e1;
            second_epoch = e2;

            if (!string.IsNullOrEmpty(e2))
            {
                steps++;
            }

            if (r != d)
            {
                steps++;
            }

            real_format = r;
            down_format = d;
            title = t;
            epoch = e;
            process = p;

            process.done_tertiary = false;

            InitializeComponent();

            Progression();

            ProcessMedia();
        }

        private void Processing_Load(object sender, System.EventArgs e)
        {
            Size = new System.Drawing.Size(400, 25);
        }

        #region Main Thread
        private async void ProcessMedia()
        {
            temp_multiplex = Globals.Temp + "\\" + title + "." + epoch + "." + down_format;
            temp_convert = Globals.Temp + "\\" + title + "." + epoch + "." + real_format;

            Title.Text = "(Processing) " + title.Truncate(38);

            Json.Read();

            first_file = Globals.Temp + "\\" + title + "." + first_epoch + "." + down_format;

            if (!string.IsNullOrEmpty(second_epoch))
            {
                second_file = Globals.Temp + "\\" + title + "." + second_epoch + ".m4a";

                step++;
                Progression();

                await Task.Run(() => Multiplex());

                first_file = temp_multiplex;
            }

            if (real_format != down_format)
            {
                step++;
                Progression();

                await Task.Run(() => Convert());
            }

            Progression();

            process.done_tertiary = true;

            Done();
        }

        private void Done()
        {
            Task.Factory.StartNew(() => {
                bool ending = true;
                while (ending)
                {
                    if (process.done)
                    {
                        process.Remove(this);
                        ending = false;
                    }

                    System.Threading.Thread.Sleep(10);
                }
            });
        }

        private void Multiplex()
        {
            try
            {
                using (var ffmpeg = new FFmpeg(Globals.Real + @"FFmpeg.exe"))
                {
                    string i1 = first_file; string i2 = second_file; string o = temp_multiplex;

                    string c;

                    switch (real_format)
                    {
                        case "webm":
                            c = string.Format($"-i \"{i1}\"  -i \"{i2}\" -c:v copy -c:a libvorbis \"{o}\"");
                            break;
                        default:
                            c = string.Format($"-i \"{i1}\" -i \"{i2}\" -c:v copy -c:a aac \"{o}\"");
                            break;
                    }

                    ffmpeg.Run(i1, o, c);
                }
            }
            catch
            {
                Utilities.Error("Could not run FFmpeg process without error (Multiplexer)...", "Error", true);
            }
        }

        private void Convert()
        {
            try
            {
                using (var ffmpeg = new FFmpeg(Globals.Real + @"FFmpeg.exe"))
                {
                    string i = first_file; string o = temp_convert;

                    string c;

                    switch (real_format)
                    {
                        case "webm":
                            c = string.Format($"-i \"{i}\" -c:v vp9 -c:a libvorbis \"{o}\"");
                            break;
                        case "flv":
                            c = string.Format($"-i \"{i}\" -c:v libx264 -ar 22050 -crf 28 \"{o}\"");
                            break;
                        case "mov":
                            c = string.Format($"-i \"{i}\" -f mov \"{o}\"");
                            break;
                        case "mp3":
                            c = string.Format($"-i \"{i}\" -c:a libmp3lame \"{o}\"");
                            break;
                        case "wav":
                            c = string.Format($"-i \"{i}\" -c:a pcm_s16le \"{o}\"");
                            break;
                        case "ogg":
                            c = string.Format($"-i \"{i}\" -c:a libvorbis \"{o}\"");
                            break;
                        case "pcm":
                            c = string.Format($"-i \"{i}\" -c:a pcm_s16le -f s16le -ac 1 -ar 16000 \"{o}\"");
                            break;
                        default:
                            c = string.Format($"-i \"{i}\" -c:v copy -c:a copy \"{o}\"");
                            break;
                    }

                    ffmpeg.Run(i, o, c);
                }
            }
            catch
            {
                Utilities.Error("Could not run FFmpeg process without error (Converter)...", "Error", true);
            }
        }
        #endregion

        #region Auxiliary
        //private void Progression(int e)
        //{
        //    try
        //    {
        //        Status.Invoke((MethodInvoker)delegate
        //        {
        //            Status.Text = "[Step " + step + "/" + steps + "]:";
        //        });

        //        progress = e;

        //        int total = (progress + ((step - 1) * 100)) / steps;

        //        Progress.Invoke((MethodInvoker)delegate
        //        {
        //            Progress.Style = ProgressBarStyle.Blocks;
        //            Progress.Value = total;
        //        });
        //    }
        //    catch
        //    {
        //        Utilities.Error("Could not invoke UI controls on progress event...", "Error", false);
        //    }
        //}

        private void Progression()
        {
            try
            {
                Status.Text = "[Step " + step + "/" + steps + "]:";

                int total = (step * 100) / (steps) + (progress * 0);

                Progress.Style = ProgressBarStyle.Blocks;
                Progress.Value = total;
            }
            catch
            {
                Utilities.Error("Could not invoke UI controls on progress event...", "Error", false);
            }
        }
        #endregion
    }
}
