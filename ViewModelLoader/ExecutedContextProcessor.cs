using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

#if NETSTANDARD2_0
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
#endif

#if NET46
using System.Net.Http;
using System.Web.Http.Filters;
#endif


namespace ViewModelLoader
{
    /// <summary>
    /// Calls loaders for content in the action executed context.
    /// </summary>
    public class ExecutedContextProcessor<TData>
    {
        private const string FIELDS_PARAMETER = "fields";
        private IDictionary<Type, object> _loaders = new Dictionary<Type, object>();

        /// <summary>
        /// Register loader.
        /// </summary>
        public void RegisterLoader(Type type, object loader)
        {
            _loaders[type] = loader;
        }

#if NETSTANDARD2_0
        /// <summary>
        /// Load addition data to the view model that is placed in the actionExecutedContext.
        /// </summary>
        /// <param name="actionExecutedContext">Action executed context</param>
        /// <param name="dataProvider">Function that loads data for loaders.</param>
        /// <returns></returns>
        public async Task Load(ActionExecutedContext actionExecutedContext, Func<ActionExecutedContext, Task<TData>> dataProvider)
        {
            // Detect if response exist, possible situation when inner exception was thrown and we get null response.
            if (actionExecutedContext.HttpContext?.Response == null) {
                return;
            }

            if (!(actionExecutedContext.Result is ObjectResult objectContent)) {
                return;
            }

            var resultType = objectContent.DeclaredType;
            // Detect type of elements if returned value is list.
            var list = objectContent.Value as IList;
            if (list != null) {
                // We process only lists with one GenericArugments (ignore dictionary, etc)
                var genericArguments = list.GetType().GetGenericArguments();
                if (genericArguments.Count() != 1) {
                    return;
                }

                resultType = genericArguments.Single();
            }

            // Check if any data should be loaded.
            var fieldsString = actionExecutedContext.HttpContext.Request.Query
                .Where(p => p.Key.Equals(FIELDS_PARAMETER, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value.ToString())
                .FirstOrDefault();

            if (string.IsNullOrEmpty(fieldsString)) {
                return;
            }

            // Find appropriate loaders and loads data.
            var matchedLoaders = _loaders.Where(loader => resultType.IsSubclassOf(loader.Key) || resultType == loader.Key).ToList();


            IList<string> fields = null;
            TData data = default(TData);

            // Get data before process loads that prevent duplication call in case of few matched loaders.
            if (matchedLoaders.Count() != 0) {
                data = await dataProvider(actionExecutedContext);
                fields = fieldsString.Replace(" ", "").Split(',');
            }
            foreach (var loader in matchedLoaders) {
                // Process list of view models.
                await GetCallLoaderGenericMethod(loader.Key)(loader.Value, objectContent.Value, fields, data);
            }
        }
#endif

#if NET46
        /// <summary>
        /// Load addition data to the view model that is placed in the actionExecutedContext.
        /// </summary>
        /// <param name="actionExecutedContext">Action executed context</param>
        /// <param name="dataProvider">Function that loads data for loaders.</param>
        /// <returns></returns>
        public async Task Load(HttpActionExecutedContext actionExecutedContext, Func<HttpActionExecutedContext, Task<TData>> dataProvider)
        {
            // Detect if response exist, possible situation when inner exception was thrown and we get null response.
            if (actionExecutedContext.Response == null) {
                return;
            }

            var objectContent = actionExecutedContext.Response.Content as ObjectContent;
            if (objectContent == null) {
                return;
            }

            var resultType = objectContent.ObjectType;
            // Detect type of elements if returned value is list.
            var list = objectContent.Value as IList;
            if (list != null) {
                // We process only lists with one GenericArugments (ignore dictionary, etc)
                var genericArguments = list.GetType().GetGenericArguments();
                if (genericArguments.Count() != 1) {
                    return;
                }

                resultType = genericArguments.Single();
            }

            // Check if any data should be loaded.
            var fieldsString = actionExecutedContext.Request.GetQueryNameValuePairs()
                .Where(p => p.Key.Equals(FIELDS_PARAMETER, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Value)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(fieldsString)) {
                return;
            }

            // Find appropriate loaders and loads data.
            var matchedLoaders = _loaders.Where(loader => resultType.IsSubclassOf(loader.Key) || resultType == loader.Key).ToList();

           
            IList<string> fields = null;
            TData data = default(TData);

            // Get data before process loads that prevent duplication call in case of few matched loaders.
            if (matchedLoaders.Count() != 0) {
                data = await dataProvider(actionExecutedContext);
                fields = fieldsString.Replace(" ", "").Split(',');
            }
            foreach (var loader in matchedLoaders) {
                // Process list of view models.
                await GetCallLoaderGenericMethod(loader.Key)(loader.Value, objectContent.Value, fields, data);
            }
        }
#endif

        // TODO: consider cache this loading to the dictionary. But 1 M calls takes only 8 seconds, so probably it isn't needed.
        private Func<object, object, IList<string>, TData, Task> GetCallLoaderGenericMethod(Type type)
        {
            return (Func<object, object, IList<string>, TData, Task>)this
                .GetType()
                .GetMethod("CallLoader", BindingFlags.Instance | BindingFlags.NonPublic)
                .MakeGenericMethod(new[] { type })
                .CreateDelegate(typeof (Func<object, object, IList<string>, TData, Task>), this);
        }

        /// <summary>
        /// Call correct generic version of the loader.
        /// </summary>
        private async Task CallLoader<T>(object loader, object viewModel,
            IList<string> fields, TData data) where T: new ()
        {
            var typedLoader = (IViewModelLoader<T, TData>)loader;
            if (viewModel is IList) {
                // NOTE: we can't convert directly to IList<T> because contravariance prevents it. 
                await typedLoader.LoadAsync(((IEnumerable<T>) viewModel).ToList(), fields, data);
            } else {
                await typedLoader.LoadAsync((T)viewModel, fields, data);
            }
        }
    }
}