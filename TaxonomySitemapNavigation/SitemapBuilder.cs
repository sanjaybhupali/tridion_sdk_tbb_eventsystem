using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TaxonomySitemapNavigation
{
    public class SitemapGenerator
    {
        public SitemapUrlSet CreateSitemap(TaxonomyBasedNavigationItem root)
        {
            var urlSet = new SitemapUrlSet();
            FlattenAndPopulate(root, urlSet.Urls);
            return urlSet;
        }

        public static void FlattenAndPopulate(TaxonomyBasedNavigationItem item, List<SitemapUrl> flatList)
        {
            // 1. Add all URLs found in the current node
            if (item.urlMapNavigationItem != null)
            {
                foreach (var map in item.urlMapNavigationItem)
                {
                    flatList.Add(new SitemapUrl
                    {
                        
                        Loc = map.url,
                        LastMod = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                         
                        Priority = "0.5",
                        DisplayTitle = map.displayTitle,
                        
                    });
                }
            }

            // 2. Recursively visit all children
            if (item.childTaxonomyBasedNavigationItem != null)
            {
                foreach (var child in item.childTaxonomyBasedNavigationItem)
                {
                    FlattenAndPopulate(child, flatList);
                }
            }
        }
    }

}
