using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class FollowAction : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetCacheability(HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        // Read query parameters
        string userId = Request.QueryString["userid"] ?? "";
        string targetUserId = Request.QueryString["target_userid"] ?? "";
        string action = Request.QueryString["action"] ?? "";

        // Optional return thread
        string threadId = Request.QueryString["thread_id"] ?? "";

        // Validate
        if (string.IsNullOrEmpty(userId) ||
            string.IsNullOrEmpty(targetUserId) ||
            string.IsNullOrEmpty(action))
        {
            WriteMrml("Greshka",
                "Nedovolni parametri (userid, target_userid i action se potrebni).",
                userId, targetUserId, threadId);
            return;
        }

        // Build HTTPS API URL
        string apiUrl = "http://172.16.40.100/follow_action.php";

        var qs = HttpUtility.ParseQueryString(string.Empty);
        qs["action"] = action;                 // follow or unfollow
        qs["target_user_id"] = targetUserId;   // REQUIRED by your API
        qs["me_id"] = userId;                // if backend needs it
        qs["query_secret"] = "supersecure123"; // if backend needs it

        string fullUrl = apiUrl + "?" + qs.ToString();

        string apiResponse = null;
        bool ok = false;
        string serverMessage = null;

        try
        {
            var req = (HttpWebRequest)WebRequest.Create(fullUrl);
            req.Method = "GET";
            req.Timeout = 8000;

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                apiResponse = sr.ReadToEnd();
            }

            JObject j = null;
            try { j = JObject.Parse(apiResponse); } catch { j = null; }

            if (j != null)
            {
                if (j["success"] != null)
                {
                    ok = (j["success"].Type == JTokenType.Boolean && j["success"].Value<bool>())
                         || (j["success"].Type == JTokenType.Integer && j["success"].Value<int>() != 0);
                }
                else if (j["status"] != null)
                {
                    var s = j["status"].ToString().ToLowerInvariant();
                    ok = (s == "ok" || s == "success");
                }

                if (j["message"] != null)
                    serverMessage = j["message"].ToString();
            }
            else
            {
                if (!string.IsNullOrEmpty(apiResponse))
                {
                    serverMessage = apiResponse.Trim();
                    ok = true;
                }
            }
        }
        catch (WebException wex)
        {
            ok = false;
            serverMessage = "Greshka pri povik na API.";

            try
            {
                if (wex.Response != null)
                {
                    using (var r = new StreamReader(wex.Response.GetResponseStream()))
                        serverMessage += " " + r.ReadToEnd();
                }
            }
            catch { }
        }
        catch (Exception ex)
        {
            ok = false;
            serverMessage = "Neochekuvana greshka: " + ex.Message;
        }

        if (string.IsNullOrEmpty(serverMessage))
            serverMessage = ok ? "Akcijata e uspesno izvrshena." : "Akcijata ne uspea.";

        string title = ok ? "Uspeh" : "Greshka";

        WriteMrml(title, serverMessage, userId, targetUserId, threadId);
    }

    private void WriteMrml(string title, string message,
                           string userId, string targetUserId,
                           string threadId)
    {
        string viewProfileHref =
            "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?"
            + "user_id=" + HttpUtility.UrlEncode(targetUserId)
            + "&userid=" + HttpUtility.UrlEncode(userId);

        string threadHref = "";
        if (!string.IsNullOrEmpty(threadId))
        {
            threadHref =
                "page:http://172.16.40.101/SETTEMediaroomApp/ThreadMessages.aspx?"
                + "thread_id=" + HttpUtility.UrlEncode(threadId)
                + "&userid=" + HttpUtility.UrlEncode(userId);
        }

        string homeHref =
            "page:http://172.16.40.101/SETTEMediaroomApp/LukaTube.aspx?userid="
            + HttpUtility.UrlEncode(userId);

        var sb = new StringBuilder();

        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine("<uidescription version=\"3.0\">");
        sb.AppendLine("<MrmlPage id=\"FollowActionResult\" width=\"1280\" height=\"720\" background=\"image(AppImages/pozadina.jpg)\">");
        sb.AppendLine("<Panel width=\"1280\" height=\"720\">");

        sb.AppendLine(string.Format(
            "<Text left=\"80\" top=\"120\" fontstyle=\"Reg36\" foreground=\"argb(255,255,255,255)\">{0}</Text>",
            EscapeXml(title)));

        sb.AppendLine(string.Format(
            "<Text left=\"80\" top=\"180\" width=\"1120\" height=\"160\" fontstyle=\"Reg22\" foreground=\"argb(255,230,230,230)\">{0}</Text>",
            EscapeXml(message)));

        int leftBase = 80;
        int btnWidth = 300;
        int btnHeight = 80;
        int gap = 24;
        int top = 380;

        // Profile button
        sb.AppendLine(string.Format(
            "<Button left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" focusScale=\"1.05\" href=\"{4}\" background=\"argb(255,40,40,80)\">",
            leftBase, top, btnWidth, btnHeight, EscapeXml(viewProfileHref)));
        sb.AppendLine("<Text top=\"26\" left=\"20\" width=\"260\" height=\"28\" fontstyle=\"Reg20\">Profil</Text>");
        sb.AppendLine("</Button>");

        // Thread button
        if (!string.IsNullOrEmpty(threadId))
        {
            sb.AppendLine(string.Format(
                "<Button left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" focusScale=\"1.05\" href=\"{4}\" background=\"argb(255,40,80,40)\">",
                leftBase + btnWidth + gap, top, btnWidth, btnHeight, EscapeXml(threadHref)));
            sb.AppendLine("<Text top=\"26\" left=\"20\" width=\"260\" height=\"28\" fontstyle=\"Reg20\">Poraki</Text>");
            sb.AppendLine("</Button>");
        }

        int okLeft = string.IsNullOrEmpty(threadId)
            ? leftBase + btnWidth + gap
            : leftBase + 2 * (btnWidth + gap);

        sb.AppendLine(string.Format(
            "<Button left=\"{0}\" top=\"{1}\" width=\"{2}\" height=\"{3}\" focusScale=\"1.05\" href=\"{4}\" background=\"argb(255,80,40,40)\">",
            okLeft, top, btnWidth, btnHeight, EscapeXml(homeHref)));
        sb.AppendLine("<Text top=\"26\" left=\"20\" width=\"260\" height=\"28\" fontstyle=\"Reg20\">OK</Text>");
        sb.AppendLine("</Button>");

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
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
}