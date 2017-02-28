using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace CascadeAuto
{
    public class VideoListModel
    {
        public string VideoName { get; set; }
      
        public string VideoDescription {get;set;}
      
        public string VideoTags {get;set;}
   
        public string AudioLocation { get; set; }
    }

    class VideoList
    {
        List<VideoListModel> videoSchedule = new List<VideoListModel>();

        public VideoList()
        {
            videoSchedule = loadData();
        }


        public void addVideo(string name, string descrption, string tag, string audio)
        {
            videoSchedule.Add(new VideoListModel { VideoName = name, VideoDescription = descrption, VideoTags = tag, AudioLocation = audio});
            saveData();
        }

        public List<VideoListModel> getData()
        {
            return videoSchedule;
        }

        public List<VideoListModel> loadData()
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(List<VideoListModel>));
                TextReader reader = new StreamReader("Videos.xml");
                videoSchedule = (List<VideoListModel>)serializer.Deserialize(reader);
                reader.Close();
                return videoSchedule;
            }
            catch
            {
                //add some code here
            }
            return videoSchedule;
        }



        public void saveData()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(List<VideoListModel>));
            TextWriter writer = new StreamWriter("Videos.xml");
            serializer.Serialize(writer, videoSchedule);
            writer.Close();

        }

    }
}
