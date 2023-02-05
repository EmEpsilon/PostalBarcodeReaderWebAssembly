using SixLabors.ImageSharp.PixelFormats;
using System.Text;

namespace PostalBarcodeReaderWebAssembly
{
    public static class Utils
    {
        public static string FormatCustomerBarcode(string cb)
        {
            StringBuilder sb = new StringBuilder();

            if( cb.Length < 7 )
            {
                return cb;
            }

            sb.Append(cb.Substring(0, 7).Insert(3, "-"));
            sb.Append(" ");
            if (cb.Length >= 8)
            {
                sb.Append(cb.Substring(7, cb.Length - 7));
            }

            return sb.ToString();
        }

        public static string GetZipCodeFromCustomerBarcode(string cb)
        {
            if (cb.Length < 7)
            {
                return "";
            }
            return cb.Substring(0, 7);
        }

        public static string GetFormatedZipCodeFromCustomerBarcode(string cb)
        {
            string wk = GetZipCodeFromCustomerBarcode(cb);
            if (wk == "")
            {
                return "";
            }
            return wk.Insert(3, "-");
        }

        public static string GetPostalZipURL(string cb)
        {
            string wk = GetZipCodeFromCustomerBarcode(cb);
            if( wk == "" )
            {
                return "";
            }

            string baseUrl = "https://www.post.japanpost.jp/cgi-zip/zipcode.php?zip=";
            return baseUrl + wk;
        }
    }
}