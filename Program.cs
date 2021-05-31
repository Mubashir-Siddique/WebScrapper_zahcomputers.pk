using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;

namespace PracticeScrappingTask
{
    class Program
    {
        static String data = Get("http://zahcomputers.pk");
        static ConcurrentBag<Product> Products = new ConcurrentBag<Product>();
        static readonly object _object = new object();
        static FileInfo fileInformation;

        static void Main(string[] args)
        {
            try
            {
                if (!Directory.Exists(Application.StartupPath + @"\ProductImages"))
                {
                    Directory.CreateDirectory(Application.StartupPath + @"\ProductImages");
                }

                // Overwrite the file if previous data exists
                File.WriteAllText(Application.StartupPath + @"\" + "zahcomputers.txt","");

                if (!String.IsNullOrEmpty(data))
                {
                    HtmlAgilityPack.HtmlDocument Doc = new HtmlAgilityPack.HtmlDocument();
                    Doc.LoadHtml(data);

                    var SelectedNodes = Doc.DocumentNode.SelectNodes("//a[@class='ux-menu-link__link flex']");

                    List<string> UrlLinks = new List<string>();

                    GetLinksUrls(SelectedNodes, UrlLinks);

                    try
                    {
                        //foreach (var item in UrlLinks)
                        //{
                        //    Thread childThread = new Thread(() =>
                        //    {
                        //        PerformUrlsRequest(item);
                        //    });

                        //    childThread.Start();
                        //}

                        Parallel.ForEach(UrlLinks, item => PerformUrlsRequest(item));

                        Console.WriteLine("_________________________");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine();
                    }
                    //Console.ReadLine();

                    File.AppendAllText(Application.StartupPath + @"\" + "zahcomputers.txt", "]");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        private static void PerformUrlsRequest(string categoryURL)
        {
            int MaxPages = 0;
            String PageResult = String.Empty;
            HtmlAgilityPack.HtmlDocument Doc = new HtmlAgilityPack.HtmlDocument();
            String CategoryName = String.Empty;

            try
            {
                String data = Get(categoryURL);

                Doc.LoadHtml(data);

                MaxPages = GetPagesCount(Doc);

                for (int PageNum = 1; PageNum <= MaxPages; PageNum++)
                {
                    PageResult = Get(categoryURL + @"page/" + PageNum);

                    Doc.LoadHtml(PageResult);
                    var ProductPerPage = Doc.DocumentNode.SelectNodes("//div[@class='image-none']/a");

                    if (ProductPerPage != null)
                    {
                        List<String> ProductsLinks = new List<String>();

                        GetLinksUrls(ProductPerPage, ProductsLinks);

                        CategoryName = categoryURL.Replace("https://zahcomputers.pk/category/", "").TrimEnd('/');

                        //foreach (String link in ProductsLinks)
                        //{
                        //    ExtractProductInfo(link, CategoryName);
                        //}

                        Parallel.ForEach(ProductsLinks, link => ExtractProductInfo(link,CategoryName));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
            }
        }

        static void ExtractProductInfo(String productUrl, String category)
        {
            HtmlAgilityPack.HtmlDocument Doc = new HtmlAgilityPack.HtmlDocument();

            try
            {
                String data = Get(productUrl);
                Doc.LoadHtml(data);

                // **** Checking if the specific nodes exists then extract the data **** //

                String BrandName = Doc.DocumentNode.SelectSingleNode("//span[@class='yith-wcbr-brands']") == null ? "" : Doc.DocumentNode.SelectSingleNode("//span[@class='yith-wcbr-brands']").InnerText.Trim().Replace("Brand:", "");

                String ProductTitle = Doc.DocumentNode.SelectSingleNode("//h1[@class='product-title product_title entry-title']") == null ? "" : Doc.DocumentNode.SelectSingleNode("//h1[@class='product-title product_title entry-title']").InnerText.Trim();

                String Price = Doc.DocumentNode.SelectSingleNode("//p[@class='price product-page-price ']/span[@class='woocommerce-Price-amount amount']/bdi") == null ? "" : Doc.DocumentNode.SelectSingleNode("//p[@class='price product-page-price ']/span[@class='woocommerce-Price-amount amount']/bdi").InnerText.Trim();

                HtmlNodeCollection DescrpListNodes = Doc.DocumentNode.SelectNodes("//div[@class='product-info summary col col-fit entry-summary product-summary']/descendant::ul/li");

                HtmlNode specsNode = Doc.DocumentNode.SelectSingleNode("//div[@id='tab-description']");

                HtmlNode IsInStock = Doc.DocumentNode.SelectSingleNode("//p[@class='stock out-of-stock']");

                HtmlNodeCollection ImagesNodes = Doc.DocumentNode.SelectNodes("//figure/div/a");


                GetProductSpecs(specsNode);
                // **** Decode Html Encoded Text **** //
                Price = HttpUtility.HtmlDecode(Price).TrimStart('₨');
                ProductTitle = HttpUtility.HtmlDecode(ProductTitle);

                Product ProductObj = null;

                try
                {
                    ProductObj = new Product()
                    {
                        ProductUrl = productUrl == null ? "" : productUrl,
                        Title = ProductTitle == null ? "" : ProductTitle,
                        Category = category == null ? "" : category,
                        Brand = BrandName == null ? "" : BrandName,
                        Price = Price == null ? "" : Price,
                        Instock = IsInStock == null ? false : true,
                        DateScraped = DateTime.Now.ToLongDateString() + "-" + DateTime.Now.ToLongTimeString(),
                        MainImages = null,
                        DescriptionList = GetDescriptionList(DescrpListNodes),
                        ProductSpecs = GetProductSpecs(specsNode)

                    };

                    Task.Factory.StartNew(
                        ()=> DownloadImages(ImagesNodes, ProductObj.Title)
                        );

                    lock (_object)
                    {
                        fileInformation = new FileInfo(Application.StartupPath + @"\" + "zahcomputers.txt");

                        if (fileInformation.Exists)
                        {
                            if (fileInformation.Length == 0)
                            {
                                File.AppendAllText(Application.StartupPath + @"\" + "zahcomputers.txt", "[");
                                File.AppendAllText(Application.StartupPath + @"\" + "zahcomputers.txt", JsonConvert.SerializeObject(ProductObj));
                            }
                            else
                            {
                                File.AppendAllText(Application.StartupPath + @"\" + "zahcomputers.txt", "," +JsonConvert.SerializeObject(ProductObj));
                            }
                        }
                        //File.AppendAllText(Application.StartupPath + @"\" + "zahcomputers.txt", JsonConvert.SerializeObject(ProductObj) + ",");
                    }                  
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                }

                lock (_object)
                {
                    var ExistingProduct = Products.SingleOrDefault(x => x.ProductUrl == ProductObj.ProductUrl);

                    if (ExistingProduct == null)
                    {
                        Console.WriteLine(ProductObj.Category + " >>> " + ProductObj.Title);
                        Products.Add(ProductObj);
                    }
                    else
                    {
                        Console.WriteLine("Duplicate");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
            }
        }

        static Dictionary<String, String> GetProductSpecs(HtmlNode specsMainNode)
        {
            Dictionary<String, String> Specs = new Dictionary<string, string>();

            try
            {
                if (specsMainNode != null)
                {
                    var specsnodes = specsMainNode.SelectNodes("//tr[@class='pair_3u9fnESIQrtuEu6ye_zM4k']");

                    if (specsnodes != null)
                    {
                        foreach (var subNodes in specsnodes)
                        {
                            var splitString = HttpUtility.HtmlDecode(subNodes.InnerText).Trim().Split('\n');
                            Specs.Add(splitString[0], splitString[1]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Specs = null;
            }

            return Specs;
        }

        static void DownloadImages(HtmlNodeCollection ImagesNodes, String productTitle)
        {
            try
            {
                if (ImagesNodes != null)
                {
                    List<String> ImagesLinks = new List<string>();
                    GetLinksUrls(ImagesNodes, ImagesLinks);
                    int picNumber = 1;

                    Parallel.ForEach(ImagesLinks, image =>
                    {
                        using (WebClient client = new WebClient())
                        {
                            if (Directory.Exists(Application.StartupPath + @"\ProductImages"))
                            {
                                productTitle = Regex.Replace(productTitle, @"[^0-9a-zA-Z]+", "");

                                if (productTitle.Length > 80)
                                {
                                    productTitle = productTitle.Remove(50, productTitle.Length - 50);
                                }
                                Directory.CreateDirectory(Application.StartupPath + @"\ProductImages\" + productTitle);
                            }

                            lock (_object)
                            {
                                client.DownloadFile(image, Application.StartupPath + @"\ProductImages\" + productTitle + @"\image" + picNumber + ".jpeg");
                            }
                        }

                        picNumber++;
                        Console.WriteLine(image);
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
            }
        }
        static List<String> GetDescriptionList(HtmlNodeCollection descriptionNodes)
        {
            List<string> descrpList = new List<string>();

            try
            {
                if (descriptionNodes != null)
                {
                    foreach (HtmlNode node in descriptionNodes)
                    {
                        descrpList.Add(HttpUtility.HtmlDecode(node.InnerText).Trim());
                    }
                }

                return descrpList;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return descrpList;
            }
        }

        static String Get(String url)
        {
            String result = String.Empty;
            try
            {
                HttpWebRequest _WebRequest = (HttpWebRequest)WebRequest.Create(url);

                WebProxy myproxy = new WebProxy("127.0.0.1:8888", false);
                myproxy.BypassProxyOnLocal = false;
                _WebRequest.Proxy = myproxy;
                _WebRequest.Method = "GET";

                _WebRequest.Credentials = CredentialCache.DefaultCredentials;

                //GetResponce
                HttpWebResponse responce = (HttpWebResponse)_WebRequest.GetResponse();
                Console.WriteLine(responce.StatusDescription);

                if (responce.StatusDescription == "OK")
                {
                    //Read Responce Stream
                    using (Stream dataStream = responce.GetResponseStream())
                    {
                        using (StreamReader reader = new StreamReader(dataStream))
                        {
                            result = reader.ReadToEnd();
                            //Console.WriteLine(responcefromServer);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
            }
            return result;
        }

        static List<string> GetLinksUrls(HtmlNodeCollection nodes, List<string> links)
        {
            try
            {
                //Selecting 'href' attributes of <a> Tag
                foreach (var item in nodes)
                {
                    links.Add(item.Attributes["href"].Value);     // Attributes["href"].Value.Replace("&amp;", "&")
                }
                Debug.WriteLine(links.Count);
                return links;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        static int GetPagesCount(HtmlAgilityPack.HtmlDocument Doc)
        {
            int PagesCount = 0;

            try
            {
                HtmlNodeCollection nodes = Doc.DocumentNode.SelectNodes("//a[@class='page-number']");

                if (nodes != null)
                {
                    HtmlNode LastNode = nodes.Last();
                    PagesCount = Convert.ToInt32(LastNode.InnerText.ToString());
                }
                else
                {
                    PagesCount = 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return PagesCount;
        }

    }
}
