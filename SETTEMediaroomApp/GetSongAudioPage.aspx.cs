using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class GetSongAudioPage : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        // ---------- GET parameters ----------
        string songUrl = Request.QueryString["song_url"];
        string filename = Request.QueryString["filename"];
        string title = Request.QueryString["title"];
        string artist = Request.QueryString["artist"];
        int page = Math.Max(1, int.TryParse(Request.QueryString["page"], out int p) ? p : 1);
        int limit = Math.Max(1, int.TryParse(Request.QueryString["limit"], out int l) ? l : 5);

        // ---------- Build API URL ----------
        string apiUrl = "http://172.16.40.100/get_song_audiopage.php?";
        var qs = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrEmpty(songUrl)) qs["song_url"] = songUrl;
        if (!string.IsNullOrEmpty(filename)) qs["filename"] = filename;
        if (!string.IsNullOrEmpty(title)) qs["title"] = title;
        if (!string.IsNullOrEmpty(artist)) qs["artist"] = artist;
        qs["page"] = page.ToString();
        qs["limit"] = limit.ToString();
        apiUrl += qs.ToString();

        // ---------- Fetch JSON ----------
        string jsonResult = "{}";
        try
        {
            var req = (HttpWebRequest)WebRequest.Create(apiUrl);
            req.Method = "GET";
            req.Timeout = 5000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                jsonResult = sr.ReadToEnd();
            }
        }
        catch
        {
            jsonResult = "{\"error\":\"Cannot reach API\"}";
        }

        JObject data = null;
        try { data = JObject.Parse(jsonResult); } catch { data = new JObject(); }
        JArray posts = data["posts"] as JArray ?? new JArray();

        // ---------- Build MRML ----------
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("  <MrmlPage id=\"SongAudioPage\" width=\"1280\" height=\"720\">");
        sb.AppendLine("    <Panel id=\"MainPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"720\">");

        // Header
        sb.AppendLine("      <Panel id=\"HeaderPanel\" left=\"0\" top=\"0\" width=\"1280\" height=\"100\" background=\"argb(255,25,25,25)\">");
        sb.AppendLine("        <Text top=\"15\" left=\"40\" width=\"800\" height=\"40\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">Song Audio Page</Text>");
        string songDisplay = !string.IsNullOrEmpty(title) ? EscapeXml(title) : (!string.IsNullOrEmpty(filename) ? EscapeXml(filename) : EscapeXml(songUrl ?? "Unknown Song"));
        sb.AppendLine($"        <Text top=\"55\" left=\"40\" width=\"1000\" height=\"36\" fontstyle=\"Reg22\" foreground=\"argb(255,180,180,180)\">{songDisplay}</Text>");
        sb.AppendLine("      </Panel>");

        int topPos = 120;
        int cardHeight = 160;
        int spacingY = 16;
        int leftCol = 40;
        int cardWidth = 1200;

        foreach (var post in posts)
        {
            string username = post["user"]?["username"]?.ToString() ?? "unknown";
            string fullName = post["user"]?["full_name"]?.ToString() ?? username;
            string caption = post["caption"]?.ToString() ?? "";
            string audioUrlPost = post["song"]?["song_url"]?.ToString() ?? post["video_url"]?.ToString() ?? "";

            sb.AppendLine($"      <Panel id=\"post_{topPos}\" left=\"{leftCol}\" top=\"{topPos}\" width=\"{cardWidth}\" height=\"{cardHeight}\" background=\"argb(255,40,40,40)\">");
            sb.AppendLine($"        <Text top=\"10\" left=\"12\" width=\"800\" height=\"28\" fontstyle=\"Reg22\" foreground=\"argb(255,255,255,255)\">{EscapeXml(fullName)} (@{EscapeXml(username)})</Text>");
            sb.AppendLine($"        <Text top=\"40\" left=\"12\" width=\"1160\" height=\"28\" fontstyle=\"Reg18\" foreground=\"argb(255,200,200,200)\">{EscapeXml(caption)}</Text>");

            if (!string.IsNullOrEmpty(audioUrlPost))
            {
                string href = "page:" + HttpUtility.UrlEncode(audioUrlPost);
                sb.AppendLine($"        <Button id=\"btn_play_{topPos}\" top=\"80\" left=\"12\" width=\"400\" height=\"64\" href=\"{EscapeXml(href)}\" background=\"argb(255,60,30,30)\">");
                sb.AppendLine("          <Text top=\"18\" left=\"10\" width=\"120\" height=\"28\" fontstyle=\"Reg18\">Play Audio</Text>");
                sb.AppendLine("        </Button>");
            }

            sb.AppendLine("      </Panel>");
            topPos += cardHeight + spacingY;
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
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}