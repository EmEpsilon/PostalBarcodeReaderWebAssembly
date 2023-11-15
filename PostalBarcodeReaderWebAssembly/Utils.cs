using SixLabors.ImageSharp.PixelFormats;
using System.Text;
using System.Threading;

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

        public static byte[] FromBase64StringParallel4(string input)
        {
            int totalLength = input.Length;
            var partLength = totalLength / 4 / 4 * 4;
            var lastLength = totalLength - partLength * 3;
            var task1 = new Task<byte[]>(() => System.Convert.FromBase64String(input.Substring(0,            partLength)));
            var task2 = new Task<byte[]>(() => System.Convert.FromBase64String(input.Substring(partLength*1, partLength)));
            var task3 = new Task<byte[]>(() => System.Convert.FromBase64String(input.Substring(partLength*2, partLength)));
            var task4 = new Task<byte[]>(() => System.Convert.FromBase64String(input.Substring(partLength*3, lastLength)));
            task1.Start();
            task2.Start();
            task3.Start();
            task4.Start();
            task1.Wait();
            task2.Wait();
            task3.Wait();
            task4.Wait();

            byte[] result1 = task1.Result;
            byte[] result2 = task2.Result;
            byte[] result3 = task3.Result;
            byte[] result4 = task4.Result;

            byte[] result = new byte[result1.Length + result2.Length + result3.Length + result4.Length];
            int typeSize = System.Runtime.InteropServices.Marshal.SizeOf(result1.GetType().GetElementType());
            int tmpPoint = 0;
            Buffer.BlockCopy(result1, 0, result, tmpPoint, result1.Length * typeSize);
            tmpPoint += result1.Length * typeSize;
            Buffer.BlockCopy(result2, 0, result, tmpPoint, result2.Length * typeSize);
            tmpPoint += result2.Length * typeSize;
            Buffer.BlockCopy(result3, 0, result, tmpPoint, result3.Length * typeSize);
            tmpPoint += result3.Length * typeSize;
            Buffer.BlockCopy(result4, 0, result, tmpPoint, result4.Length * typeSize);

            return result;
        }
    }
}