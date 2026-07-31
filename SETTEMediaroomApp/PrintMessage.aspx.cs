using System;
using System.Drawing.Printing;
using System.Web;
using System.Web.UI;

public partial class PrintMessage : Page
{
    protected void Page_Load(object sender, EventArgs e)
    {
        // Get printer name and message from query string
        string printerName = Request.QueryString["printer"];
        string message = Request.QueryString["message"];

        string responseMessage;

        if (string.IsNullOrEmpty(printerName))
        {
            responseMessage = "No printer specified.";
        }
        else
        {
            try
            {
                // Create a PrintDocument
                PrintDocument pd = new PrintDocument();
                pd.PrinterSettings.PrinterName = printerName;

                // Handle the PrintPage event to draw the message
                pd.PrintPage += delegate(object s, PrintPageEventArgs ev)
                {
                    string printText = string.IsNullOrEmpty(message) ? "Hello from Mediaroom!" : message;
                    ev.Graphics.DrawString(
                        printText,
                        new System.Drawing.Font("Arial", 24),
                        System.Drawing.Brushes.Black,
                        100,
                        100
                    );
                };

                // Send to printer
                pd.Print();
                responseMessage = "Message sent to printer: " + HttpUtility.HtmlEncode(printerName);
            }
            catch (Exception ex)
            {
                responseMessage = "Error printing: " + HttpUtility.HtmlEncode(ex.Message);
            }
        }

        // Return simple MRML confirmation page
        Response.Clear();
        Response.ContentType = "application/vnd.microsoft-tvui+xml; charset=utf-8";
        Response.ContentEncoding = System.Text.Encoding.UTF8;

        string mrml = string.Format(
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<uidescription version=""3.0"">
  <MrmlPage id=""PrintMessage"" appid=""lukatube.printers/1.0"" width=""1280"" height=""720"">
    <Panel>
      <Text top=""100"" left=""40"" width=""1200"" height=""60"" fontstyle=""Reg28"" foreground=""argb(255,255,255,255)"">{0}</Text>
    </Panel>
  </MrmlPage>
</uidescription>", responseMessage);

        Response.Write(mrml);
        Response.End();
    }
}