using System;
using System.Net;
using System.Web;
using System.Web.UI;
using System.Security;
using System.Text.RegularExpressions;

public partial class SendLinkToPhone : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string user_id = Request.QueryString["user_id"] ?? "";
        string deviceguid = Request.QueryString["deviceguid"] ?? "";
        string url = Request.QueryString["url"] ?? "";
        bool dontRedirect = IsTrue(Request.QueryString["dontredirect"]);

        // ===== Send link to phone first =====
        string status = "failed";
        if (!string.IsNullOrEmpty(user_id) && !string.IsNullOrEmpty(url))
        {
            try
            {
                string api =
                    "http://172.16.40.100/send_link_to_phone.php?user_id=" +
                    HttpUtility.UrlEncode(user_id) +
                    "&url=" +
                    HttpUtility.UrlEncode(url) +
                    "&dontredirect=" +
                    (dontRedirect ? "true" : "false");

                using (WebClient wc = new WebClient())
                {
                    wc.DownloadString(api);
                    status = "sent";
                }
            }
            catch
            {
                status = "error";
            }
        }

        // ===== Redirect logic for YouTube URLs =====
        if (!dontRedirect && !string.IsNullOrEmpty(url))
        {
            string youtubeVideoId = GetYouTubeVideoId(url);
            if (!string.IsNullOrEmpty(youtubeVideoId))
            {
                string redirectUrl = "http://172.16.40.101/SETTEMediaroomApp/PlayYoutubeVideo.aspx?videoId=" +
                                     HttpUtility.UrlEncode(youtubeVideoId);

                Response.Redirect(redirectUrl, true);
                return;
            }
        }

        // ===== Redirect logic for Kupikarta URLs =====
        if (!dontRedirect && !string.IsNullOrEmpty(url))
        {
            var match = Regex.Match(
                url,
                @"https?://kupikarta\.com/(event-details|tickets)\.nspx\?eventid=(\d+)",
                RegexOptions.IgnoreCase
            );

            if (match.Success)
            {
                string eventId = match.Groups[2].Value;

                // PASS USER DATA HERE
                var qs = HttpUtility.ParseQueryString(string.Empty);
                qs["id"] = eventId;

                if (!string.IsNullOrEmpty(user_id))
                {
                    qs["user_id"] = user_id;
                    qs["userid"] = user_id;
                    qs["me_id"] = user_id;
                }

                if (!string.IsNullOrEmpty(deviceguid))
                {
                    qs["deviceguid"] = deviceguid;
                }

                string redirectUrl =
                    "http://172.16.40.101/SETTEMediaroomApp/ViewEvent.aspx?" +
                    qs.ToString();

                Response.Redirect(redirectUrl, true);
                return;
            }
        }

        // ===== Prepare MRML =====
        string safeUrl = EscapeXml(url);
        string safeStatus = EscapeXml(status);

        string openBrowserHref =
            "page:http://172.16.40.101/SETTEMediaroomApp/WebBrowser.aspx?url=" +
            HttpUtility.UrlEncode(url);

        string backHref = "action:back";

        string mrml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<uidescription version=""3.0"">
<MrmlPage
    id=""SendLinkPage""
    width=""1280""
    height=""720""
    background=""image(AppImages/pozadina.jpg)"">

    <Panel
        id=""MainPanel""
        left=""0""
        top=""0""
        width=""1280""
        height=""720"">

        <Text
            id=""Title""
            left=""420""
            top=""220""
            fontstyle=""Reg32""
            foreground=""argb(255,226,0,116)"">
            Send Link To Phone
        </Text>

        <Text
            id=""StatusLabel""
            left=""420""
            top=""290""
            fontstyle=""Reg26""
            foreground=""argb(255,255,255,255)"">
            Status: " + safeStatus + @"
        </Text>

        <Text
            id=""UrlLabel""
            left=""420""
            top=""350""
            fontstyle=""Reg22""
            foreground=""argb(255,255,255,255)"">
            URL: " + safeUrl + @"
        </Text>

        <Button
            left=""420""
            top=""430""
            width=""240""
            height=""44""
            href=""" + EscapeXml(openBrowserHref) + @""">
            Open in Browser
        </Button>

        <Button
            left=""680""
            top=""430""
            width=""160""
            height=""44""
            href=""" + EscapeXml(backHref) + @""">
            Back
        </Button>

    </Panel>

    <Actions>
        <Event type=""onenter"" action=""back""/>
    </Actions>
</MrmlPage>
</uidescription>";

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Write(mrml);
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private static bool IsTrue(string value)
    {
        if (string.IsNullOrEmpty(value)) return false;

        value = value.Trim().ToLowerInvariant();
        return value == "1" || value == "true" || value == "yes" || value == "on";
    }

    private static string EscapeXml(string s)
    {
        if (string.IsNullOrEmpty(s))
            return "";

        return SecurityElement.Escape(s);
    }

    private static string GetYouTubeVideoId(string url)
    {
        if (string.IsNullOrEmpty(url))
            return "";

        string trimmed = url.Trim();

        Uri uri;
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out uri))
            return "";

        string host = uri.Host.ToLowerInvariant();

        bool isYouTube =
            host == "youtu.be" ||
            host.EndsWith(".youtu.be") ||
            host == "youtube.com" ||
            host.EndsWith(".youtube.com") ||
            host == "www.youtube.com" ||
            host == "m.youtube.com" ||
            host == "music.youtube.com";

        if (!isYouTube)
            return "";

        // youtu.be/VIDEOID
        if (host.Contains("youtu.be"))
        {
            string path = uri.AbsolutePath.Trim('/');
            if (!string.IsNullOrEmpty(path))
                return CleanVideoId(path);
        }

        // youtube.com/watch?v=VIDEOID
        // youtube.com/embed/VIDEOID
        // youtube.com/shorts/VIDEOID
        var query = HttpUtility.ParseQueryString(uri.Query);
        string v = query["v"];
        if (!string.IsNullOrEmpty(v))
            return CleanVideoId(v);

        string pathLower = uri.AbsolutePath.ToLowerInvariant();

        Match m = Regex.Match(uri.AbsolutePath, @"^/(embed|shorts|v)/([^/?#]+)", RegexOptions.IgnoreCase);
        if (m.Success && m.Groups.Count > 2)
            return CleanVideoId(m.Groups[2].Value);

        // Some share URLs may have /watch/VIDEOID style
        m = Regex.Match(uri.AbsolutePath, @"^/watch/([^/?#]+)", RegexOptions.IgnoreCase);
        if (m.Success && m.Groups.Count > 1)
            return CleanVideoId(m.Groups[1].Value);

        return "";
    }

    private static string CleanVideoId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "";

        id = id.Trim();

        // Keep only valid-looking YouTube ID characters
        id = Regex.Replace(id, @"[^A-Za-z0-9_-]", "");

        return id;
    }
}