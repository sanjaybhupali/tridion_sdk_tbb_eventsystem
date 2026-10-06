using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SitemapNavigation
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
                        RouteName = map.routeName,
                        Loc = map.url,
                        LastMod = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                        CategoryTitle = map.categoryTitle,
                        Priority = "0.5",
                        DisplayTitle = map.displayTitle,
                        VisibilityStatus = map.visibilityStatus 
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
