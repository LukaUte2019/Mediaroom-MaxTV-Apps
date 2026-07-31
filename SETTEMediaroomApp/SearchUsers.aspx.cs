using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.UI;
using Newtonsoft.Json.Linq;

public partial class SearchUsers : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string meId = Request.QueryString["me_id"];
        string deviceGuid = Request.QueryString["DeviceGuid"];
        string rawSearch = (GetSearchFromRequest() ?? "").Trim();
        string pageStr = Request.QueryString["page"];
        string debugStr = Request.QueryString["debug"];
        bool debug = !string.IsNullOrEmpty(debugStr) && debugStr == "1";

        // Only one SearchSendTo, take last non-empty
        string[] searchSendToValues = Request.QueryString.GetValues("SearchSendTo") ?? new string[0];
        string finalSearchSendTo = rawSearch;
        if (searchSendToValues.Length > 0)
        {
            for (int i = searchSendToValues.Length - 1; i >= 0; i--)
            {
                if (!string.IsNullOrEmpty(searchSendToValues[i]))
                {
                    finalSearchSendTo = searchSendToValues[i];
                    break;
                }
            }
        }

        string apiDebug;
        List<UserInfo> users = GetUsers(finalSearchSendTo, meId, debug, out apiDebug);

        int page = 1;
        int pageSize = 6;
        int pageParsed;
        if (int.TryParse(pageStr, out pageParsed) && pageParsed > 0)
            page = pageParsed;

        int totalUsers = users.Count;
        int totalPages = (int)Math.Ceiling(totalUsers / (double)pageSize);
        var pagedUsers = users.GetRange((page - 1) * pageSize, Math.Min(pageSize, totalUsers - (page - 1) * pageSize));

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""SearchUsersList"" appid=""lukatube.dm/1.0"" width=""1280"" height=""720"">");

        // Submit Action URL (for the search form)
        string submitBaseHttp = "http://172.16.40.101/SETTEMediaroomApp/SearchUsers.aspx";
        var submitQs = HttpUtility.ParseQueryString(string.Empty);
        if (!string.IsNullOrEmpty(meId)) submitQs["me_id"] = meId;
        if (!string.IsNullOrEmpty(deviceGuid)) submitQs["DeviceGuid"] = deviceGuid;

        string submitQueryPart = submitQs.ToString();
        string submitUrlFull = submitBaseHttp + (submitQueryPart.Length > 0 ? "?" + submitQueryPart : "");
        string submitPageUrl = "page:" + submitUrlFull;

        sb.AppendLine("<Actions>");
        sb.AppendLine(string.Format(
            "<Action name=\"SearchThreads\" type=\"submit\" data=\"SearchSendTo\" method=\"GET\" url=\"{0}\" />",
            HttpUtility.HtmlAttributeEncode(submitPageUrl)
        ));
        sb.AppendLine("</Actions>");

        sb.AppendLine("<Panel>");
        sb.AppendLine("<Text top=\"20\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">" + HttpUtility.HtmlEncode("Search Users") + "</Text>");
        sb.AppendLine("<Text top=\"70\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,200)\">" + HttpUtility.HtmlEncode("Search for users to view their profile and their Lukify posts") + "</Text>");
        sb.AppendLine(string.Format(
            "<EditText id=\"SearchSendTo\" name=\"SearchSendTo\" top=\"110\" left=\"40\" width=\"900\" height=\"48\" fontstyle=\"Reg24\">{0}</EditText>",
            HttpUtility.HtmlEncode(finalSearchSendTo)
        ));

        sb.AppendLine(
            "<Button id=\"btnSearch\" top=\"110\" left=\"960\" width=\"280\" height=\"48\">" +
              "<Actions><Event type=\"onclick\" action=\"SearchThreads\" /></Actions>" +
              "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">" + HttpUtility.HtmlEncode("Search") + "</Text>" +
            "</Button>"
        );

        // NEW BUTTON: Go to SearchArtists.aspx
       string artistsHref = "page:http://172.16.40.101/SETTEMediaroomApp/SearchArtists.aspx";

var artistsQs = HttpUtility.ParseQueryString(string.Empty);

if (!string.IsNullOrEmpty(meId))
    artistsQs["me_id"] = meId;

if (!string.IsNullOrEmpty(deviceGuid))
    artistsQs["DeviceGuid"] = deviceGuid;

string artistsQuery = artistsQs.ToString();
if (!string.IsNullOrEmpty(artistsQuery))
    artistsHref += "?" + artistsQuery;
        sb.AppendLine(
            "<Button id=\"btnArtists\" top=\"170\" left=\"960\" width=\"280\" height=\"48\" href=\"" + HttpUtility.HtmlAttributeEncode(artistsHref) + "\">" +
              "<Text alignment=\"center\" justification=\"center\" fontstyle=\"Reg24\" foreground=\"argb(255,255,255,255)\">" + HttpUtility.HtmlEncode("Search Artists") + "</Text>" +
            "</Button>"
        );

        int topPos = 230;
        if (debug)
        {
            sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"28\" fontstyle=\"Reg20\" foreground=\"argb(255,255,200,100)\">DEBUG: RawUrl: " + HttpUtility.HtmlEncode(Request.RawUrl) + "</Text>");
            topPos += 36;
            sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"20\" fontstyle=\"Reg20\" foreground=\"argb(255,200,255,200)\">HttpMethod: " + HttpUtility.HtmlEncode(Request.HttpMethod) + "</Text>");
            topPos += 28;
        }

        if (pagedUsers.Count == 0)
        {
            sb.AppendLine("<Text top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"36\" fontstyle=\"Reg28\" foreground=\"argb(255,255,60,60)\">" + HttpUtility.HtmlEncode("No users found") + "</Text>");
        }
        else
        {
            foreach (var u in pagedUsers)
            {
                string uname = u.username ?? "";
                string memberId = u.user_id ?? "";
                string threadId = ""; // optional

                string primaryText = HttpUtility.HtmlEncode(!string.IsNullOrEmpty(uname) ? uname : memberId);
                string secondaryText = HttpUtility.HtmlEncode(u.full_name ?? "");

                string avatarTag = "";
                if (!string.IsNullOrEmpty(u.profile_picture_url))
                {
                    string avatarUrl = u.profile_picture_url;
                    if (avatarUrl.IndexOf("lukaserver.ddns.net", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        avatarUrl = avatarUrl.Replace("https://lukaserver.ddns.net", "http://172.16.40.100")
                                             .Replace("http://lukaserver.ddns.net", "http://172.16.40.100");
                    }
                    avatarUrl = HttpUtility.HtmlAttributeEncode(avatarUrl);
                    avatarTag = "<Image top=\"0\" left=\"0\" width=\"70\" height=\"70\" url=\"" + avatarUrl + "\" />";
                }

                string profileHref = "page:http://172.16.40.101/SETTEMediaroomApp/ViewProfile.aspx?"
                    + "username=" + HttpUtility.UrlEncode(uname)
                    + "&user_id=" + HttpUtility.UrlEncode(meId ?? "")
                    + "&selected_user_id=" + HttpUtility.UrlEncode(memberId)
                    + "&thread_id=" + HttpUtility.UrlEncode(threadId);
                profileHref = HttpUtility.HtmlAttributeEncode(profileHref);

                string safeId = "btn_" + SanitizeId(memberId ?? uname ?? "user");
                string textLeft = string.IsNullOrEmpty(avatarTag) ? "40" : "120";

                sb.AppendLine(
                    "<Button id=\"" + HttpUtility.HtmlAttributeEncode(safeId) + "\" top=\"" + topPos + "\" left=\"40\" width=\"1200\" height=\"70\" href=\"" + profileHref + "\">" +
                        avatarTag +
                        "<Text top=\"5\" left=\"" + textLeft + "\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg28\" foreground=\"argb(255,255,255,255)\">" + primaryText + "</Text>" +
                        "<Text top=\"35\" left=\"" + textLeft + "\" alignment=\"left\" justification=\"left\" fontstyle=\"Reg24\" foreground=\"argb(255,200,200,200)\">" + secondaryText + "</Text>" +
                    "</Button>"
                );

                topPos += 80;
            }
        }

        sb.AppendLine("</Panel>");
        sb.AppendLine("</MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private List<UserInfo> GetUsers(string search, string meId, bool debug, out string apiDebug)
    {
        apiDebug = null;
        var result = new List<UserInfo>();
        string searchSafe = (search ?? "").Trim();

        string baseUrl = "http://172.16.40.100/search_users.php";
        string[] paramNames = new[] { "SearchSendTo", "search", "query", "q", "SearchUsers", "term", "s" };

        foreach (var pname in paramNames)
        {
            try
            {
                string url = baseUrl + "?" + pname + "=" + HttpUtility.UrlEncode(searchSafe);
                if (!string.IsNullOrEmpty(meId))
                    url += "&me_id=" + HttpUtility.UrlEncode(meId);

                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Timeout = 10000;
                req.UserAgent = "LukaTube/1.0";

                string respBody = null;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    respBody = sr.ReadToEnd();
                }

                if (debug) apiDebug = url + "\r\n" + (respBody ?? "");
                if (string.IsNullOrEmpty(respBody)) continue;

                try
                {
                    var j = JObject.Parse(respBody);
                    JArray usersArray = null;

                    JToken usersToken = null;
                    if (j["results"] != null) usersToken = j["results"]["users"];
                    if (usersToken == null && j["users"] != null) usersToken = j["users"];
                    if (usersToken != null && usersToken.Type == JTokenType.Array)
                        usersArray = (JArray)usersToken;

                    if (usersArray != null)
                    {
                        foreach (var it in usersArray)
                        {
                            try
                            {
                                var u = new UserInfo();
                                u.user_id = (it["user_id"] ?? it["id"] ?? it["userId"] ?? "").ToString();
                                u.username = (it["username"] ?? it["user_name"] ?? "").ToString();
                                u.full_name = (it["full_name"] ?? it["name"] ?? "").ToString();

                                string[] avatarKeys = new[] { "profile_picture_url", "profile_picture", "avatar", "image", "picture", "photo", "image_url", "thumbnail", "thumb" };
                                u.profile_picture_url = "";
                                foreach (var k in avatarKeys)
                                {
                                    if (it[k] != null && !string.IsNullOrEmpty(it[k].ToString()))
                                    {
                                        u.profile_picture_url = it[k].ToString();
                                        break;
                                    }
                                }

                                result.Add(u);
                            }
                            catch { }
                        }
                        if (result.Count > 0) return result;
                    }
                }
                catch { }
            }
            catch { }
        }

        return result;
    }

    private string GetSearchFromRequest()
    {
        string[] keys = new[] { "SearchSendTo", "SearchSendto", "search", "Search", "query", "Query", "q", "Q", "txtQuery" };
        foreach (var k in keys)
        {
            string v = Request.QueryString[k];
            if (!string.IsNullOrEmpty(v))
                return HttpUtility.UrlDecode(v);

            if (Request.Form != null)
            {
                string fv = Request.Form[k];
                if (!string.IsNullOrEmpty(fv))
                    return HttpUtility.HtmlDecode(fv);
            }
        }
        return "";
    }

    private string SanitizeId(string input)
    {
        if (string.IsNullOrEmpty(input)) return "unknown";
        var sb = new StringBuilder();
        foreach (char c in input)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                sb.Append(c);
            else
                sb.Append('_');
        }
        string s = sb.ToString();
        return s.Length <= 60 ? s : s.Substring(0, 60);
    }

    private class UserInfo
    {
        public string user_id { get; set; }
        public string username { get; set; }
        public string full_name { get; set; }
        public string profile_picture_url { get; set; }
    }
}