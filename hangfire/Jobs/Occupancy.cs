using Hangfire.Server;
using Hangfire.Console;
using Prometheus;
using HtmlAgilityPack;

namespace hangfire.Jobs
{

    public class Occupancy
    {
        private static readonly Gauge OrlovaPool = Metrics
            .CreateGauge("occupancy_orlova_pool", "Number of people currently at Orlova pool");
        private static readonly Gauge OrlovaSauna = Metrics
            .CreateGauge("occupancy_orlova_sauna", "Number of people currently at Orlova sauna");
        private static readonly Gauge OlomoucPool = Metrics
            .CreateGauge("occupancy_olomouc_pool", "Number of people currently at Olomouc pool");
        private static readonly Gauge OlomoucSauna = Metrics
            .CreateGauge("occupancy_olomouc_sauna", "Number of people currently at Olomouc sauna");

        private static Dictionary<string, int> ScrapeAsync(string url, string poolXpath, string saunaXpath)
        {
            var map = new Dictionary<string, int>();

            var web = new HtmlWeb();
            var doc = web.Load(url);

            HtmlNode poolNode = doc.DocumentNode.SelectSingleNode(poolXpath);
            if (poolNode != null)
                map["pool"] = Int32.Parse(poolNode.InnerText);
            HtmlNode saunaNode = doc.DocumentNode.SelectSingleNode(saunaXpath);
            if (saunaNode != null)
                map["sauna"] = Int32.Parse(saunaNode.InnerText);
            return map;
        }

        public static void Run(PerformContext context)
        {
            context.WriteLine("Scraping relaxcentrumorlova.cz");
            var orlova = ScrapeAsync("https://relaxcentrumorlova.cz/", "//*[@id=\"text-bazen\"]/span", "//*[@id=\"text-sauna\"]/span");

            context.WriteLine("Scraping bazen-olomouc.cz");
            var olomouc = ScrapeAsync("https://www.bazen-olomouc.cz/", "/html/body/header/div[1]/div/div/ul/li[2]/strong", "/html/body/header/div[1]/div/div/ul/li[1]/strong");

            context.SetTextColor(ConsoleTextColor.DarkCyan);
            context.WriteLine($"OrlovaPool: {orlova["pool"]}");
            context.WriteLine($"OrlovaSauna: {orlova["sauna"]}");
            context.WriteLine($"OlomoucPool: {olomouc["pool"]}");
            context.WriteLine($"OlomoucSauna: {45 - olomouc["sauna"]}");
            context.ResetTextColor();

            context.WriteLine("Incerasing gauges");
            OrlovaPool.Set(orlova["pool"]);
            OrlovaSauna.Set(orlova["sauna"]);
            OlomoucPool.Set(olomouc["pool"]);
            OlomoucSauna.Set(45 - olomouc["sauna"]);
        }
    }
}