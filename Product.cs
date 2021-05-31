using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PracticeScrappingTask
{
    class Product
    {
        public String ProductUrl { get; set; }
        public String Category { get; set; }
        public String Brand { get; set; }
        public String Title { get; set; }
        public String Price { get; set; }
        public List<String> DescriptionList { get; set; }
        public bool Instock { get; set; }
        public Dictionary<String, String> ProductSpecs { get; set; }
        public String DateScraped { get; set; }
        public List<byte[]> MainImages { get; set; }

        public override bool Equals(Object prod)
        {
            return prod is Product && ((Product)prod).ProductUrl == this.ProductUrl;
        }
    }
}
