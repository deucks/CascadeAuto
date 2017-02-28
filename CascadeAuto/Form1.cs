using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using AForge.Video.FFMPEG;
using System.Drawing.Imaging;
using DirectShowLib;
using DirectShowLib.DES;
using Splicer.Timeline;
using Splicer.Utilities;
using Splicer.Renderer;
using Splicer.WindowsMedia;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Drawing.Drawing2D;
using System.Net;
using System.Diagnostics;

namespace CascadeAuto
{
    public partial class Form1 : Form
    {
        double iProgressBarValue = 0;

        VideoList vList = new VideoList();

        CascadeMerge casMerge = new CascadeMerge();

        long fileSizeBytes = 0;

        int timerCount = 0;


        public Form1()
        {
            InitializeComponent();
            refreshList();
            preSetup();
            timer1.Start();

        }

        private void preSetup()
        {
            textBox4.Text = Properties.Settings.Default.uploadTimer.ToString();

            textBox5.Text = Properties.Settings.Default.visibility.ToString();

            if (!Directory.Exists("VideoData/images/merged"))
            {
                Directory.CreateDirectory("VideoData/images/merged");
            }

            if (!Directory.Exists("VideoData/images/raw"))
            {
                Directory.CreateDirectory("VideoData/images/raw");
            }

            if (!Directory.Exists("VideoData/video"))
            {
                Directory.CreateDirectory("VideoData/video");
            }
        }

        private void refreshList()
        {
            listBox1.DataSource = null;
            listBox1.ValueMember = "VideoName";
            listBox1.DisplayMember = "VideoName";
            
            listBox1.DataSource = vList.loadData();
        }


        private void DownloadFile()
        {
            int eachCount = 0;
            foreach (VideoListModel videoItem in vList.getData())
            {
                eachCount++;
            }

            if (eachCount == 0)
            {
                Console.WriteLine("No videos in que");
                return;
            }
                

            int imageCount = Properties.Settings.Default.imageCount;

            string url = "https://unsplash.it/1920/1080?image=" + imageCount.ToString();

            WebClient client = new WebClient();
            client.DownloadProgressChanged += client_DownloadProgressChanged;
            client.DownloadFileCompleted += client_DownloadFileCompleted;

            //string FileName = url.Substring(url.LastIndexOf("/") + 1, (url.Length - url.LastIndexOf("/") - 1));

            client.DownloadFileAsync(new Uri(url), "VideoData/images/raw/" + imageCount.ToString() + ".jpg");
        }

        private void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                // handle error scenario
                Debug.WriteLine(e.Error.Message.ToString());
                //throw e.Error;
            }
            if (e.Cancelled)
            {
                // handle cancelled scenario
            }

            int imageCount = Properties.Settings.Default.imageCount;

            Image ori = Image.FromFile("VideoData/images/raw/" + imageCount.ToString() + ".jpg");

            if (casMerge.merge(ori, imageCount.ToString()))
            {
                Properties.Settings.Default.imageCount++;
                Properties.Settings.Default.Save();

                makeMovie(imageCount); 
                
            }
        }

        private void startMakingMovie()
        {
            
           
        }




        private void makeMovie(int imageName)
        {
           
            int simpleCount = 0;
            string simpleAudio = "";
            string videoPath = "";
            string title = "";
            string description = "";
            string tags = "";
            foreach (VideoListModel videoItem in vList.getData())
            {
                if (simpleCount == 0)
                {
                    title = videoItem.VideoName;
                    description = videoItem.VideoDescription;
                    tags = videoItem.VideoTags;
                    simpleAudio = videoItem.AudioLocation;
                    videoPath = Application.StartupPath + "\\VideoData\\video\\" + videoItem.VideoName + ".wmv";

                    
                }
                simpleCount++;
            }

            label9.Text = title;

            Console.WriteLine(Application.StartupPath + "\\VideoData\\images\\merged\\" + imageName.ToString() + ".jpg");

            using (ITimeline timeline = new DefaultTimeline(1))
            {
                IGroup group = timeline.AddVideoGroup(32, 1920, 1080);

                ITrack videoTrack = group.AddTrack();
                IClip clip1 = videoTrack.AddImage(Application.StartupPath + "\\VideoData\\images\\merged\\" + imageName.ToString() + ".jpg", 0, 0);

                ITrack audioTrack = timeline.AddAudioGroup().AddTrack();

                IClip audio = audioTrack.AddAudio(simpleAudio);

                IClip clip2 = videoTrack.AddImage(Application.StartupPath + "\\VideoData\\images\\merged\\" + imageName.ToString() + ".jpg", 0, audio.Duration);


                var participant = new PercentageProgressParticipant(timeline);
                participant.ProgressChanged += new EventHandler<Splicer.Renderer.ProgressChangedEventArgs>(participant_ProgressChanged);
                using (
                    WindowsMediaRenderer renderer = new WindowsMediaRenderer(timeline, videoPath, WindowsMediaProfiles.HighQualityVideo))
                {                    
                    renderer.Render();
                }
            }



            progressBar1.Value = 0;

            vList.getData().RemoveAt(0);
            vList.saveData();

            refreshList();

            try
            {
                //2. Get credentials and upload the file
                Run(title, description, videoPath, tags);

            }
            catch (AggregateException ex)
            {
                foreach (var exception in ex.InnerExceptions)
                {
                    Console.WriteLine(exception.Message);
                }
            }

            

        }

        private void participant_ProgressChanged(object sender, Splicer.Renderer.ProgressChangedEventArgs e)
        {
            //Properties.Settings.Default.renderProgress = (int)(e.Progress * 100) + 1;
            //Properties.Settings.Default.Save();
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;

            progressBar1.Value = int.Parse(Math.Truncate(percentage).ToString());
        }

        private void button2_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Filter = "Audio Files (*.mp3, .wav*)|*.mp3*";
            ofd.Multiselect = false;
            ofd.CheckFileExists = true;
            DialogResult r = ofd.ShowDialog();
            if (r == DialogResult.OK)
            {
                label5.Text = ofd.FileName;
            }
        }

        private async Task Run(string title, string description, string videoPath, string tags)
        {
            List<string> tagsArray = tags.Split(',').ToList<string>();

            //2.1 Get credentials
            SetProgress(0);

            UserCredential credentials;

            //2.1.1 Use https://console.developers.google.com/ to get the json file (Credential section)
            using (var stream = new FileStream("client_secret_678539849409-ml9uev6ef6mfs8ha3h437518td6ej19o.apps.googleusercontent.com.json", FileMode.Open, FileAccess.Read))
            {
                credentials = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { YouTubeService.Scope.YoutubeUpload },
                    "user",
                    CancellationToken.None,
                    new FileDataStore("YouTube.Auth.Store")).Result;
            }

            //2.2 Create a YoutubeService instance using our credentials
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credentials,
                ApplicationName = Assembly.GetExecutingAssembly().GetName().Name
            });

            //2.3 Create a video object
            var video = new Video()
            {
                Id = "myIdVeo",

                Status = new VideoStatus
                {
                    PrivacyStatus = Properties.Settings.Default.visibility
                },

                Snippet = new VideoSnippet
                {
                    Title = title,
                    Description = description,



                    Tags = tagsArray
                }
            };

            var filePath = videoPath;

            FileInfo f2 = new FileInfo(filePath);

            fileSizeBytes = f2.Length;

            Console.WriteLine(fileSizeBytes);

            listBox2.Items.Add("UPLOAD STARTED : " + title);

            //2.4 Read and insert the video in youtubeService
            using (var fileStream = new FileStream(filePath, FileMode.Open))
            {
                var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/wmv");
                videosInsertRequest.ProgressChanged += ProgressChanged;
                videosInsertRequest.ResponseReceived += ResponseReceived;

                const int KB = 0x400;
                var minimumChunkSize = 256 * KB;
                videosInsertRequest.ChunkSize = minimumChunkSize * 4;

                //2.4.1 Wait for the upload process
                await videosInsertRequest.UploadAsync();
            }
        }

        private void ProgressChanged(IUploadProgress progress)
        {
            switch (progress.Status)
            {                    
                case UploadStatus.Starting:
                    Console.WriteLine("Start uploading");
                    break;
                case UploadStatus.Uploading:
                    updateProgressBar(progress.BytesSent);
                    break;
                case UploadStatus.Completed:
                    listBox2.Items.Add("UPLOAD COMPLETE");
                    SetProgress(100);
                    break;
                case UploadStatus.Failed:
                    Console.WriteLine("An error prevented the upload from completing.\n{0}", progress.Exception);
                    listBox2.Items.Add("UPLOAD FAILED");
                    break;
            }
        }

        private void updateProgressBar(long bytesIn)
        {
            double percentage = (long)((float)bytesIn / fileSizeBytes * 100);

            Console.WriteLine(bytesIn.ToString());
            Console.WriteLine(percentage.ToString());

            SetProgress(percentage);
        }

        delegate void SetProgressCallback(double percentage);

        private void SetProgress(double percentage)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.textBox1.InvokeRequired)
            {
                SetProgressCallback d = new SetProgressCallback(SetProgress);
                this.Invoke(d, new object[] { percentage });
            }
            else
            {
                this.progressBar2.Value = int.Parse(Math.Truncate(percentage).ToString()); 
            }
        }

        static void ResponseReceived(Video video)
        {
            Console.WriteLine("Video '{0}' was successfully uploaded.", video.Snippet.Title);
        }


        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            foreach (VideoListModel obj in listBox1.SelectedItems)
            {
                Console.WriteLine(obj.VideoName);
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
           
            if (textBox1.Text != "" || textBox2.Text != "" || textBox3.Text != "" || label5.Text != "")
            {
                vList.addVideo(textBox1.Text, textBox2.Text, textBox3.Text, label5.Text);
                MessageBox.Show("Your video has been scheduled");
            }
            else
            {
                MessageBox.Show("MAAATEEEEE YOUR SHIT AINT FILLED IN");
            }

            refreshList();
        }

        private void button5_Click(object sender, EventArgs e)
        {
            foreach (int i in listBox1.SelectedIndices)
            {
                vList.getData().RemoveAt(i);
            }

            vList.saveData();

            refreshList();
            
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DownloadFile();
            timerCount = 0;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {

            if (timerCount == Properties.Settings.Default.uploadTimer)
            {
                timerCount = 0;
                DownloadFile();
            }
            timerCount++;

            label12.Text = ((Properties.Settings.Default.uploadTimer - timerCount) / 60).ToString() + " minutes left | " + (((Properties.Settings.Default.uploadTimer - timerCount) / 60) / 60).ToString() + " hours left";
            
        }

        private void button1_Click(object sender, EventArgs e)
        {
            int timerValue = Convert.ToInt32(textBox4.Text);
            Properties.Settings.Default.uploadTimer = timerValue;
            Properties.Settings.Default.Save();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (textBox5.Text == "private" || textBox5.Text == "unlisted" || textBox5.Text == "public")
            {
                Properties.Settings.Default.visibility = textBox5.Text;
                Properties.Settings.Default.Save();
                
            }
            else
            {
                MessageBox.Show("Invalid mate");
                textBox5.Text = Properties.Settings.Default.visibility;
            }
        }

    }

    public class CascadeMerge
    {
        public bool merge(Image originalImage, string fileName)
        {
            Image cascadeImage = Image.FromFile("cascade.png");

            Bitmap source1 = new Bitmap(originalImage); // your source images - assuming they're the same size
            Bitmap source2 = new Bitmap(cascadeImage);
            var target = new Bitmap(source1.Width, source1.Height, PixelFormat.Format32bppArgb);
            var graphics = Graphics.FromImage(target);
            graphics.CompositingMode = CompositingMode.SourceOver; // this is the default, but just to be clear

            graphics.DrawImage(source1, 0, 0);
            graphics.DrawImage(source2, 0, 0);



            target.Save("VideoData/images/merged/" + fileName + ".jpg", ImageFormat.Png);

            return true;
        }
    }

}
