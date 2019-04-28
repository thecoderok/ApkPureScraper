using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ApkPureScraper
{
    class Program
    {
        static void Main(string[] args)
        {
            Task.Run(Run).Wait();
        }

        private static async Task Run()
        {
            var packages = new HashSet<string>();
            var errors_packages = new HashSet<string>();
            var processed_packaes = new HashSet<string>();
            if (File.Exists("processed.txt"))
            {
                processed_packaes.UnionWith(File.ReadAllLines("processed.txt"));
            }
            packages.UnionWith(File.ReadAllLines("Input.txt"));
            Console.WriteLine("Found {0} packages", packages.Count);
            
            foreach (var package in packages)
            {
                if (processed_packaes.Contains(package))
                {
                    Console.WriteLine("Already processed {0} package.", package);
                    continue;
                }
                Console.WriteLine("Processing {0} package.", package);
                try
                {
                    var items = await processSinglePackage(package);
                    var result = new StringBuilder();
                    foreach (var key in items.Keys)
                    {
                        string val = JsonConvert.SerializeObject(items[key]);
                        result.Append(key).Append("\t").Append(val).Append("\n");
                    }
                    File.AppendAllText("result.txt", result.ToString());
                    File.AppendAllText("processed.txt", package + "\n");
                }
                catch(Exception e)
                {
                    File.AppendAllText("errors.txt", String.Format("{0}, error: {1}\n", package, e.Message));
                    continue;
                }
            }
           
            Console.WriteLine("Done");
        }

        private static async Task<Dictionary<string, Object>> processSinglePackage(string package)
        {
            const string URL_TEMPLATE = "https://apkpure.com/abc/{0}/versions";
            const string SAFE_URL_LINK = ".//*[@href='/faq-safe.html']";
            const string XPATH = "//*[@class='ver-wrap']/li";
            const string SIGNATURE_XPATH = ".//*[.='Signature: ']/..";
            const string FILE_SHA_XPATH = ".//*[.='File SHA1: ']/..";

            var client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_12_6) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/60.0.3112.113 Safari/537.36");
            client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.8,ru;q=0.6,es;q=0.4,uk;q=0.2");
            string content = await GetHtmlContent(string.Format(URL_TEMPLATE, package), client);
            HtmlDocument htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(content);

            var results = new Dictionary<string, Object>();

            var version_items = htmlDocument.DocumentNode.SelectNodes(XPATH);
            Console.WriteLine("Found {0} version items.", version_items.Count);
            foreach(var version in version_items)
            {
                var safe_url_link = version.SelectSingleNode(SAFE_URL_LINK);
                if (safe_url_link == null)
                {
                    Console.WriteLine("Skipping version as it appears not to be safe");
                    continue;
                }
                var sha_el = version.SelectSingleNode(FILE_SHA_XPATH);
                if (sha_el == null)
                {
                    Console.WriteLine("Can't find File SHA1 hash");
                    continue;
                }
                var signature_el = version.SelectSingleNode(SIGNATURE_XPATH);
                if (signature_el == null)
                {
                    Console.WriteLine("Can't find signature hash");
                    continue;
                }
                string sha1 = sha_el.InnerText.Replace("File SHA1: ", "").Replace("\n", String.Empty).Trim();
                string signature = signature_el.InnerText.Replace("Signature: ", "").Replace("\n", String.Empty).Trim();
                string version_text = version.InnerText.Replace("  ", String.Empty).Replace("\n\n", "\n");
                var val = new {
                    package = package,
                    signature = signature,
                    version_text = version_text,
                };
                results.Add(sha1, val);
            }
            return results;
        }

        public static async Task<string> GetHtmlContent(string url, HttpClient client)
        {
            var responseMessage = await client.GetAsync(url).ConfigureAwait(false);
            if (!responseMessage.IsSuccessStatusCode)
            {
                throw new Exception(responseMessage.StatusCode.ToString());
            }
            var content = await responseMessage.Content.ReadAsStringAsync();
            return content;
        }
    }
}
