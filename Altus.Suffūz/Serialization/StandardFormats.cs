using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Altus.Suffūz.Serialization
{
    public static class StandardFormats
    {
        public const string BINARY = "bin";
        public const string JSON = "json";
        public const string JSONP = "jsonp";
        public const string TEXT = "text";
        public const string CSV = "csv";
        public const string EXCEL = "excel";
        public const string HTML = "html";
        public const string XML = "xml";
        public const string PROTOCOL_DEFAULT = "*";

        static ContentTypesSection _section = null;
        static StandardFormats()
        {
            _section = ConfigurationManager.GetSection("messagingContentTypes") as ContentTypesSection;
        }

        public static string GetContentType(string format)
        {
            try
            {
                return _section.ContentTypes[format].ContentType;
            }
            catch
            {
                return _section.ContentTypes["*"].ContentType;
            }
        }
    }
}
