using System.Globalization;
using System.Xml.Linq;
using DXLogQsoRecorder.Models;

namespace DXLogQsoRecorder.Services;

public sealed class DxLogXmlParser
{
    public bool TryParse(string xml, out DxLogQso? qso, out string error)
    {
        qso = null;
        error = "";
        try
        {
            var doc = XDocument.Parse(xml);
            var root = doc.Root;
            if (root is null || !string.Equals(root.Name.LocalName, "contactinfo", StringComparison.OrdinalIgnoreCase))
            {
                error = "The packet root element is not contactinfo.";
                return false;
            }

            string V(string name) => root.Elements().FirstOrDefault(x =>
                string.Equals(x.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value.Trim() ?? "";

            if (!DateTime.TryParseExact(V("timestamp"), "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var timestamp))
            {
                error = "The timestamp field is invalid.";
                return false;
            }

            bool.TryParse(V("newqso"), out var isNew);
            qso = new DxLogQso
            {
                Logger = V("logger"), QsoId = V("qsoid"), ContestName = V("contestname"),
                Timestamp = timestamp, MyCall = V("mycall"), Call = V("call"), Band = V("band"),
                Mode = V("mode"), TxFrequency = V("txfreq"), RxFrequency = V("rxfreq"),
                StationId = V("stationid"), Guid = V("guid"), IsNewQso = isNew
            };
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
