using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class SearchinstagramUsers : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string rawSearch = (Request.QueryString["q"] ?? "").Trim();

        List<UserInfo> users = GetInstagramUsers(rawSearch);

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;

        // 🔥 BASE URL (CHANGE IF NEEDED)
        string baseUrl = "http://172.16.40.101/SETTEMediaroomApp/";

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""SearchInstagramUsers"" width=""1280"" height=""720"">");

        sb.AppendLine("<Panel>");
        sb.AppendLine("<Text top=\"20\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\">Instagram Search</Text>");

        // FULL PAGE URL FOR SEARCH SUBMIT
        string searchPageFull = "page:" + baseUrl + "SearchinstagramUsers.aspx?q={q}";

        sb.AppendLine(
            "<EditText id=\"q\" top=\"80\" left=\"40\" width=\"800\" height=\"50\">" +
            HttpUtility.HtmlEncode(rawSearch) +
            "</EditText>"
        );

        sb.AppendLine(
            "<Button top=\"80\" left=\"860\" width=\"200\" height=\"50\">" +
            "<Actions><Event type=\"onclick\" url=\"" + HttpUtility.HtmlAttributeEncode(searchPageFull) + "\" /></Actions>" +
            "<Text>Search</Text>" +
            "</Button>"
        );

        int top = 160;

        foreach (var u in users)
        {
            string avatar = "";
            if (!string.IsNullOrEmpty(u.profile_pic))
            {
                avatar = "<Image top=\"0\" left=\"0\" width=\"70\" height=\"70\" url=\"" +
                         HttpUtility.HtmlAttributeEncode(u.profile_pic) + "\" />";
            }

            // 🔥 FULL PROFILE URL
            string profileUrl = "page:" + baseUrl + "ViewInstagramProfile.aspx?username=" + HttpUtility.UrlEncode(u.username);

            sb.AppendLine(
                "<Button top=\"" + top + "\" left=\"40\" width=\"1200\" height=\"80\" href=\"" + HttpUtility.HtmlAttributeEncode(profileUrl) + "\">" +
                    avatar +
                    "<Text top=\"5\" left=\"120\" fontstyle=\"Reg28\">" + HttpUtility.HtmlEncode(u.username) + "</Text>" +
                    "<Text top=\"40\" left=\"120\" fontstyle=\"Reg24\">" + HttpUtility.HtmlEncode(u.full_name) + "</Text>" +
                "</Button>"
            );

            top += 90;
        }

        if (users.Count == 0)
        {
            sb.AppendLine("<Text top=\"" + top + "\" left=\"40\" fontstyle=\"Reg28\">No results</Text>");
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private List<UserInfo> GetInstagramUsers(string query)
    {
        var list = new List<UserInfo>();
        if (string.IsNullOrEmpty(query)) return list;

        try
        {
            string url = "https://www.instagram.com/web/search/topsearch/?query=" + HttpUtility.UrlEncode(query);

            var req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";

            // MOBILE HEADERS
            req.UserAgent = "Instagram 275.0.0.27.98 Android";
            req.Headers.Add("X-IG-App-ID", "936619743392459");
            req.Headers.Add("X-ASBD-ID", "198387");
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            req.Accept = "*/*";
            req.Referer = "https://www.instagram.com/";

            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream()))
            {
                string json = sr.ReadToEnd();

                var j = JObject.Parse(json);
                var users = j["users"];

                if (users != null)
                {
                    foreach (var u in users)
                    {
                        var user = u["user"];
                        if (user == null) continue;

                        list.Add(new UserInfo
                        {
                            username = user["username"]?.ToString(),
                            full_name = user["full_name"]?.ToString(),
                            profile_pic = user["profile_pic_url"]?.ToString()
                        });
                    }
                }
            }
        }
        catch { }

        return list;
    }

    class UserInfo
    {
        public string username;
        public string full_name;
        public string profile_pic;
    }
}