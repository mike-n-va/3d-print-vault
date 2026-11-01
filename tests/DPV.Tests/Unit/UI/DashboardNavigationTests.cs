using DPV.UI.ViewModels;

namespace DPV.Tests.Unit.UI;

public class DashboardNavigationTests
{
    [Fact]
    public void DashboardIsTheInitialShellViewModel()
    {
        var dashboard = new DashboardViewModel();
        var shell = new ShellViewModel(dashboard);

        Assert.Same(dashboard, shell.CurrentViewModel);
    }

    [Fact]
    public void DashboardNavigationCommandCanSelectStartupRoute()
    {
        var dashboard = new DashboardViewModel();
        var shell = new ShellViewModel(dashboard);

        Assert.True(shell.ShowDashboardCommand.CanExecute(null));
        shell.ShowDashboardCommand.Execute(null);

        Assert.Same(dashboard, shell.CurrentViewModel);
    }

    [Fact]
    public void DashboardViewModelProvidesOnlyPlaceholderContent()
    {
        var viewModel = new DashboardViewModel();

        Assert.Equal("Welcome to 3D Print Vault", viewModel.Title);
        Assert.Equal("Your library overview will appear here.", viewModel.Description);
    }
}
