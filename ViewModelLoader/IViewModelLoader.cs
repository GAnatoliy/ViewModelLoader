using System.Collections.Generic;
using System.Threading.Tasks;


namespace ViewModelLoader
{
    public interface IViewModelLoader<TViewModel, in TData> where TViewModel: new ()
    {
        /// <summary>
        /// Load data to view model.
        /// </summary>
        Task LoadAsync(TViewModel viewModel, IList<string> fields, TData data);

        /// <summary>
        /// Load data to list of view models.
        /// </summary>
        Task LoadAsync(IList<TViewModel> viewModels, IList<string> fields, TData data);
    }
}