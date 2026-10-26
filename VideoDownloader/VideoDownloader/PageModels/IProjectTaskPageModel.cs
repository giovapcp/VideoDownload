using CommunityToolkit.Mvvm.Input;
using VideoDownloader.Models;

namespace VideoDownloader.PageModels
{
    public interface IProjectTaskPageModel
    {
        IAsyncRelayCommand<ProjectTask> NavigateToTaskCommand { get; }
        bool IsBusy { get; }
    }
}