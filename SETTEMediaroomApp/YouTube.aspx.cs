using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

public partial class YouTube : Page
{
    private const string SearchApiUrl = "http://172.16.40.100/LukaTube-Downloader-API/search.php";

    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        int pageSize = 12;
        try { pageSize = Math.Max(1, Convert.ToInt32(Request.QueryString["pageSize"] ?? "12")); } catch { pageSize = 12; }

        int offset = 0;
        try { offset = Math.Max(0, Convert.ToInt32(Request.QueryString["offset"] ?? "0")); } catch { offset = 0; }

        string rawSearch = (Request.QueryString["SearchYouTube"] ?? "").Trim();
        string searchLower = rawSearch.ToLowerInvariant();

        string videosFolder = Server.MapPath("~/youtubeclone/videos_mediaroom_youtube/");
        string thumbsFolder = Server.MapPath("~/youtubeclone/thumbs/");

        try
        {
            if (!Directory.Exists(videosFolder)) Directory.CreateDirectory(videosFolder);
            if (!Directory.Exists(thumbsFolder)) Directory.CreateDirectory(thumbsFolder);
        }
        catch
        {
        }

        var videos = new List<VideoItem>();

        try
        {
            string query = string.IsNullOrEmpty(rawSearch) ? "Самоинсталација на дополнителен" : rawSearch;
            string url = SearchApiUrl + "?q=" + HttpUtility.UrlEncode(query);

            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            }
            catch
            {
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Timeout = 15000;
            request.ReadWriteTimeout = 15000;
            request.UserAgent = "LukaTube/1.0";

            string ethernet4IP = GetEthernet4IP();
            if (!string.IsNullOrEmpty(ethernet4IP))
            {
                request.ServicePoint.BindIPEndPointDelegate = delegate (ServicePoint sp, IPEndPoint remoteEndPoint, int retryCount)
                {
                    try
                    {
                        return new IPEndPoint(IPAddress.Parse(ethernet4IP), 0);
                    }
                    catch
                    {
                        return null;
                    }
                };
            }

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
            {
                string json = reader.ReadToEnd();

                if (!string.IsNullOrEmpty(json))
                {
                    JObject root = JObject.Parse(json);
                    JArray data = root["data"] as JArray;

                    if (data != null)
                    {
                        foreach (JToken v in data)
                        {
                            string id = v.Value<string>("videoId") ?? v.Value<string>("id") ?? "";
                            string title = v.Value<string>("title") ?? "Untitled";
                            string videoUrl = v.Value<string>("url") ?? "";
                            string thumb = v.Value<string>("thumbnail") ?? v.Value<string>("imgSrc") ?? "";
                            string duration = v.Value<string>("duration") ?? "";

                            if (string.IsNullOrEmpty(id))
                                continue;

                            videos.Add(new VideoItem
                            {
                                Id = id,
                                Title = title,
                                Url = videoUrl,
                                Img = thumb,
                                Duration = duration,
                                Filename = id + ".mp4"
                            });
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore API errors
        }

        if (!string.IsNullOrEmpty(searchLower))
        {
            videos = videos.FindAll(delegate (VideoItem vi)
            {
                if (vi == null || string.IsNullOrEmpty(vi.Title)) return false;
                return vi.Title.ToLowerInvariant().Contains(searchLower);
            });
        }

        int total = videos.Count;
        if (offset >= total) offset = 0;

        int take = Math.Min(pageSize, Math.Max(0, total - offset));
        List<VideoItem> pageVideos = (take > 0) ? videos.GetRange(offset, take) : new List<VideoItem>();

        HashSet<string> savedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(videosFolder))
            {
                string[] filesOnDisk = Directory.GetFiles(videosFolder, "*.mp4", SearchOption.TopDirectoryOnly);
                foreach (string f in filesOnDisk)
                {
                    savedFiles.Add(f);
                    try { savedFiles.Add(Path.GetFileName(f)); } catch { }
                }
            }
        }
        catch
        {
        }

        string deviceGuid = Request.QueryString["DeviceGuid"] ?? "";
        int lukifyUserId = 0;

        if (!string.IsNullOrEmpty(deviceGuid))
        {
            try
            {
                string clientApiUrl = "http://172.16.40.100/get_lukify_clientidforuserid.php?deviceguid="
                                      + HttpUtility.UrlEncode(deviceGuid);

                var reqClient = (HttpWebRequest)WebRequest.Create(clientApiUrl);
                reqClient.Method = "GET";
                reqClient.Timeout = 4000;
                reqClient.ReadWriteTimeout = 4000;

                using (var respClient = (HttpWebResponse)reqClient.GetResponse())
                using (var sr = new StreamReader(respClient.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    JObject obj = JObject.Parse(json);

                    if (obj != null && obj.Value<string>("status") == "success")
                    {
                        int tmpId;
                        if (int.TryParse(obj.Value<string>("userid") ?? "0", out tmpId))
                            lukifyUserId = tmpId;
                    }
                }
            }
            catch
            {
            }
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("  <MrmlPage id=\"YouTubeList\" appid=\"lukatube.app/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("    <Header />");
        sb.AppendLine("    <Actions>");
        sb.AppendLine("      <Action name=\"SearchYouTube\" type=\"submit\" data=\"SearchYouTube\" url=\"page:" + EscapeXml(Request.Url.GetLeftPart(UriPartial.Authority) + "/SETTEMediaroomApp/YouTube.aspx") + "\" method=\"GET\" />");
        sb.AppendLine("    </Actions>");
        sb.AppendLine("    <Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");

        string titleSuffix = "";
        if (total > 0)
        {
            int end = Math.Min(offset + pageSize, total);
            titleSuffix = " (showing " + (offset + 1) + "-" + end + " of " + total + ")";
        }

        sb.AppendLine("      <Text id=\"Title\" top=\"10\" left=\"20\" width=\"900\" height=\"30\" fontstyle=\"Reg26\" foreground=\"argb(255,228,0,115)\">YouTube - Videos" + EscapeXml(titleSuffix) + "</Text>");
        sb.AppendLine("      <EditText id=\"SearchYouTube\" top=\"50\" left=\"20\" width=\"400\" height=\"40\" visible=\"true\" hint=\"Search videos...\">" + EscapeXml(rawSearch) + "</EditText>");
        sb.AppendLine("      <Button id=\"SearchButton\" top=\"50\" left=\"430\" width=\"140\" height=\"40\" justification=\"center\">");
        sb.AppendLine("        <Text>Search YouTube</Text>");
        sb.AppendLine("        <Actions><Event type=\"onclick\" action=\"SearchYouTube\"/></Actions>");
        sb.AppendLine("      </Button>");

        int thumbWidth = 200;
        int thumbHeight = 120;
        int gapX = 20;
        int gapY = 30;
        int perRow = 5;
        int startTop = 120;
        int startLeft = 20;

        for (int idx = 0; idx < pageVideos.Count; idx++)
        {
            VideoItem v = pageVideos[idx];
            int row = idx / perRow;
            int col = idx % perRow;
            int top = startTop + row * (thumbHeight + gapY);
            int left = startLeft + col * (thumbWidth + gapX);

            string fullLocalPath = Path.Combine(videosFolder, v.Filename);

            bool isSaved = false;
            try
            {
                isSaved = File.Exists(fullLocalPath)
                          || savedFiles.Contains(fullLocalPath)
                          || savedFiles.Contains(Path.GetFileName(fullLocalPath));
            }
            catch
            {
                isSaved = false;
            }

            string fileState = isSaved ? "READY" : "REMOTE";

            string thumbLocalUrl = v.Img;
            try
            {
                string localThumbFile = EnsureThumbnailSaved(v.Img, v.Id, thumbsFolder);
                if (!string.IsNullOrEmpty(localThumbFile))
                {
                    string thumbFilename = Path.GetFileName(localThumbFile);
                    thumbLocalUrl = Request.Url.GetLeftPart(UriPartial.Authority).TrimEnd('/') + "/youtubeclone/thumbs/" + HttpUtility.UrlEncode(thumbFilename);
                }
            }
            catch
            {
                thumbLocalUrl = v.Img;
            }

            var playHrefBuilder = new StringBuilder();
            playHrefBuilder.Append(Request.Url.GetLeftPart(UriPartial.Authority));
            playHrefBuilder.Append("/SETTEMediaroomApp/PlayYouTubeVideo.aspx?videoId=");
            playHrefBuilder.Append(HttpUtility.UrlEncode(v.Id));
            playHrefBuilder.Append("&title=");
            playHrefBuilder.Append(HttpUtility.UrlEncode(v.Title));

            if (lukifyUserId > 0)
                playHrefBuilder.Append("&userid=" + HttpUtility.UrlEncode(lukifyUserId.ToString()));

            if (!string.IsNullOrEmpty(deviceGuid))
                playHrefBuilder.Append("&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid));

            string playHref = playHrefBuilder.ToString();

            sb.AppendLine("      <Button id=\"videoBtn" + idx + "\" top=\"" + top + "\" left=\"" + left + "\" width=\"" + thumbWidth + "\" height=\"" + thumbHeight + "\" href=\"page:" + EscapeXml(playHref) + "\">");
            sb.AppendLine("        <Image top=\"-50\" left=\"0\" width=\"" + thumbWidth + "\" height=\"" + thumbHeight + "\" url=\"" + EscapeXml(thumbLocalUrl) + "\">");
            sb.AppendLine("        </Image>");
            sb.AppendLine("        <Text top=\"" + (thumbHeight - 45) + "\" left=\"0\" width=\"" + thumbWidth + "\" height=\"15\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\">" + EscapeXml(v.Title) + "</Text>");
            sb.AppendLine("        <Text top=\"" + (thumbHeight - 30) + "\" left=\"0\" width=\"" + thumbWidth + "\" height=\"15\" fontstyle=\"Reg16\" foreground=\"argb(255,200,200,200)\">" + EscapeXml(v.Duration) + "</Text>");
            sb.AppendLine("        <Text top=\"" + (thumbHeight - 15) + "\" left=\"0\" width=\"" + thumbWidth + "\" height=\"15\" fontstyle=\"Reg16\" foreground=\"argb(255,180,180,180)\">" + EscapeXml(fileState) + "</Text>");
            sb.AppendLine("      </Button>");
        }

        int nextOffset = offset + pageSize;
        if (nextOffset < total)
        {
            string nextUrl = Request.Url.GetLeftPart(UriPartial.Authority) + "/SETTEMediaroomApp/YouTube.aspx?offset=" + nextOffset + "&pageSize=" + pageSize;
            if (!string.IsNullOrEmpty(rawSearch)) nextUrl += "&SearchYouTube=" + HttpUtility.UrlEncode(rawSearch);

            int rowsRendered = (pageVideos.Count + perRow - 1) / perRow;
            int loadTop = startTop + rowsRendered * (thumbHeight + gapY);

            sb.AppendLine("      <Button id=\"loadMoreBtn\" top=\"" + loadTop + "\" left=\"20\" width=\"300\" height=\"40\" href=\"page:" + EscapeXml(nextUrl) + "\">");
            sb.AppendLine("        <Text top=\"0\" left=\"0\" width=\"300\" height=\"40\">Load more videos...</Text>");
            sb.AppendLine("      </Button>");
        }

        sb.AppendLine("    </Panel>");
        sb.AppendLine("  </MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
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

    private string JsonEscape(string s)
    {
        if (s == null) return "";
        return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private string GetEthernet4IP()
    {
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                string lname = (nic.Name ?? "").ToLowerInvariant();
                string ldesc = (nic.Description ?? "").ToLowerInvariant();

                if (lname.Contains("ethernet 4") || lname.Contains("ethernet4") || lname.Contains("eth4") ||
                    ldesc.Contains("ethernet 4") || ldesc.Contains("ethernet4") || ldesc.Contains("eth4"))
                {
                    var ipProps = nic.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                            return addr.Address.ToString();
                    }
                }
            }
        }
        catch
        {
        }

        return null;
    }

    private string EnsureThumbnailSaved(string remoteUrl, string videoId, string thumbsFolder)
    {
        if (string.IsNullOrEmpty(remoteUrl) || string.IsNullOrEmpty(videoId) || string.IsNullOrEmpty(thumbsFolder))
            return null;

        if (!remoteUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !remoteUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return null;

        string ext = ".jpg";
        try
        {
            Uri u = new Uri(remoteUrl);
            string e = Path.GetExtension(u.AbsolutePath);
            if (!string.IsNullOrEmpty(e) && e.Length <= 5) ext = e;
        }
        catch
        {
        }

        string safeFilename = SanitizeFileName(videoId) + ext;
        string fullPath = Path.Combine(thumbsFolder, safeFilename);

        try
        {
            if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 200)
                return fullPath;
        }
        catch
        {
        }

        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(remoteUrl);
            req.Method = "GET";
            req.Timeout = 7000;
            req.ReadWriteTimeout = 7000;
            req.UserAgent = "LukaTubeThumbFetcher/1.0";

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream input = resp.GetResponseStream())
            using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                input.CopyTo(fs);
            }

            if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 200)
                return fullPath;

            try { File.Delete(fullPath); } catch { }
        }
        catch
        {
            try { if (File.Exists(fullPath)) File.Delete(fullPath); } catch { }
            return null;
        }

        return null;
    }

    private string SanitizeFileName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "thumb";
        foreach (char c in Path.GetInvalidFileNameChars())
            s = s.Replace(c, '_');
        s = Regex.Replace(s, @"\s+", "_");
        return s;
    }

    private class VideoItem
    {
        public string Id;
        public string Title;
        public string Url;
        public string Img;
        public string Duration;
        public string Filename;
    }
}