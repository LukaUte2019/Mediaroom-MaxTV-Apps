using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;

public partial class Gley : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        string deviceGuid = (Request.QueryString["DeviceGuid"] ?? "").Trim();
        string folder = (Request.QueryString["folder"] ?? "").Trim();

        // paging
        int page = 1;
        int.TryParse(Request.QueryString["page"], out page);
        if (page < 1) page = 1;
        const int PAGE_SIZE = 20; // items per page (change if you want)

        string baseUrl = "http://172.16.40.100/gley/";

        List<string> items = new List<string>();
        bool showingFolders = string.IsNullOrEmpty(folder);

        try
        {
            string targetUrl = baseUrl;
            if (!showingFolders)
                targetUrl = baseUrl + folder + "/";

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(targetUrl);
            req.Timeout = 5000;

            string html;

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                html = sr.ReadToEnd();
            }

            MatchCollection matches = Regex.Matches(
                html,
                @"href\s*=\s*[""']([^""']+)[""']",
                RegexOptions.IgnoreCase);

            foreach (Match m in matches)
            {
                string link = m.Groups[1].Value;

                if (link == "../")
                    continue;

                if (showingFolders)
                {
                    if (link.EndsWith("/"))
                        items.Add(link.TrimEnd('/'));
                }
                else
                {
                    string ext = Path.GetExtension(link).ToLowerInvariant();

                    if (ext == ".mp4" ||
                        ext == ".m4v" ||
                        ext == ".mov" ||
                        ext == ".mkv")
                    {
                        items.Add(link);
                    }
                }
            }
        }
        catch
        {
            items = new List<string>();
        }

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"GleyList\" appid=\"gley.app/1.0\" width=\"1280\" height=\"720\">");
        sb.AppendLine("<Panel id=\"MainPanel\" top=\"0\" left=\"0\" width=\"1280\" height=\"720\">");

        string title = "Gley";

        if (!showingFolders)
            title = "Gley / " + folder.Replace("_", " ");

        sb.AppendLine("<Text top=\"10\" left=\"20\" width=\"1000\" height=\"30\" fontstyle=\"Reg26\" foreground=\"argb(255,228,0,115)\">"
            + EscapeXml(title) + "</Text>");

        sb.AppendLine("<Text id=\"Time\" top=\"10\" left=\"0\" width=\"1280\" height=\"30\" fontstyle=\"Reg20\" justification=\"right\" foreground=\"argb(255,200,200,200)\">{Time}</Text>");

        sb.AppendLine(BuildGrid(items, showingFolders, folder, baseUrl, deviceGuid, page, PAGE_SIZE));

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private string BuildGrid(List<string> items, bool showingFolders, string folder, string baseUrl, string deviceGuid, int page, int pageSize)
    {
        const int ITEMS_PER_ROW = 5;
        const int CARD_WIDTH = 200;
        const int CARD_HEIGHT = 120;
        const int CARD_SPACING = 20;

        StringBuilder sb = new StringBuilder();

        sb.AppendLine("<VerticalFlowPanel id=\"VideoGrid\" top=\"80\" left=\"40\" width=\"1200\" height=\"560\" clipsChildren=\"true\" itemSpacing=\"" + CARD_SPACING + "\">");

        // paging math
        int totalItems = items.Count;
        int start = (page - 1) * pageSize;
        int end = Math.Min(start + pageSize, totalItems);

        // If start is out of range, show first page instead
        if (start >= totalItems)
        {
            start = 0;
            end = Math.Min(pageSize, totalItems);
            page = 1;
        }

        // iterate rows, from start to end (exclusive)
        for (int i = start; i < end; i += ITEMS_PER_ROW)
        {
            sb.AppendLine("<HorizontalFlowPanel height=\"" + CARD_HEIGHT + "\" itemSpacing=\"" + CARD_SPACING + "\">");

            int sliceEnd = Math.Min(i + ITEMS_PER_ROW, end);

            for (int j = i; j < sliceEnd; j++)
            {
                string item = items[j];

                if (showingFolders)
                {
                    string folderUrl = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority)
                        + "/SETTEMediaroomApp/Gley.aspx?folder="
                        + HttpUtility.UrlEncode(item);

                    if (!string.IsNullOrEmpty(deviceGuid))
                        folderUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                    string displayFolder = item.Replace("_", " ");

                    sb.AppendLine("<Button width=\"" + CARD_WIDTH + "\" height=\"" + CARD_HEIGHT + "\" focusScale=\"1.08\" backgroundFocus=\"argb(255,40,40,40)\" href=\"page:" + EscapeXml(folderUrl) + "\">");

                    sb.AppendLine("<Text top=\"10\" width=\"" + CARD_WIDTH + "\" height=\"100\" fontstyle=\"Reg18\" alignment=\"center\">"
                        + EscapeXml(displayFolder) + "</Text>");

                    sb.AppendLine("</Button>");
                }
                else
                {
                    string videoName = Path.GetFileName(item);

                    string cleanName = Path.GetFileNameWithoutExtension(videoName);
                    cleanName = cleanName.Replace("_", " ");

                    string playUrl = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority)
                        + "/SETTEMediaroomApp/PlayVideo.aspx"
                        + "?video_url="
                        + HttpUtility.UrlEncode(baseUrl + folder + "/" + item)
                        + "&video_name="
                        + HttpUtility.UrlEncode(cleanName);

                    if (!string.IsNullOrEmpty(deviceGuid))
                        playUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

                    playUrl += "&LocalFolder=false";

                    string displayName = Path.GetFileNameWithoutExtension(videoName);
                    displayName = displayName.Replace("_", " ");

                    sb.AppendLine("<Button width=\"" + CARD_WIDTH + "\" height=\"" + CARD_HEIGHT + "\" focusScale=\"1.08\" backgroundFocus=\"argb(255,40,40,40)\" href=\"page:" + EscapeXml(playUrl) + "\">");

                    sb.AppendLine("<Text top=\"10\" width=\"" + CARD_WIDTH + "\" height=\"100\" fontstyle=\"Reg18\" lines=\"3\" alignment=\"center\" ellipsize=\"end\">"
                        + EscapeXml(displayName) + "</Text>");

                    sb.AppendLine("</Button>");
                }
            }

            sb.AppendLine("</HorizontalFlowPanel>");
        }

        sb.AppendLine("</VerticalFlowPanel>");

        // Load More button (only if there are more items)
        if (end < totalItems)
        {
            string nextUrl = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority)
                + "/SETTEMediaroomApp/Gley.aspx?";

            if (!string.IsNullOrEmpty(folder))
                nextUrl += "folder=" + HttpUtility.UrlEncode(folder) + "&";

            nextUrl += "page=" + (page + 1);

            if (!string.IsNullOrEmpty(deviceGuid))
                nextUrl += "&DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid);

            // place the button centered-ish near bottom
            sb.AppendLine("<Button top=\"660\" left=\"540\" width=\"200\" height=\"40\" focusScale=\"1.1\" backgroundFocus=\"argb(255,60,60,60)\" href=\"page:" + EscapeXml(nextUrl) + "\">");
            sb.AppendLine("<Text top=\"8\" left=\"0\" width=\"200\" height=\"24\" fontstyle=\"Reg18\" alignment=\"center\">Load More</Text>");
            sb.AppendLine("</Button>");
        }
        else
        {
            // Optionally, show nothing or a "No more items" text — left out to keep UI clean
        }

        return sb.ToString();
    }

    private string EscapeXml(string s)
    {
        if (s == null) return "";

        return s.Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
    }
}