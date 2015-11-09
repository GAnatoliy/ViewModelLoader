using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;


namespace ViewModelLoader
{
    /// <summary>
    /// Base class for loaders that load data to the view model.
    /// </summary>
    public abstract class ViewModelLoader<TViewModel, TData> : IViewModelLoader<TViewModel, TData> where TViewModel: new ()
    {
        private const char INNER_FIELD_DELIMITER = '.';

        /// <summary>
        /// Contains field loaders.
        /// </summary>
        private readonly IDictionary<string, Func<TViewModel, IList<string>, TData, Task>> _loaders;

        /// <summary>
        /// Field loaders for list of view models.
        /// </summary>
        private readonly IDictionary<string, Func<IList<TViewModel>, IList<string>, TData, Task>> _listLoaders; 


        public ViewModelLoader()
        {
            _loaders = new Dictionary<string, Func<TViewModel, IList<string>, TData, Task>>();
            _listLoaders = new Dictionary<string, Func<IList<TViewModel>, IList<string>, TData, Task>>();
        } 

        /// <summary>
        /// Load data to view model.
        /// </summary>
        public async Task LoadAsync(TViewModel viewModel, IList<string> fields, TData data)
        {
            InitFieldLoaders();
            fields = fields.Select(NormalizeField).ToList();
            var tasks = fields.Where(IsNotInnerField).Select(async f => await LoadFieldAsync(viewModel, f, fields, data));
            await Task.WhenAll(tasks);
        }



        /// <summary>
        /// Load data to list of view models.
        /// </summary>
        public async Task LoadAsync(IList<TViewModel> viewModels, IList<string> fields, TData data)
        {
            InitFieldLoaders();
            fields = fields.Select(NormalizeField).ToList();
            var tasks = fields.Where(IsNotInnerField).Select(async f => await LoadFieldAsync(viewModels, f, fields, data));
            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Method that inits fields loaders. Should be overridden.
        /// </summary>
        protected abstract void InitFieldLoaders();

        /// <summary>
        /// Add field loader by field name.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="fieldLoader"></param>
        /// <param name="fieldListLoader"></param>
        protected void AddFieldLoader(string key, Func<TViewModel, IList<string>, TData, Task> fieldLoader,
            Func<IList<TViewModel>, IList<string>, TData, Task> fieldListLoader)
        {
            key = NormalizeField(key);
            _loaders[key] = fieldLoader;
            _listLoaders[key] = fieldListLoader;
        }

        /// <summary>
        /// Add field loader by expression.
        /// </summary>
        /// <param name="keyProperty"></param>
        /// <param name="fieldLoader"></param>
        /// <param name="fieldListLoader"></param>
        protected void AddFieldLoader(Expression<Func<TViewModel, object>> keyProperty, Func<TViewModel, IList<string>, TData, Task> fieldLoader,
            Func<IList<TViewModel>, IList<string>, TData, Task> fieldListLoader)
        {
            AddFieldLoader(GetPropertyName(keyProperty), fieldLoader, fieldListLoader);
        }

        /// <summary>
        /// Returns list of the subfields of given field specified by string.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="fields"></param>
        /// <returns>Returns subfields or empty list.</returns>
        protected IList<string> GetSubFields(string field, IList<string> fields)
        {
            return fields
                .Where(f => f.StartsWith(NormalizeField(field) + INNER_FIELD_DELIMITER))
                .Select(f => f.Split(new [] { INNER_FIELD_DELIMITER }, 2).Last())
                .ToList();
        }

        /// <summary>
        /// Returns list of the subfields of given field specified by expression.
        /// </summary>
        /// <param name="field"></param>
        /// <param name="fields"></param>
        /// <returns>Returns subfields or empty list.</returns>
        protected IList<string> GetSubFields(Expression<Func<TViewModel, object>> field, IList<string> fields)
        {
            return fields
                .Where(f => f.StartsWith(NormalizeField(GetPropertyName(field)) + INNER_FIELD_DELIMITER))
                .Select(f => f.Split(new[] { INNER_FIELD_DELIMITER }, 2).Last())
                .ToList();
        } 


        private static bool IsNotInnerField(string field)
        {
            return !field.Contains(INNER_FIELD_DELIMITER);
        }

        private static string NormalizeField(string field)
        {
            return field.Trim().ToLower();
        }

        private async Task LoadFieldAsync(TViewModel board, string field, IList<string> fields, TData data)
        {
            if (!_loaders.ContainsKey(field)) {
                throw new Exception($"Can't load field '{field}' for view model '{typeof (TViewModel)}', field or loader don't exist.");
            }

            await _loaders[field](board, fields, data);
        }

        private async Task LoadFieldAsync(IList<TViewModel> boards, string field, IList<string> fields, TData data)
        {
            if (!_listLoaders.ContainsKey(field)) {
                throw new Exception($"Can't load field '{field}' for view model '{typeof (TViewModel)}', field or loader for list don't exist.");
            }

            await _listLoaders[field](boards, fields, data);
        }

        private string GetPropertyName(Expression<Func<TViewModel, object>> property)
        {
            // Get property name from expression.
            PropertyInfo propertyInfo;

            if (property.Body is MemberExpression) {
                var member = (MemberExpression)property.Body;
                propertyInfo = (PropertyInfo)member.Member;
            } else if (property.Body is UnaryExpression) {
                var operand = ((UnaryExpression)property.Body).Operand;
                propertyInfo = (PropertyInfo)((MemberExpression)operand).Member;
            } else {
                throw new Exception("Unsupported lambda expression. Should be property.");
            }

            return propertyInfo.Name;
        }
    }
}