using System;
using System.Collections.Generic;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Drawing.Printing;

public partial class ListPrinters : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        // Get message from query string
        string customMessage = Request.QueryString["message"];
        if (string.IsNullOrEmpty(customMessage))
        {
            customMessage = "Hello from Mediaroom!";
        }

        // Get installed printers
        List<Printer> printers = GetInstalledPrinters();

        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = Encoding.UTF8;
        Response.Cache.SetNoStore();

        StringBuilder sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""utf-8""?>");
        sb.AppendLine(@"<uidescription version=""3.0"">");
        sb.AppendLine(@"<MrmlPage id=""ListPrinters"" appid=""lukatube.printers/1.0"" width=""1280"" height=""720"">");
        sb.AppendLine(@"<Panel>");
        sb.AppendLine(@"<Text top=""10"" left=""40"" width=""1200"" height=""40"" fontstyle=""Reg32"" foreground=""argb(255,255,255,255)"">Available Printers</Text>");

        // Show message preview at the top (after title)
        string decodedMsg = HttpUtility.UrlDecode(customMessage);
        string preview = TruncateString(decodedMsg, 400); // wrap / truncate if needed
        sb.AppendLine(string.Format(
            @"<Text top=""60"" left=""40"" width=""1200"" height=""60"" fontstyle=""Reg28"" foreground=""argb(255,200,200,200)"">{0}</Text>",
            HttpUtility.HtmlEncode(preview)
        ));

        int topPos = 130; // start printer list below the message preview
        foreach (var printer in printers)
        {
            sb.AppendLine(string.Format(
                @"<Text top=""{0}"" left=""40"" width=""800"" height=""40"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">{1}</Text>",
                topPos,
                HttpUtility.HtmlEncode(printer.Name)
            ));

            // Button URL with printer name and message sent to PrintMessage.aspx
            string buttonUrl = "http://172.16.40.101/SETTEMediaroomApp/PrintMessage.aspx?printer="
                               + HttpUtility.UrlEncode(printer.Name)
                               + "&message=" + HttpUtility.UrlEncode(customMessage);

            sb.AppendLine(string.Format(
                @"<Button top=""{0}"" left=""900"" width=""300"" height=""40"" href=""page:{1}""><Text>Print Message</Text></Button>",
                topPos,
                HttpUtility.HtmlAttributeEncode(buttonUrl)
            ));

            topPos += 50; // spacing between printers
        }

        sb.AppendLine(@"</Panel>");
        sb.AppendLine(@"</MrmlPage>");
        sb.AppendLine(@"</uidescription>");

        Response.Write(sb.ToString());
        Response.End();
    }

    private List<Printer> GetInstalledPrinters()
    {
        List<Printer> printers = new List<Printer>();
        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            printers.Add(new Printer { Name = printerName });
        }
        return printers;
    }

    private static string TruncateString(string s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return s ?? "";
        if (s.Length <= maxLen) return s;
        return s.Substring(0, maxLen - 1) + "…";
    }

    public class Printer
    {
        public string Name { get; set; }
    }
}