using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;

public partial class GetVideoFromSong : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string songUrl = Request.QueryString["song_url"] ?? "";
        string deviceGuid = Request.QueryString["DeviceGuid"] ?? "";

        if (string.IsNullOrEmpty(songUrl))
        {
            ShowError("Missing song_url");
            return;
        }

        try
        {
            // Call API to get video info
            string apiUrl = "http://172.16.40.100/get_video_from_song.php?song_url=" + HttpUtility.UrlEncode(songUrl);
            string json = HttpGet(apiUrl);

            JavaScriptSerializer js = new JavaScriptSerializer();
            ApiResponse resp = js.Deserialize<ApiResponse>(json);

            // Classic null checks
            if (resp == null || resp.result == null || resp.result.video == null || string.IsNullOrEmpty(resp.result.video.video_url))
            {
                ShowError("Video not found");
                return;
            }

            string videoUrl = resp.result.video.video_url;
            videoUrl = videoUrl.Replace("https://lukaserver.ddns.net", "http://172.16.40.100");

            // Build video name as "Artist - Title"
            string artist = resp.result.video.artist ?? "";
            string title = resp.result.video.title ?? "";

            string videoName = "";
            if (!string.IsNullOrEmpty(artist) && !string.IsNullOrEmpty(title))
            {
                videoName = artist + " - " + title;
            }
            else if (!string.IsNullOrEmpty(title))
            {
                videoName = title;
            }
            else
            {
                videoName = Path.GetFileNameWithoutExtension(videoUrl);
            }

            // Redirect to PlayVideo.aspx with artist + title as parameters
            RedirectToPlayer(videoUrl, videoName, deviceGuid, artist, title);
        }
        catch (Exception ex)
        {
            ShowError("Error: " + ex.Message);
        }
    }

    private string HttpGet(string url)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
        req.Method = "GET";
        req.Timeout = 10000;

        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
        using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
        {
            return sr.ReadToEnd();
        }
    }

    private void RedirectToPlayer(string videoUrl, string videoName, string deviceGuid, string artist, string title)
    {
        string playUrl = "PlayVideo.aspx?" +
                         "video_url=" + HttpUtility.UrlEncode(videoUrl) +
                         "&video_name=" + HttpUtility.UrlEncode(videoName) +
                         "&artist=" + HttpUtility.UrlEncode(artist) +
                         "&title=" + HttpUtility.UrlEncode(title) +
                         "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid) +
                         "&LocalFolder=false";

        Response.Redirect(playUrl);
    }

    private void ShowError(string msg)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml";
        Response.ContentEncoding = Encoding.UTF8;

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel>");
        sb.AppendLine(string.Format(
            "<Text top=\"200\" left=\"200\" width=\"900\" height=\"100\" fontstyle=\"Reg32\" foreground=\"argb(255,255,60,60)\">{0}</Text>",
            HttpUtility.HtmlEncode(msg)));
        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    // ===== Models =====
    public class ApiResponse
    {
        public QueryData query { get; set; }
        public ResultData result { get; set; }
    }

    public class QueryData
    {
        public string artist { get; set; }
        public string title { get; set; }
        public string song_url { get; set; }
    }

    public class ResultData
    {
        public string name { get; set; }
        public VideoData video { get; set; }
    }

    public class VideoData
    {
        public string artist { get; set; }
        public string title { get; set; }
        public string video_url { get; set; }
        public string thumb_url { get; set; }
    }
}