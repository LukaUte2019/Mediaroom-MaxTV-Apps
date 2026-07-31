using System;
using System.Drawing.Printing;
using System.Web;
using System.Web.UI;

public partial class PrintTest : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        string printerName = Request.QueryString["printer"];
        string messageToPrint = Request.QueryString["message"];
        string responseMessage;

        if (!string.IsNullOrEmpty(printerName))
        {
            try
            {
                PrintDocument pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = printerName;

                pd.PrintPage += delegate (object s, PrintPageEventArgs ev)
                {
                    ev.Graphics.DrawString(
                        string.IsNullOrEmpty(messageToPrint) ? "Test Page from Mediaroom MRML" : messageToPrint,
                        new System.Drawing.Font("Arial", 24),
                        System.Drawing.Brushes.Black,
                        100,
                        100
                    );
                };

                pd.Print(); // send to printer
                responseMessage = "Print job sent to " + HttpUtility.HtmlEncode(printerName);
            }
            catch (Exception ex)
            {
                responseMessage = "Error printing: " + HttpUtility.HtmlEncode(ex.Message);
            }
        }
        else
        {
            responseMessage = "No printer specified.";
        }

        // Return simple MRML confirmation page
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = System.Text.Encoding.UTF8;

        string mrml = string.Format(
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<uidescription version=""3.0"">
  <MrmlPage id=""PrintTest"" appid=""lukatube.printers/1.0"" width=""1280"" height=""720"">
    <Panel>
      <Text top=""100"" left=""40"" width=""1200"" height=""60"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">{0}</Text>
    </Panel>
  </MrmlPage>
</uidescription>", responseMessage);

        Response.Write(mrml);
        Response.End();
    }
}