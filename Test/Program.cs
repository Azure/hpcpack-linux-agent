using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    class Program
    {
        static void Main(string[] args)
        {
            byte[] abc = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            string test = "ttt";
            HttpClient c = new HttpClient();
            JsonMediaTypeFormatter f = new JsonMediaTypeFormatter();
            MemoryStream ms = new MemoryStream();
            f.WriteToStream(typeof(Tuple<byte[], string>), Tuple.Create(abc, test), ms, Encoding.Default);

            ms.Seek(0, SeekOrigin.Begin);

            var obj = f.ReadFromStream(typeof(Tuple<byte[], string>), ms, Encoding.Default, null);
            var t = (Tuple<byte[], string>)obj;
            var abc1 = t.Item1;

            StreamReader sr = new StreamReader(ms);

            Console.WriteLine(sr.ReadToEnd());
        }
    }
}
