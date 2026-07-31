using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;

public partial class DMThreads : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string userId = Request.QueryString["userid"];
        if (string.IsNullOrEmpty(userId))
            userId = "0";

        string deviceGuid = Request.QueryString["DeviceGuid"];

        List<Thread> threads = GetThreads(userId);

        int page = 1;
        int pageSize = 5;
        int totalThreads = threads.Count;

        int tmp;
        if (!string.IsNullOrEmpty(Request.QueryString["page"]) &&
            int.TryParse(Request.QueryString["page"], out tmp) &&
            tmp > 0)
            page = tmp;

        int startIndex = (page - 1) * pageSize;
        int endIndex = Math.Min(startIndex + pageSize, totalThreads);

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();

        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""DMThreads"" appid=""lukatube.dm/1.0"" width=""1280"" height=""720"">");
        sb.AppendLine(@"<Panel>");

        if (threads.Count == 0)
        {
            sb.AppendLine(@"<Text top=""200"" left=""200"" width=""900"" height=""80"" fontstyle=""Reg48"" foreground=""argb(255,255,60,60)"">No threads found</Text>");
        }
        else
        {
            sb.AppendLine(@"<Text top=""10"" left=""40"" width=""1200"" height=""50"" fontstyle=""Reg48"" foreground=""argb(255,255,255,255)"">Chats</Text>");

            
// -------- THREAD BUTTONS --------
for (int i = startIndex; i < endIndex; i++)
{
    Thread t = threads[i];
    int topPos = 50 + ((i - startIndex) * 100);

    string title = HttpUtility.HtmlEncode(t.title ?? "");

    string lastMsg = "";
    if (t.last_message != null)
    {
        // Ако порака е видео, прикажи [video] + реалното име
        if (!string.IsNullOrEmpty(t.last_message.video_name))
        {
            lastMsg = "[video] " + HttpUtility.HtmlEncode(t.last_message.video_name);
        }
        else if (!string.IsNullOrEmpty(t.last_message.text))
        {
            lastMsg = HttpUtility.HtmlEncode(t.last_message.text);
        }
    }

    // Build thread URL
    string baseUrl = "http://172.16.40.101/SETTEMediaroomApp/DMThreadMessages.aspx";
    string threadIdB64 = Base64UrlEncode(t.thread_id);

    List<string> q = new List<string>();
    q.Add("thread_id=" + threadIdB64);
    q.Add("userid=" + HttpUtility.UrlEncode(userId));
    if (!string.IsNullOrEmpty(deviceGuid))
        q.Add("DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid));
    if (!string.IsNullOrEmpty(t.title))
        q.Add("thread_name=" + HttpUtility.UrlEncode(t.title));

    string url = baseUrl + "?" + string.Join("&", q);
    url = url.Replace("&", "&amp;");

    // -------- THREAD IMAGE --------
    string imageTag = "";
    if (!string.IsNullOrEmpty(t.profile_picture_url))
    {
        string avatar = t.profile_picture_url
                        .Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                        .Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
        avatar = EscapeXml(avatar);
        imageTag = "<Image top=\"0\" left=\"0\" width=\"70\" height=\"70\" url=\"" + avatar + "\" />";
    }

    // -------- BUTTON WITH IMAGE, TITLE, AND LAST MESSAGE --------
    sb.AppendLine(string.Format(
        "<Button id=\"thread_{0}\" top=\"{1}\" left=\"40\" width=\"1200\" height=\"70\" focusScale=\"1.05\" href=\"page:{2}\">" +
        "{3}" +
        "<Text top=\"0\" left=\"{4}\" width=\"1120\" height=\"35\" fontstyle=\"Reg32\">{5}</Text>" +
        "<Text top=\"35\" left=\"{4}\" width=\"1120\" height=\"35\" fontstyle=\"Reg26\" foreground=\"argb(255,180,180,180)\">{6}</Text>" +
        "</Button>",
        i,
        topPos,
        url,
        imageTag,
        string.IsNullOrEmpty(imageTag) ? "0" : "80",
        title,
        lastMsg
    ));
}
            // -------- PAGING BUTTONS --------
            int buttonTop = 50 + (pageSize * 100) + 20;

            if (page > 1)
            {
                var prevParams = new List<string>();
                prevParams.Add("userid=" + HttpUtility.UrlEncode(userId));
                prevParams.Add("page=" + (page - 1));
                if (!string.IsNullOrEmpty(deviceGuid))
                    prevParams.Add("DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid));

                string prev = "http://172.16.40.101/SETTEMediaroomApp/DMThreads.aspx" + "?" + string.Join("&", prevParams);
                prev = prev.Replace("&", "&amp;");

                sb.AppendLine(string.Format(
                    @"<Button id=""PrevPage"" top=""{0}"" left=""40"" width=""200"" height=""50"" href=""page:{1}""><Text>Previous</Text></Button>",
                    buttonTop, prev));
            }

            if (endIndex < totalThreads)
            {
                var nextParams = new List<string>();
                nextParams.Add("userid=" + HttpUtility.UrlEncode(userId));
                nextParams.Add("page=" + (page + 1));
                if (!string.IsNullOrEmpty(deviceGuid))
                    nextParams.Add("DeviceGuid=" + HttpUtility.UrlEncode(deviceGuid));

                string next = "http://172.16.40.101/SETTEMediaroomApp/DMThreads.aspx" + "?" + string.Join("&", nextParams);
                next = next.Replace("&", "&amp;");

                sb.AppendLine(string.Format(
                    @"<Button id=""NextPage"" top=""{0}"" left=""300"" width=""200"" height=""50"" href=""page:{1}""><Text>Next</Text></Button>",
                    buttonTop, next));
            }
        }

        sb.AppendLine(@"</Panel>");
        sb.AppendLine(@"</MrmlPage>");
        sb.AppendLine(@"</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    // ================= BASE64 =================
    private static string Base64UrlEncode(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
        return b64.Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    // ================= API =================
    private List<Thread> GetThreads(string userId)
    {
        try
        {
            string url = "http://172.16.40.100/dm_api.php?action=list_threads&query_secret=supersecure123&userid=" +
                         HttpUtility.UrlEncode(userId);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";

            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
            {
                string json = sr.ReadToEnd();
                JavaScriptSerializer js = new JavaScriptSerializer();
                ThreadListResponse data = js.Deserialize<ThreadListResponse>(json);

                if (data != null && data.threads != null)
                    return data.threads;
            }
        }
        catch { }

        return new List<Thread>();
    }

    // ================= MODELS =================
    public class ThreadListResponse
    {
        public List<Thread> threads { get; set; }
    }

    public class Thread
    {
        public string thread_id { get; set; }
        public string title { get; set; }
        public string profile_picture_url { get; set; } // added for avatar
        public LastMessage last_message { get; set; }
    }

    public class LastMessage
    {
        public string text { get; set; }
        public long timestamp { get; set; }
        public int sender_id { get; set; }

        // NEW: optional video name if last message is a video
    public string video_name { get; set; }
    }

    private string EscapeXml(string s)
    {
        return (s ?? "")
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}