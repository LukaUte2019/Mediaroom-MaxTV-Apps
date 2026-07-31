using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.Script.Serialization;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;

public partial class ShowTVCode : Page
{
    // --- Мултијазични преводи ---
    private Dictionary<string, Dictionary<string, string>> Translations = new Dictionary<string, Dictionary<string, string>>
    {
        { "en", new Dictionary<string,string> {
            { "Title", "Connect to Lukify" },
            { "CodeInfo", "Your TV code: {0}\nPlease enter this code in the Lukify Music app to connect your TV." }
        }},
       { "mk", new Dictionary<string,string> {
    { "Title", "Povrzi se so Lukify" },
    { "CodeInfo", "Tvojot TV kod: {0}\nVnesi go ovoj kod vo Lukify Music aplikacijata za da go povrzis TV-to." }
}},

        { "al", new Dictionary<string,string> {
            { "Title", "Lidhu me Lukify" },
            { "CodeInfo", "Kodi ytë i TV-së: {0}\nJu lutem futni këtë kod në aplikacionin Lukify Music për të lidhur TV-në tuaj." }
        }},
        { "it", new Dictionary<string,string> {
            { "Title", "Connetti a Lukify" },
            { "CodeInfo", "Il tuo codice TV: {0}\nInserisci questo codice nell'app Lukify Music per collegare la TV." }
        }},
        { "rs", new Dictionary<string,string> {
            { "Title", "Poveži se sa Lukify" },
            { "CodeInfo", "Vaš TV kod: {0}\nUnesite ovaj kod u Lukify Music aplikaciju da povežete TV." }
        }}
    };

    protected void Page_Load(object sender, EventArgs e)
    {
        // --- Get client ID from query string ---
        string clientId = Request.QueryString["DeviceGuid"];
        if (string.IsNullOrEmpty(clientId))
        {
            clientId = "UNKNOWN_CLIENT";
        }

        // --- Generate a secure random TV code (6 chars) ---
        string tvCode = GenerateRandomCode(6);

        // --- Send to PHP server ---
        var postData = new Dictionary<string, string>
        {
            { "DeviceGuid", clientId },
            { "TVCode", tvCode }
        };
        string jsonData = new JavaScriptSerializer().Serialize(postData);

        try
        {
            var request = (HttpWebRequest)WebRequest.Create("http://172.16.40.100/storetvcodes.php");
            request.Method = "POST";
            request.ContentType = "application/json; charset=utf-8";

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(jsonData);
            }

            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                string responseText = reader.ReadToEnd();
            }
        }
        catch
        {
            // Optional: log error
        }

        // --- Detect language from Accept-Language header ---
        string lang = "en"; // default
        string acceptLang = Request.Headers["Accept-Language"];
        if (!string.IsNullOrEmpty(acceptLang))
        {
            string firstLang = acceptLang.Split(',')[0].Trim().ToLowerInvariant();
            if (firstLang.StartsWith("mk")) lang = "mk";
            else if (firstLang.StartsWith("sq") || firstLang.StartsWith("al")) lang = "al";
            else if (firstLang.StartsWith("it")) lang = "it";
            else if (firstLang.StartsWith("sr") || firstLang.StartsWith("rs")) lang = "rs";
        }

        // --- Build MRML response ---
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetCacheability(System.Web.HttpCacheability.NoCache);
        Response.Cache.SetNoStore();

        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"  <MrmlPage id=""TVPage"" appid=""tvconnect.app/1.0"" width=""1280"" height=""720"">");
        sb.AppendLine(@"    <Panel>");

        // Big red title at top-left
        sb.AppendLine("      <Text id=\"TitleText\" top=\"20\" left=\"20\" width=\"700\" height=\"120\" foreground=\"argb(255,255,0,0)\" alignment=\"left\" justification=\"left\">");
        sb.AppendLine("        " + HttpUtility.HtmlEncode(Translations[lang]["Title"]));
        sb.AppendLine("      </Text>");

        // TV code below the title, centered
        sb.AppendLine("      <Text id=\"CodeText\" top=\"180\" left=\"300\" width=\"680\" height=\"120\"  foreground=\"argb(255,255,255,255)\" alignment=\"center\" justification=\"center\">");
        sb.AppendLine(HttpUtility.HtmlEncode(string.Format(Translations[lang]["CodeInfo"], tvCode)));
        sb.AppendLine("      </Text>");

        sb.AppendLine("    </Panel>");
        sb.AppendLine("  </MrmlPage>");
        sb.AppendLine("</uidescription>");

        Response.Write(sb.ToString());
        Response.Flush();
        HttpContext.Current.ApplicationInstance.CompleteRequest();
    }

    // --- Secure random TV code ---
    private string GenerateRandomCode(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var result = new char[length];

        using (var rng = RandomNumberGenerator.Create())
        {
            byte[] buffer = new byte[length];
            rng.GetBytes(buffer);

            for (int i = 0; i < length; i++)
            {
                result[i] = chars[buffer[i] % chars.Length];
            }
        }

        return new string(result);
    }
}
