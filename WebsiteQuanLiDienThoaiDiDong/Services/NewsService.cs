using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using WebsiteQuanLiDienThoaiDiDong.Helpers;

namespace WebsiteQuanLiDienThoaiDiDong.Services
{
    public class NewsItem
    {
        public string Title { get; set; }
        public string Link { get; set; }
        public string Image { get; set; }
        public string Summary { get; set; }
        public string Source { get; set; }
        public DateTime Date { get; set; }
    }

    public class VideoItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Channel { get; set; }
        public DateTime Date { get; set; }
        public string Thumb { get { return "https://i.ytimg.com/vi/" + Id + "/hqdefault.jpg"; } }
        public string Url { get { return "https://www.youtube.com/watch?v=" + Id; } }
    }

    public class TinTucVM
    {
        public List<NewsItem> Tin { get; set; }
        public List<VideoItem> Video { get; set; }
    }

    // Lấy tin RSS và video YouTube, lưu tạm 30 phút. Lỗi mạng thì trả danh sách cũ/rỗng, không làm hỏng trang.
    public static class NewsService
    {
        private static readonly object _lock = new object();
        private static List<NewsItem> _news = new List<NewsItem>();
        private static DateTime _newsExp = DateTime.MinValue;
        private static List<VideoItem> _videos = new List<VideoItem>();
        private static DateTime _videoExp = DateTime.MinValue;

        public static List<NewsItem> GetNews(int n)
        {
            lock (_lock)
            {
                if (DateTime.Now > _newsExp)
                {
                    var list = LoadNews();
                    if (list.Count > 0) { _news = list; _newsExp = DateTime.Now.AddMinutes(30); }
                    else _newsExp = DateTime.Now.AddMinutes(2);
                }
                return _news.Take(n).ToList();
            }
        }

        public static List<VideoItem> GetVideos(int n)
        {
            lock (_lock)
            {
                if (DateTime.Now > _videoExp)
                {
                    var list = LoadVideos();
                    if (list.Count > 0) { _videos = list; _videoExp = DateTime.Now.AddMinutes(30); }
                    else _videoExp = DateTime.Now.AddMinutes(2);
                }
                return _videos.Take(n).ToList();
            }
        }

        private static string Fetch(string url)
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Timeout = 6000;
                req.UserAgent = "Mozilla/5.0 PhoneStore";
                using (var resp = req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    return sr.ReadToEnd();
            }
            catch { return null; }
        }

        private static List<NewsItem> LoadNews()
        {
            var res = new List<NewsItem>();
            var xml = Fetch(CauHinh.Get("Tin_RssUrl", "https://vnexpress.net/rss/khoa-hoc-cong-nghe.rss"));
            if (xml == null) return res;
            try
            {
                var doc = XDocument.Parse(xml);
                foreach (var it in doc.Descendants("item"))
                {
                    string link = (string)it.Element("link");
                    string title = (string)it.Element("title");
                    if (string.IsNullOrWhiteSpace(link) || string.IsNullOrWhiteSpace(title)) continue;

                    var enc = it.Element("enclosure");
                    string desc = (string)it.Element("description") ?? "";
                    int i = desc.IndexOf("</br>", StringComparison.OrdinalIgnoreCase);
                    string sum = i >= 0 ? desc.Substring(i + 5) : desc;
                    sum = WebUtility.HtmlDecode(Regex.Replace(sum, "<.*?>", "")).Trim();
                    if (sum.Length > 150) sum = sum.Substring(0, 147).TrimEnd() + "...";

                    DateTimeOffset dto;
                    DateTime date = DateTimeOffset.TryParse((string)it.Element("pubDate"), CultureInfo.InvariantCulture, DateTimeStyles.None, out dto)
                        ? dto.ToOffset(TimeSpan.FromHours(7)).DateTime : DateTime.Now;

                    string host = "";
                    try { host = new Uri(link).Host.Replace("www.", ""); } catch { }

                    res.Add(new NewsItem
                    {
                        Title = WebUtility.HtmlDecode(title.Trim()),
                        Link = link.Trim(),
                        Image = enc == null ? null : (string)enc.Attribute("url"),
                        Summary = sum,
                        Source = host,
                        Date = date
                    });
                }
            }
            catch { }
            return res;
        }

        private static List<VideoItem> LoadVideos()
        {
            var res = new List<VideoItem>();
            XNamespace a = "http://www.w3.org/2005/Atom";
            XNamespace yt = "http://www.youtube.com/xml/schemas/2015";
            var ids = CauHinh.Get("Video_ChannelIds", "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s != "");
            foreach (var id in ids)
            {
                var xml = Fetch("https://www.youtube.com/feeds/videos.xml?channel_id=" + Uri.EscapeDataString(id));
                if (xml == null) continue;
                try
                {
                    var doc = XDocument.Parse(xml);
                    string chan = (string)doc.Root.Element(a + "title");
                    foreach (var e in doc.Descendants(a + "entry"))
                    {
                        DateTime d;
                        res.Add(new VideoItem
                        {
                            Id = (string)e.Element(yt + "videoId"),
                            Title = (string)e.Element(a + "title"),
                            Channel = chan,
                            Date = DateTime.TryParse((string)e.Element(a + "published"), CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d : DateTime.MinValue
                        });
                    }
                }
                catch { }
            }
            return res.Where(v => !string.IsNullOrEmpty(v.Id)).OrderByDescending(v => v.Date).ToList();
        }
    }
}