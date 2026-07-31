using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class PlayLukifyMusicSong : Page
{
protected void Page_Load(object sender, EventArgs e)
{
string songUrl = Request.QueryString["song_url"] ?? "";
string coverUrl = Request.QueryString["cover"] ?? "";
string title = Request.QueryString["title"] ?? "Now Playing";
string artist = Request.QueryString["artist"] ?? "Unknown Artist";
string deviceGuid = Request.QueryString["DeviceGuid"] ?? "";


    Response.Clear();
    Response.ClearContent();
    Response.ClearHeaders();
    Response.Buffer = true;
    Response.BufferOutput = true;
    Response.Cache.SetNoStore();
    Response.Cache.SetCacheability(HttpCacheability.NoCache);
    Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
    Response.ContentEncoding = new UTF8Encoding(false);
    Response.Charset = "utf-8";

    if (string.IsNullOrWhiteSpace(songUrl))
    {
        WriteXml(BuildMrmlErrorPage("Missing song_url"));
        return;
    }

    try
    {
        string mp4Url = GetVideoUrlFromPhp(songUrl, coverUrl, title, artist);

        string backUrl = "LukifyMusic.aspx";
        if (!string.IsNullOrEmpty(deviceGuid))
            backUrl += "?DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

        string channelTVHD = "page:file:///ChannelTVHD.xml";
        string diagnosticsPage = "page:file:///Diagnostics.xml";

        StringBuilder sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine();
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"PlayLukifyMusicSong\" appid=\"lukatube.music/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel background=\"rgb(0,0,0)\">");

        sb.AppendLine("<Text top=\"18\" left=\"40\" width=\"1200\" height=\"40\" fontstyle=\"Reg30\" foreground=\"argb(255,255,255,255)\">Now Playing</Text>");
        sb.AppendLine("<Text top=\"70\" left=\"40\" width=\"1200\" height=\"30\" fontstyle=\"Reg22\" foreground=\"argb(255,220,220,220)\">" +
                      HttpUtility.HtmlEncode(title) + "</Text>");
        sb.AppendLine("<Text top=\"105\" left=\"40\" width=\"1200\" height=\"26\" fontstyle=\"Reg18\" foreground=\"argb(255,180,180,180)\">" +
                      HttpUtility.HtmlEncode(artist) + "</Text>");

        sb.AppendLine("<Video id=\"player\" top=\"155\" left=\"40\" width=\"1200\" height=\"470\" " +
                      "tuneurl=\"" + HttpUtility.HtmlAttributeEncode(mp4Url) + "\" " +
                      "visible=\"true\" showcontrols=\"true\" autoplay=\"true\" />");

          sb.AppendLine("<Actions>");  

        sb.AppendLine("<Action name=\"OpenHardDisk\" type=\"submit\" url=\"" +
                      HttpUtility.HtmlAttributeEncode(channelTVHD) + "\" method=\"GET\"/>");
        sb.AppendLine("<Action name=\"OpenDiagnostics\" type=\"submit\" url=\"" +
                      HttpUtility.HtmlAttributeEncode(diagnosticsPage) + "\" method=\"GET\"/>");

              sb.AppendLine("</Actions>");  

        sb.AppendLine("<Button id=\"btnBack\" top=\"645\" left=\"40\" width=\"180\" height=\"45\" href=\"" +
                      HttpUtility.HtmlAttributeEncode(backUrl) + "\">" +
                      "<Text top=\"8\" left=\"18\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\">Back</Text>" +
                      "</Button>");
sb.AppendLine("<Button id=\"btnOpenHardDisk\" top=\"645\" left=\"240\" width=\"220\" height=\"45\">");
sb.AppendLine("  <Actions>");
sb.AppendLine("    <Event type=\"onclick\" action=\"OpenHardDisk\" />");
sb.AppendLine("  </Actions>");
sb.AppendLine("  <Text top=\"8\" left=\"18\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\">Open Video Play Controls</Text>");
sb.AppendLine("</Button>");

sb.AppendLine("<Button id=\"btnDiagnostics\" top=\"645\" left=\"480\" width=\"220\" height=\"45\">");
sb.AppendLine("  <Actions>");
sb.AppendLine("    <Event type=\"onclick\" action=\"OpenDiagnostics\" />");
sb.AppendLine("  </Actions>");
sb.AppendLine("  <Text top=\"8\" left=\"18\" fontstyle=\"Reg18\" foreground=\"argb(255,255,255,255)\">Diagnostics</Text>");
sb.AppendLine("</Button>");

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        WriteXml(sb.ToString());
    }
    catch (Exception ex)
    {
        WriteXml(BuildMrmlErrorPage("Conversion failed: " + ex.Message));
    }
}

private string GetVideoUrlFromPhp(string songUrl, string coverUrl, string title, string artist)
{
    string phpUrl =
        "http://172.16.40.100/createvideoofsong.php" +
        "?song_url=" + HttpUtility.UrlEncode(songUrl) +
        "&cover=" + HttpUtility.UrlEncode(coverUrl) +
        "&title=" + HttpUtility.UrlEncode(title) +
        "&artist=" + HttpUtility.UrlEncode(artist);

    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(phpUrl);
    req.Method = "GET";
    req.Timeout = 120000;
    req.ReadWriteTimeout = 120000;
    req.UserAgent = "LukifyMusic/1.0";

    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
    {
        string json = sr.ReadToEnd();
        JObject obj = JObject.Parse(json);

        string url = obj["url"] != null ? obj["url"].ToString() : "";
        if (string.IsNullOrWhiteSpace(url))
        {
            string status = obj["status"] != null ? obj["status"].ToString() : "unknown";
            string error = obj["error"] != null ? obj["error"].ToString() : "";
            string log = obj["log"] != null ? obj["log"].ToString() : "";
            throw new Exception("PHP returned no video url. Status=" + status + " " + error + " " + log);
        }

        return url;
    }
}

private void WriteXml(string xml)
{
    Response.Write(xml);
    try
    {
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }
    catch
    {
    }
}

private string BuildMrmlErrorPage(string message)
{
    StringBuilder sb = new StringBuilder();
    sb.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
    sb.AppendLine();
    sb.AppendLine("<uidescription version=\"3.0\">");
    sb.AppendLine("<MrmlPage id=\"ErrorPage\" appid=\"lukatube.music/1.0\" width=\"1280\" height=\"720\">");
    sb.AppendLine("<Panel background=\"rgb(0,0,0)\">");
    sb.AppendLine("<Text top=\"60\" left=\"40\" width=\"1200\" height=\"40\" fontstyle=\"Reg28\" foreground=\"argb(255,255,100,100)\">Error</Text>");
    sb.AppendLine("<Text top=\"120\" left=\"40\" width=\"1200\" height=\"300\" fontstyle=\"Reg20\" foreground=\"argb(255,255,255,255)\">" +
                  HttpUtility.HtmlEncode(message) + "</Text>");
    sb.AppendLine("</Panel>");
    sb.AppendLine("</MrmlPage>");
    sb.AppendLine("</uidescription>");
    return sb.ToString();
}
}
