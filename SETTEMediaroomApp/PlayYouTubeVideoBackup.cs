using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Collections.Generic;

public partial class PlayYouTubeVideo : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        // -------- parameters
        string videoId = Request.QueryString["videoId"];
        if (string.IsNullOrEmpty(videoId))
            videoId = "dQw4w9WgXcQ";

        string videoTitle = Request.QueryString["title"];
        if (!string.IsNullOrEmpty(videoTitle))
            videoTitle = HttpUtility.UrlDecode(videoTitle);
        else
            videoTitle = "YouTube Video";

        if (videoTitle.Length > 120)
            videoTitle = videoTitle.Substring(0, 117) + "...";

        string videosFolder = Server.MapPath("~/youtubeclone/videos_mediaroom_youtube/");
        if (!Directory.Exists(videosFolder))
            Directory.CreateDirectory(videosFolder);

        string outputFile = Path.Combine(videosFolder, videoId + ".mp4");
        string debugFile = Path.Combine(videosFolder, videoId + ".debug.txt");

        int bitrate = 1500; // kbps
        long MIN_SIZE = 1024 * 1024; // 1 MB

        string ffmpegPath = Server.MapPath("~/bin/ffmpeg.exe");

        Log("Play videoId=" + videoId + " title=" + videoTitle);

        try
        {
            bool fileValid = File.Exists(outputFile) &&
                             new FileInfo(outputFile).Length > MIN_SIZE;

            if (!fileValid)
            {
                Log("Video missing or invalid, downloading...");

                // -------- get download URL
                string payload =
                    "{\"videoId\":\"" + JsonEscape(videoId) +
                    "\",\"format\":\"mp4\",\"quality\":\"1080\"}";

                string respJson =
                    HttpPostJson("https://embed.dlsrv.online/api/download/mp4", payload);

                JObject o = JObject.Parse(respJson);

                string downloadUrl = null;
                if (o["url"] != null)
                    downloadUrl = o["url"].ToString();

                if (string.IsNullOrEmpty(downloadUrl))
                    throw new Exception("No download URL");

                Log("Download URL: " + downloadUrl);

                // -------- download via Ethernet 4
                string ethernet4IP = GetEthernet4IP();

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(downloadUrl);
                req.Method = "GET";
                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;

                if (!string.IsNullOrEmpty(ethernet4IP))
                {
                    req.ServicePoint.BindIPEndPointDelegate =
                        delegate (ServicePoint sp, IPEndPoint ep, int retry)
                        {
                            return new IPEndPoint(IPAddress.Parse(ethernet4IP), 0);
                        };
                }

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream input = resp.GetResponseStream())
                {
                    File.WriteAllText(debugFile, "=== FFmpeg log ===\n");

                    Process ffmpeg = new Process();
                    ffmpeg.StartInfo.FileName = ffmpegPath;
                    ffmpeg.StartInfo.Arguments =
                        "-y -i pipe:0 " +
                        "-c:v libx264 -profile:v main -level 3.1 " +
                        "-b:v " + bitrate + "k -maxrate " + bitrate + "k " +
                        "-bufsize " + (bitrate * 2) + "k " +
                        "-preset ultrafast -pix_fmt yuv420p -movflags +faststart " +
                        "-c:a aac -b:a 128k -ac 2 \"" + outputFile + "\"";

                    ffmpeg.StartInfo.UseShellExecute = false;
                    ffmpeg.StartInfo.RedirectStandardInput = true;
                    ffmpeg.StartInfo.RedirectStandardError = true;
                    ffmpeg.StartInfo.CreateNoWindow = true;

                    ffmpeg.ErrorDataReceived += delegate (object s, DataReceivedEventArgs ev)
                    {
                        if (!string.IsNullOrEmpty(ev.Data))
                            File.AppendAllText(debugFile, ev.Data + "\n");
                    };

                    ffmpeg.Start();
                    ffmpeg.BeginErrorReadLine();

                    input.CopyTo(ffmpeg.StandardInput.BaseStream);
                    ffmpeg.StandardInput.Close();
                    ffmpeg.WaitForExit();

                    if (ffmpeg.ExitCode != 0)
                        throw new Exception("FFmpeg exit " + ffmpeg.ExitCode);
                }
            }
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            videoTitle = "Fallback Video";
        }

        string videoUrl =
            Request.Url.GetLeftPart(UriPartial.Authority) +
            "/youtubeclone/videos_mediaroom_youtube/" +
            HttpUtility.UrlEncode(Path.GetFileName(outputFile));

        // -------- MRML
        string mrml =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<uidescription version=\"3.0\">\n" +
            " <MrmlPage id=\"YouTubePlay\" appid=\"lukatube.app/1.0\" width=\"1280\" height=\"720\">\n" +
            "  <Header />\n" +
            "  <Panel>\n" +
            "   <Video id=\"video\" visible=\"true\" width=\"1280\" height=\"720\" " +
            "timeshiftenabled=\"true\" allowtrickmodes=\"true\" " +
            "tuneurl=\"" + EscapeXml(videoUrl) + "\" />\n" +
            "   <Text id=\"Info\" margin=\"rect(30,20,0,0)\" width=\"900\" height=\"90\" " +
            "highlightcolor=\"argb(255,228,0,115)\">\n" +
            "    Video: " + EscapeXml(videoTitle) + "\n" +
            "    \\n Time: {Time} Date: {Date}\n" +
            "    \\n Device: " + EscapeXml(ParseIPTVUserAgent(Request.UserAgent)) + "\n" +
            "   </Text>\n" +
            "  </Panel>\n" +
            " </MrmlPage>\n" +
            "</uidescription>";

        Response.Write(mrml);
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    // -------- helpers

 private string GetEthernet4IP()
{
    foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
    {
        string n = nic.Name.ToLowerInvariant();
        if (n.Contains("wi-fi") || n.Contains("wifi") || n.Contains("wlan"))
        {
            foreach (var a in nic.GetIPProperties().UnicastAddresses)
                if (a.Address.AddressFamily == AddressFamily.InterNetwork)
                    return a.Address.ToString();
        }
    }
    return null;
}


    private string HttpPostJson(string url, string payload)
    {
        HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
        r.Method = "POST";
        r.ContentType = "application/json";
        byte[] b = Encoding.UTF8.GetBytes(payload);
        r.ContentLength = b.Length;
        using (Stream s = r.GetRequestStream())
            s.Write(b, 0, b.Length);
        using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
        using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            return sr.ReadToEnd();
    }

    private static string JsonEscape(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private string EscapeXml(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
    }

private string ParseIPTVUserAgent(string ua)
{
    if (string.IsNullOrEmpty(ua))
        return "Unknown device";

    string os = "";
    string mediaroomVersion = "";
    string vendor = "";
    string model = "";

    // Split client/version from parentheses
    string[] uaParts = ua.Split(new char[] { '(' }, 2);
    string detailsPart = uaParts.Length > 1 ? uaParts[1].Trim() : "";

    if (!string.IsNullOrEmpty(detailsPart))
    {
        detailsPart = detailsPart.TrimEnd(')');
        string[] details = detailsPart.Split(';');

        for (int i = 0; i < details.Length; i++)
            details[i] = details[i].Trim();

        // OS
        if (details.Length > 0)
            os = details[0];

        // Mediaroom version
        for (int i = 0; i < details.Length; i++)
        {
            if (details[i].ToLower().StartsWith("mediaroom"))
            {
                string[] mr = details[i].Split(' ');
                if (mr.Length > 1)
                    mediaroomVersion = string.Join(" ", mr, 1, mr.Length - 1);
                break;
            }
        }

        // Remove HEVC from the end if present
        if (details.Length > 0 &&
            details[details.Length - 1].ToLower().EndsWith("hevc"))
        {
            Array.Resize(ref details, details.Length - 1);
        }

        // Vendor & Model
        if (details.Length >= 1)
            model = details[details.Length - 1];

        if (details.Length >= 2)
            vendor = details[details.Length - 2];
    }

    return "OS: " + os +
           ", Mediaroom: " + mediaroomVersion +
           ", Vendor: " + vendor +
           ", Model: " + model;
}


    private void Log(string msg)
    {
        try
        {
            File.AppendAllText(
                Server.MapPath("~/youtubeclone/log.txt"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss ") + msg + "\n");
        }
        catch { }
    }
}
