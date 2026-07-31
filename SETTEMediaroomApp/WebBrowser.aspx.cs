using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.UI;
using System.Security;
using System.Collections.Generic;

public partial class WebBrowser : Page
{
    private const string ProxyFetch = "http://172.16.40.100/web_proxy.php?url=";
    private const string ImageProxy = "http://172.16.40.100/ig_pfp_loader.php?image_url=";
    private const string BrowserPage = "http://172.16.40.101/SETTEMediaroomApp/WebBrowser.aspx?url=";

    protected void Page_Load(object sender, EventArgs e)
    {
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.Cache.SetNoStore();

        string url = Request.QueryString["url"];
        if (string.IsNullOrWhiteSpace(url))
            url = "https://example.com";

        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "http://" + url;
        }

        string html = FetchHtml(ProxyFetch + HttpUtility.UrlEncode(url));
        string title = ExtractTitle(html);
        List<Block> blocks = ParseHtmlToBlocks(html, url);

        StringBuilder mrml = new StringBuilder();
        mrml.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        mrml.Append("<uidescription version=\"3.0\">");
        mrml.Append("<MrmlPage id=\"Browser\" width=\"1280\" height=\"720\">");
        mrml.Append("<Panel width=\"1280\" height=\"720\">");

        mrml.AppendFormat(
            "<Text left=\"20\" top=\"10\" width=\"1000\" height=\"30\" fontstyle=\"Reg22\">{0}</Text>",
            EscapeXml(title)
        );

        mrml.AppendFormat(
            "<EditText id=\"BrowserUrl\" top=\"44\" left=\"20\" width=\"900\" height=\"30\">{0}</EditText>",
            EscapeXml(url)
        );

        // Go button using page: with local WebBrowser.aspx
        mrml.Append(@"
<Button top=""44"" left=""940"" width=""100"" height=""30"">
    <Text>Go</Text>
    <Actions>
        <Event type=""onclick"" action=""GoBrowse""/>
    </Actions>
</Button>

<Actions>
    <Action 
        name=""GoBrowse"" 
        type=""submit"" 
        data=""BrowserUrl"" 
        url=""page:" + BrowserPage + @"{BrowserUrl}"" 
        method=""GET""/>
</Actions>
");

        int y = 90;
        foreach (var b in blocks)
        {
            if (y > 680) break;

            if (b.Type == "text")
            {
                mrml.AppendFormat(
                    "<Text left=\"20\" top=\"{0}\" width=\"1200\" height=\"24\">{1}</Text>",
                    y,
                    EscapeXml(b.Text)
                );
                y += 26;
            }
            else if (b.Type == "link")
            {
                // Clickable links go to local WebBrowser.aspx using page:
                string pageUrl = "page:" + BrowserPage + HttpUtility.UrlEncode(b.Url);

                mrml.AppendFormat(
                    "<Button left=\"20\" top=\"{0}\" width=\"1200\" height=\"32\" href=\"{1}\">{2}</Button>",
                    y,
                    EscapeXml(pageUrl),
                    EscapeXml(b.Text)
                );
                y += 34;
            }
            else if (b.Type == "image")
            {
                string imgUrl = ImageProxy + HttpUtility.UrlEncode(b.Url);
                mrml.AppendFormat(
                    "<Image left=\"20\" top=\"{0}\" width=\"400\" height=\"200\" src=\"{1}\"/>",
                    y,
                    EscapeXml(imgUrl)
                );
                y += 210;
            }
        }

        mrml.Append("</Panel></MrmlPage></uidescription>");
        Response.Write(mrml.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    private List<Block> ParseHtmlToBlocks(string html, string baseUrl)
    {
        List<Block> list = new List<Block>();
        if (string.IsNullOrEmpty(html)) return list;

        // LINKS
        foreach (Match m in Regex.Matches(html, @"<a[^>]*href\s*=\s*['""]([^'""]+)['""][^>]*>(.*?)</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline))
        {
            string href = ResolveUrl(baseUrl, m.Groups[1].Value.Trim());
            // Remove any internal proxy prefix
            href = Regex.Replace(href, @"^https?:\/\/172\.16\.40\.100\/web_proxy\.php\?url=", "", RegexOptions.IgnoreCase);
            href = HttpUtility.UrlDecode(href);

            string text = StripTags(m.Groups[2].Value).Trim();
            if (!string.IsNullOrWhiteSpace(href))
            {
                list.Add(new Block
                {
                    Type = "link",
                    Url = href,
                    Text = string.IsNullOrWhiteSpace(text) ? href : text
                });
            }
        }

        // IMAGES
        foreach (Match m in Regex.Matches(html, @"<img[^>]*src\s*=\s*['""]([^'""]+)['""]",
            RegexOptions.IgnoreCase))
        {
            string src = ResolveUrl(baseUrl, m.Groups[1].Value.Trim());
            if (!string.IsNullOrWhiteSpace(src))
            {
                list.Add(new Block
                {
                    Type = "image",
                    Url = src
                });
            }
        }

        // TEXT
        string clean = Regex.Replace(html, "<script[^>]*>.*?</script>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        clean = Regex.Replace(clean, "<style[^>]*>.*?</style>", " ", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        clean = Regex.Replace(clean, "<.*?>", " ");
        clean = HttpUtility.HtmlDecode(clean);

        foreach (string line in SplitLines(clean, 120))
        {
            string trimmed = line.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                list.Add(new Block
                {
                    Type = "text",
                    Text = trimmed
                });
            }
        }

        return list;
    }

    private string FetchHtml(string url)
    {
        try
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "Mozilla/5.0";
            req.Timeout = 15000;

            using (HttpWebResponse res = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(res.GetResponseStream(), Encoding.UTF8))
            {
                return sr.ReadToEnd();
            }
        }
        catch (Exception ex)
        {
            return "<html><body>Error: " + EscapeXml(ex.Message) + "</body></html>";
        }
    }

    private string ExtractTitle(string html)
    {
        Match m = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? HttpUtility.HtmlDecode(m.Groups[1].Value) : "Web Browser";
    }

    private string ResolveUrl(string baseUrl, string href)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(href)) return "";
            if (href.StartsWith("#") || href.StartsWith("javascript:")) return "";
            return new Uri(new Uri(baseUrl), href).ToString();
        }
        catch { return href; }
    }

    private string StripTags(string s)
    {
        return Regex.Replace(s ?? "", "<.*?>", "");
    }

    private string[] SplitLines(string text, int max)
    {
        List<string> lines = new List<string>();
        if (string.IsNullOrEmpty(text)) return lines.ToArray();

        StringBuilder sb = new StringBuilder();
        foreach (char c in text)
        {
            sb.Append(c);
            if (sb.Length >= max && c == ' ')
            {
                lines.Add(sb.ToString());
                sb.Clear();
            }
        }
        if (sb.Length > 0) lines.Add(sb.ToString());
        return lines.ToArray();
    }

    private string EscapeXml(string s)
    {
        return SecurityElement.Escape(s ?? "");
    }

    private class Block
    {
        public string Type;
        public string Text;
        public string Url;
    }
}