namespace Sitecore.Support.Shell.Applications.Search.Search
{
    using System;
    using System.Reflection;
    using System.Text;
    using Sitecore.Diagnostics;
    using Sitecore.Globalization;
    using Sitecore.Web;
    public class SearchForm : Sitecore.Shell.Applications.Search.Search.SearchForm
    {
        private static readonly MethodInfo ShowAdvancedSearchMethodInfo =
            typeof(Sitecore.Shell.Applications.Search.Search.SearchForm).GetMethod("ShowAdvancedSearch",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo SearchMethodInfo =
            typeof(Sitecore.Shell.Applications.Search.Search.SearchForm).GetMethod("Search",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo CreatedFromChangedMethodInfo =
            typeof(Sitecore.Shell.Applications.Search.Search.SearchForm).GetMethod("CreatedFromChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo CreatedToChangedMethodInfo =
            typeof(Sitecore.Shell.Applications.Search.Search.SearchForm).GetMethod("CreatedToChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo UpdatedFromChangedMethodInfo =
            typeof(Sitecore.Shell.Applications.Search.Search.SearchForm).GetMethod("UpdatedFromChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo UpdatedToChangedMethodInfo =
            typeof(Sitecore.Shell.Applications.Search.Search.SearchForm).GetMethod("UpdatedToChanged",
                BindingFlags.Instance | BindingFlags.NonPublic);

        protected override void OnLoad([NotNull] EventArgs e)
        {
            Assert.IsNotNull(e, "e");

            if (this.Unsupported != null)
            {
                this.Unsupported.Parent.Controls.Remove(this.Unsupported);
            }

            var simple = true;

            if (!Context.ClientPage.IsEvent)
            {
                if (Context.ClientPage.IsPostBack)
                {
                    if (Context.ClientPage.Request["Advanced"] != null)
                    {
                        ShowAdvancedSearchMethodInfo.Invoke(this, new object[] { });
                        simple = false;
                    }
                    else
                    {
                        var search = this.GetSearchString();

                        if (search.Length > 0)
                        {
                            SearchMethodInfo.Invoke(this, new object[] { search });
                            simple = false;
                        }
                    }
                }
                else
                {
                    var search = WebUtil.GetQueryString("qs");

                    if (search.Length > 0)
                    {
                        SearchMethodInfo.Invoke(this, new object[] { search });
                        simple = false;
                    }
                    else if (WebUtil.GetQueryString("mo") == "ad")
                    {
                        ShowAdvancedSearchMethodInfo.Invoke(this, new object[] { });
                        simple = false;
                    }
                }

                if (simple)
                {
                    this.Results.Parent.Controls.Remove(this.Results);
                    this.Advanced.Parent.Controls.Remove(this.Advanced);
                }

                this.execute.Attributes["value"] = Translate.Text(Texts.SEARCH);
                this.Search2.Attributes["value"] = Translate.Text(Texts.SEARCH);
                this.Search3.Attributes["value"] = Translate.Text(Texts.SEARCH);
            }

            if (this.CreatedFrom != null)
            {
                this.CreatedFrom.OnChanged += (sender, eventArgs) => CreatedFromChangedMethodInfo.Invoke(this, new object[] { sender, eventArgs });
                this.CreatedTo.OnChanged += (sender, eventArgs) => CreatedToChangedMethodInfo.Invoke(this, new object[] { sender, eventArgs });
                this.UpdatedFrom.OnChanged += (sender, eventArgs) => UpdatedFromChangedMethodInfo.Invoke(this, new object[] { sender, eventArgs });
                this.UpdatedTo.OnChanged += (sender, eventArgs) => UpdatedToChangedMethodInfo.Invoke(this, new object[] { sender, eventArgs });
            }
        }

        private string ProcessDateValue(string date, bool endDate = false)
        {
            var from = date.Length > 0 ? DateUtil.IsoDateToDateTime(date) : endDate ? DateTime.MaxValue : DateTime.MinValue;
            if ((from != DateTime.MinValue && !endDate) || (endDate && from != DateTime.MaxValue))
            {
                from = new DateTime(from.Year, from.Month, from.Day, 0, 0, 0, DateTimeKind.Local);
                if (endDate)
                {
                    from = from.AddDays(1).AddSeconds(-1);
                }
            }

            return DateUtil.ToIsoDate(DateUtil.ToUniversalTime(from));
        }

        private string CreateDateFilter(string fromValue, string toValue, string fieldName)
        {
            if (fromValue.Length > 0 || toValue.Length > 0)
            {
                return "+" + fieldName + ":[" + ProcessDateValue(fromValue) + " TO " + ProcessDateValue(toValue, true) + "];";
            }
            return string.Empty;
        }

        [NotNull]
        private string GetSearchString()
        {
            StringBuilder result = new StringBuilder();
            if (this.SearchAgain.Value.Length > 0)
            {
                result.Append(this.SearchAgain.Value);
            }
            else
            {
                result.Append("text:");
                result.Append(this.AdvancedSearch.Value.Length > 0 ? this.AdvancedSearch.Value : this.SimpleSearch.Value);
                result.Append(";");
            }

            // created
            var createdFrom = StringUtil.GetString(Context.ClientPage.ClientRequest.Form["CreatedFromField"]);
            var createdTo = StringUtil.GetString(Context.ClientPage.ClientRequest.Form["CreatedToField"]);

            result.Append(CreateDateFilter(createdFrom, createdTo, "__smallcreateddate"));

            // updated
            var updatedFrom = StringUtil.GetString(Context.ClientPage.ClientRequest.Form["UpdatedFromField"]);
            var updatedTo = StringUtil.GetString(Context.ClientPage.ClientRequest.Form["UpdatedToField"]);

            result.Append(CreateDateFilter(updatedFrom, updatedTo, "__smallupdateddate"));

            // author
            if (this.Author.Value.Length > 0)
            {
                result.Append("+author:");
                result.Append(this.Author.Value.Replace("\\", ""));
                result.Append(";");
            }

            return result.ToString();
        }
    }
}