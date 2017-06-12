namespace Sitecore.Support.ContentSearch.Client.Pipelines.Search
{
    using System.Collections.Generic;
    using System.Linq;
    using Sitecore.ContentSearch;
    using Sitecore.ContentSearch.Abstractions;
    using Sitecore.ContentSearch.Client.Pipelines.Search;
    using Sitecore.ContentSearch.Diagnostics;
    using Sitecore.ContentSearch.Exceptions;
    using Sitecore.ContentSearch.SearchTypes;
    using Sitecore.ContentSearch.Utilities;
    using Sitecore.Data.Items;
    using Sitecore.Diagnostics;
    using Sitecore.Pipelines.Search;
    using Sitecore.Search;
    using Sitecore.Shell;
    using Sitecore.StringExtensions;
    public class SearchContentSearchIndex : Sitecore.ContentSearch.Client.Pipelines.Search.SearchContentSearchIndex
    {
        private ISettings settings;
        public override void Process(SearchArgs args)
        {
            Assert.ArgumentNotNull(args, "args");

            if (args.UseLegacySearchEngine)
            {
                return;
            }

            if (!ContentSearchManager.Locator.GetInstance<IContentSearchConfigurationSettings>().ContentSearchEnabled())
            {
                args.UseLegacySearchEngine = true;
                return;
            }

            if (!ContentSearchManager.Locator.GetInstance<ISearchIndexSwitchTracker>().IsOn)
            {
                args.IsIndexProviderOn = false;
                return;
            }

            var rootItem = args.Root ?? args.Database.GetRootItem();
            Assert.IsNotNull(rootItem, "rootItem");

            //Assert.IsNotNull(SearchManager.SystemIndex, "System index");

            if (args.TextQuery.IsNullOrEmpty())
            {
                return;
            }

            ISearchIndex index;

            try
            {
                index = ContentSearchManager.GetIndex(new SitecoreIndexableItem(rootItem));
            }
            catch (IndexNotFoundException)
            {
                SearchLog.Log.Warn("No index found for " + rootItem.ID);
                return;
            }

            if (!ContentSearchManager.Locator.GetInstance<ISearchIndexSwitchTracker>().IsIndexOn(index.Name))
            {
                args.IsIndexProviderOn = false;
                return;
            }

            if (this.settings == null)
            {
                this.settings = index.Locator.GetInstance<ISettings>();
            }

            using (var context = index.CreateSearchContext())
            {
                List<SitecoreUISearchResultItem> results = new List<SitecoreUISearchResultItem>();

                try
                {
                    IQueryable<SitecoreUISearchResultItem> query = null;

                    if (args.Type != SearchType.ContentEditor)
                    {
                        //fix for #95272
                        if (!string.IsNullOrEmpty(args.TextQuery))
                        {
                            var models = SearchStringModel.ExtractSearchQuery(args.TextQuery);
                            foreach (var model in models)
                            {
                                if (model.Type == "__smallcreateddate" || model.Type == "__smallupdateddate")
                                {
                                    model.Value = "#datecompare#" + model.Value;
                                }
                            }
                            query = LinqHelper.CreateQuery<SitecoreUISearchResultItem>(context, models, rootItem);
                        }
                        //end of fix for for #95272
                        else
                        {
                            query = new GenericSearchIndex().Search(args, context);
                        }
                    }
                    
                    if (query == null || Enumerable.Count(query) == 0)
                    {
                        if (args.ContentLanguage != null && !args.ContentLanguage.Name.IsNullOrEmpty())
                        {
                            query =
                                context.GetQueryable<SitecoreUISearchResultItem>()
                                    .Where(
                                        i =>
                                            i.Name.StartsWith(args.TextQuery) ||
                                            (i.Content.Contains(args.TextQuery) &&
                                             i.Language.Equals(args.ContentLanguage.Name)));
                        }
                        else
                        {
                            query =
                                context.GetQueryable<SitecoreUISearchResultItem>()
                                    .Where(i => i.Name.StartsWith(args.TextQuery) || i.Content.Contains(args.TextQuery));
                        }
                    }

                    // In content editor, we search the entire tree even if the root is supplied. If it is, the results will get special categorization treatment later on in the pipeline.
                    if (args.Root != null && args.Type != SearchType.ContentEditor)
                    {
                        query = query.Where(i => i.Paths.Contains(args.Root.ID));
                    }

                    foreach (var result in Enumerable.TakeWhile(query, result => results.Count < args.Limit))
                    {
                        if (!UserOptions.View.ShowHiddenItems)
                        {
                            var item = result.GetItem();
                            if (item != null && this.IsHidden(item))
                            {
                                continue;
                            }
                        }

                        var resultForSameItem = results.FirstOrDefault(r => r.ItemId == result.ItemId);
                        if (resultForSameItem == null)
                        {
                            results.Add(result);
                            continue;
                        }

                        if (args.ContentLanguage != null && !args.ContentLanguage.Name.IsNullOrEmpty())
                        {
                            if ((resultForSameItem.Language != args.ContentLanguage.Name &&
                                 result.Language == args.ContentLanguage.Name)
                                ||
                                (resultForSameItem.Language == result.Language &&
                                 resultForSameItem.Uri.Version.Number < result.Uri.Version.Number))
                            {
                                results.Remove(resultForSameItem);
                                results.Add(result);
                            }
                        }
                        else if (args.Type != SearchType.Classic)
                        {
                            if (resultForSameItem.Language == result.Language &&
                                resultForSameItem.Uri.Version.Number < result.Uri.Version.Number)
                            {
                                results.Remove(resultForSameItem);
                                results.Add(result);
                            }
                        }
                        else
                        {
                            results.Add(result);
                        }
                    }
                }
                catch (System.Exception e)
                {
                    Log.Error("Invalid lucene search query: " + args.TextQuery, e, this);
                    return;
                }

                foreach (var result in results)
                {
                    var title = result.DisplayName ?? result.Name;
                    object icon = result.Fields.Find(pair => pair.Key == Sitecore.ContentSearch.BuiltinFields.Icon).Value
                                  ?? result.GetItem().Appearance.Icon ?? this.settings.DefaultIcon();

                    string url = string.Empty;
                    if (result.Uri != null)
                    {
                        url = result.Uri.ToString();
                    }

                    args.Result.AddResult(new SearchResult(title, icon.ToString(), url));
                }
            }
        }
        private bool IsHidden([NotNull] Item item)
        {
            Assert.ArgumentNotNull(item, "item");

            return item.Appearance.Hidden || (item.Parent != null && this.IsHidden(item.Parent));
        }
    }
}