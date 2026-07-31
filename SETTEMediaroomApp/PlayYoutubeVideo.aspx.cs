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
using System.Threading.Tasks;

public partial class PlayYouTubeVideo : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
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

        long MIN_SIZE = 1024 * 1024; // 1 MB

        string ffmpegPath = Server.MapPath("~/bin/ffmpeg.exe");

        Log("Play videoId=" + videoId + " title=" + videoTitle);

        string videoUrl = "";

        try
        {
            bool fileValid = File.Exists(outputFile) &&
                             new FileInfo(outputFile).Length > MIN_SIZE;

            if (!fileValid)
            {
                Log("Video missing or invalid, downloading...");

                // -------- start download with NEW API
                string youtubeUrl = "https://www.youtube.com/watch?v=" + HttpUtility.UrlEncode(videoId);
                string startApiUrl =
                    "http://172.16.40.100/LukaTube-Downloader-API/index.php?url=" +
                    HttpUtility.UrlEncode(youtubeUrl);

                string startJson = HttpGet(startApiUrl);
                JObject startObj = JObject.Parse(startJson);

                string statusUrl = startObj.Value<string>("status_url");
                string downloadUrl =
                    startObj.Value<string>("video_url") ??
                    startObj.Value<string>("url") ??
                    startObj.Value<string>("download_url");

                // If API returned final URL directly, use it.
                // Otherwise poll the status endpoint until completed.
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    if (string.IsNullOrEmpty(statusUrl))
                        throw new Exception("No status URL returned by API");

                    downloadUrl = WaitForCompletedVideoUrl(statusUrl, 15 * 60 * 1000);
                }

                if (string.IsNullOrEmpty(downloadUrl))
                    throw new Exception("No download URL returned from API");

                Log("Download URL: " + downloadUrl);

                // -------- download via Ethernet 4
                string ethernet4IP = GetEthernet4IP();

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(downloadUrl);
                req.Method = "GET";
                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;
                req.AllowAutoRedirect = true;
                req.UserAgent = "SETTEMediaroomApp/1.0";

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

                    using (Process ffmpeg = new Process())
                    {
                        ffmpeg.StartInfo.FileName = ffmpegPath;
                        ffmpeg.StartInfo.Arguments =
                            "-y -i pipe:0 " +
                            "-c:v libx264 -profile:v main -level 3.1 " +
                            "-preset veryfast -crf 23 -pix_fmt yuv420p -movflags +faststart " +
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

            // -------- FINAL STB URL
            // Use the public URL of the saved MP4 file, not the downloader API URL.
            if (File.Exists(outputFile))
            {
                videoUrl = Request.Url.GetLeftPart(UriPartial.Authority)
                           + "/youtubeclone/videos_mediaroom_youtube/"
                           + Uri.EscapeDataString(Path.GetFileName(outputFile));
            }
            else
            {
                throw new Exception("Final video file not found");
            }
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex.Message);
            videoTitle = "Fallback Video";
            videoUrl = "";
        }

        // ---------- DeviceGuid (if caller provided)
        string deviceGuid = Request.QueryString["DeviceGuid"] ?? "";

        // ---------- Get Lukify client/user id (same endpoint as PlayVideo.aspx)
        int lukifyUserId = 0;
        try
        {
            if (!string.IsNullOrEmpty(deviceGuid))
            {
                string clientApiUrl = "http://172.16.40.100/get_lukify_clientidforuserid.php?deviceguid="
                                      + HttpUtility.UrlEncode(deviceGuid);

                HttpWebRequest reqClient = (HttpWebRequest)WebRequest.Create(clientApiUrl);
                reqClient.Method = "GET";
                reqClient.Timeout = 4000;
                reqClient.ReadWriteTimeout = 4000;

                using (HttpWebResponse respClient = (HttpWebResponse)reqClient.GetResponse())
                using (StreamReader sr = new StreamReader(respClient.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    try
                    {
                        JObject obj = JObject.Parse(json);
                        if (obj != null && obj.Value<string>("status") == "success")
                        {
                            try { lukifyUserId = obj.Value<int>("userid"); }
                            catch
                            {
                                int tmp;
                                if (int.TryParse(obj.Value<string>("userid") ?? "0", out tmp))
                                    lukifyUserId = tmp;
                            }
                        }
                    }
                    catch
                    {
                        // ignore parse errors
                    }
                }
            }
        }
        catch
        {
            // ignore API errors
        }

        // ---------- Build SendTo URL (public-friendly) and include userid if available
        string sendToUrl = "";
        if (!string.IsNullOrEmpty(videoUrl))
        {
            string sendBase = Request.Url.GetLeftPart(UriPartial.Authority) + "/SETTEMediaroomApp/SendTo.aspx";
            sendToUrl = sendBase + "?message=" + HttpUtility.UrlEncode(videoUrl, Encoding.UTF8);

            if (lukifyUserId > 0)
                sendToUrl += "&userid=" + lukifyUserId.ToString();

            if (!string.IsNullOrEmpty(deviceGuid))
                sendToUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);
        }

        // ---------- Hard Disk button URL
        string hardDiskUrl = @"page:\Hard Disk\TV2ClientCE\Content\channeltvhd.xml";

        // Build optional SendTo button XML only if we have lukify user
        string sendToButtonXml = "";
        if (!string.IsNullOrEmpty(sendToUrl) && lukifyUserId > 0)
        {
            sendToButtonXml =
                "   <Button id=\"SendToButton\" " +
                "top=\"20\" left=\"780\" width=\"220\" height=\"50\" " +
                "focusable=\"false\" " +
                "href=\"page:" + EscapeXml(sendToUrl) + "\" " +
                "background=\"argb(0,0,0,0)\">\n" +
                "    <Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\">SEND TO</Text>\n" +
                "   </Button>\n";
        }

 string deviceInfo = ParseIPTVUserAgent(Request.UserAgent ?? "");

string frontPanelText = videoTitle;

if (string.IsNullOrEmpty(frontPanelText))
    frontPanelText = "LukaTube";


string targetPage =
    "http://172.16.40.101/SETTEMediaroomApp/LukaTube.aspx";


string openChannelsSearch =
    "page:http://172.16.40.101/SETTEMediaroomApp/SearchChannels.aspx";


string openArtistSearch =
    "page:http://172.16.40.101/SETTEMediaroomApp/MenuOrSearchArtist.aspx";


string diagnosticsPage =
    "page:file:///Diagnostics.xml";


// Hard Disk Channel TV HD
string channelTVHD =
    "page:file:///ChannelTVHD.xml";

     // -------- MRML
StringBuilder sb = new StringBuilder();

sb.Append(@"<?xml version=""1.0"" encoding=""utf-8""?>");
sb.Append(@"<uidescription version=""3.0"">");
sb.Append(@"<MrmlPage id=""YouTubePlay"" appid=""lukatube.app/1.0"" width=""1280"" height=""720"">");

sb.Append(@"<DataSource id=""TVSource"" uri=""local://tv"" autoload=""true""/>");
sb.Append(@"<DataSource id=""ZoetropeDataSource"" uri=""local://zoetrope"" autoload=""true""/>");
sb.Append(@"<DataSource id=""ZoetropeDataSourceForActions"" uri=""local://zoetrope"" autoload=""true""/>");

sb.Append(@"<Scripts>");
sb.Append(@"<Script src=""../../Scripts/mrml/Play.crunch.js""/>");
sb.Append(@"</Scripts>");

sb.Append(@"
<Extensions>
<Extension name=""fullscreenTVControls"" type=""9"" urn=""urn:microsoft:mediaroom:extension:fullscreentvcontrols:1"">
<Param name=""video"" value=""backgroundVideoPlayer""/>
<Param name=""browsebar"" value=""on""/>
<Param name=""channelbar"" value=""off""/>
<Param name=""channelentry"" value=""on""/>
<Param name=""optionspanel"" value=""on""/>
<Param name=""recentpanel"" value=""on""/>
<Param name=""rosette"" value=""on""/>
<Param name=""seekbar"" value=""on""/>
</Extension>
</Extensions>");

sb.Append("<Header/>");


sb.Append(
"<FrontPanel id=\"fp1\" visible=\"true\" LoadAsync=\"false\" DefaultMessage=\"" +
EscapeXml(frontPanelText) +
"\">" +
EscapeXml(frontPanelText) +
"</FrontPanel>");



sb.Append("<Actions>");

sb.AppendLine(
@"      <Action name=""OpenHardDisk""
type=""submit""
url=""" +
EscapeXml(channelTVHD) +
@"""
method=""GET""/>");

sb.AppendLine(
@"      <Event type=""onkey:select""
action=""OpenHardDisk""/>");

// Media error
sb.Append(
"<Action name=\"showMediaError\" type=\"dialog\" " +
"data=\"YouTube Video cannot be played. Please try again later until it has been converted.\"/>");


// Media end
sb.Append(
"<Action name=\"finishedAction\" type=\"navigate\" data=\"back\"/>");

sb.Append(
"<Event type=\"onmediaend\" action=\"finishedAction\"/>");

sb.Append(
"<Event type=\"onmediaerror\" action=\"showMediaError\"/>");



// Guide -> LukaTube
sb.Append(
"<Action name=\"OpenLukaTube\" type=\"submit\" " +
"data=\"lbltuneMainChannel\" " +
"url=\"page:" +
EscapeXml(targetPage) +
"\" method=\"GET\"/>");

sb.Append(
"<Event type=\"onkey:guide\" action=\"OpenLukaTube\"/>");



// Menu -> Search
sb.Append(
"<Action name=\"OpenArtistSearch\" type=\"submit\" " +
"url=\"" +
EscapeXml(openArtistSearch) +
"\" method=\"GET\"/>");

sb.Append(
"<Event type=\"onkey:menu\" action=\"OpenArtistSearch\"/>");



// Blue -> Channels
sb.Append(
"<Action name=\"OpenChannels\" type=\"submit\" " +
"url=\"" +
EscapeXml(openChannelsSearch) +
"\" method=\"GET\"/>");

sb.Append(
"<Event type=\"onkey:blue\" action=\"OpenChannels\"/>");



// Info -> Diagnostics
sb.Append(
"<Action name=\"OpenDiagnostics\" type=\"submit\" " +
"url=\"" +
EscapeXml(diagnosticsPage) +
"\" method=\"GET\"/>");

sb.Append(
"<Event type=\"onkey:info\" action=\"OpenDiagnostics\"/>");


sb.Append("</Actions>");



// Video player
sb.Append(
"<Video SessionName=\"FULLSCREEN\" " +
"id=\"backgroundVideoPlayer\" " +
"width=\"1280\" height=\"720\" " +
"visible=\"true\" " +
"showcontrols=\"true\" " +
"showbusyindicator=\"true\" " +
"tuneurl=\"" +
EscapeXml(videoUrl + "#urn:microsoft:mediaroom:event:media:state:seekbar") +
"\">" +
"</Video>");



// Text overlay
sb.Append(
"<Text id=\"VideoInfo\" " +
"alignment=\"left\" " +
"fontstyle=\"Reg18\" " +
"foreground=\"argb(255,255,255,255)\" " +
"margin=\"rect(20,20,0,0)\" " +
"width=\"1200\" height=\"160\">");

sb.Append(
EscapeXml(
"Device: " + deviceInfo +
"\nVideo: " + videoTitle +
"\nPress UP/Down, Channel UP/Channel Down for Next/Previous, GUIDE for LukaTube, LEFT/Green to Send To, MENU/Blue/Music Button to search artist, Double Press SELECT for Video Controls"
));

sb.Append("</Text>");

sb.Append("</MrmlPage>");
sb.Append("</uidescription>");

Response.Clear();
Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
Response.ContentEncoding = Encoding.UTF8;

Response.Write(sb.ToString());

HttpContext.Current.ApplicationInstance.CompleteRequest();


// Output XML
Response.Clear();
Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
Response.ContentEncoding = Encoding.UTF8;
Response.Write(sb.ToString());
Response.Flush();
HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private string WaitForCompletedVideoUrl(string statusUrl, int timeoutMs)
    {
        DateTime start = DateTime.UtcNow;

        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            string json = HttpGet(statusUrl);

            if (!string.IsNullOrWhiteSpace(json))
            {
                JObject obj = JObject.Parse(json);

                string status = (obj.Value<string>("status") ?? "").ToLowerInvariant();
                string videoUrl =
                    obj.Value<string>("video_url") ??
                    obj.Value<string>("url") ??
                    obj.Value<string>("download_url");

                if (status == "completed" && !string.IsNullOrEmpty(videoUrl))
                    return videoUrl;

                if (status == "failed")
                {
                    string message = obj.Value<string>("message") ?? "Download failed";
                    throw new Exception(message);
                }
            }

            Task.Delay(2000).Wait();
        }

        throw new Exception("Timed out waiting for download to complete");
    }

    private string GetEthernet4IP()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                string n = nic.Name.ToLowerInvariant();
                if (n.Contains("ethernet 7") || n.Contains("eth7"))
                {
                    foreach (var a in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (a.Address.AddressFamily == AddressFamily.InterNetwork)
                            return a.Address.ToString();
                    }
                }
            }
        }
        catch
        {
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

    private string HttpGet(string url)
    {
        HttpWebRequest r = (HttpWebRequest)WebRequest.Create(url);
        r.Method = "GET";
        r.Timeout = 120000;
        r.ReadWriteTimeout = 120000;
        r.UserAgent = "SETTEMediaroomApp/1.0";

        string eth4IP = GetEthernet4IP();
        if (!string.IsNullOrEmpty(eth4IP))
        {
            r.ServicePoint.BindIPEndPointDelegate = (sp, ep, retry) =>
            {
                try
                {
                    return new IPEndPoint(IPAddress.Parse(eth4IP), 0);
                }
                catch
                {
                    return null;
                }
            };
        }

        using (HttpWebResponse resp = (HttpWebResponse)r.GetResponse())
        using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
        {
            return sr.ReadToEnd();
        }
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

        string[] uaParts = ua.Split(new char[] { '(' }, 2);
        string detailsPart = uaParts.Length > 1 ? uaParts[1].Trim() : "";

        if (!string.IsNullOrEmpty(detailsPart))
        {
            detailsPart = detailsPart.TrimEnd(')');
            string[] details = detailsPart.Split(';');

            for (int i = 0; i < details.Length; i++)
                details[i] = details[i].Trim();

            if (details.Length > 0)
                os = details[0];

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

            if (details.Length > 0 &&
                details[details.Length - 1].ToLower().EndsWith("hevc"))
            {
                Array.Resize(ref details, details.Length - 1);
            }

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
        catch
        {
        }
    }
}