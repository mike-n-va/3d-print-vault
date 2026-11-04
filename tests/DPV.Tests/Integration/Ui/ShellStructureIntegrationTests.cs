using System.Xml.Linq;
using DPV.Tests.Support;

namespace DPV.Tests.Integration.Ui;

public class ShellStructureIntegrationTests
{
    [Fact]
    public void MainWindowDeclaresVisibleSidebarAndNavigationContentRegion()
    {
        var viewPath = Path.Combine(TestRepository.Root, "src", "DPV.UI", "MainWindow.axaml");
        var document = XDocument.Load(viewPath);
        var xamlNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2006/xaml");
        var root = Assert.IsType<XElement>(document.Root);

        Assert.Equal("3D Print Vault", (string?)root.Attribute("Title"));

        var sidebar = Assert.Single(
            root.Descendants(),
            element => (string?)element.Attribute(xamlNamespace + "Name") == "SidebarRegion");
        var navigationRegion = Assert.Single(
            root.Descendants(),
            element => (string?)element.Attribute(xamlNamespace + "Name") == "NavigationRegion");
        var navigationItems = Assert.Single(
            sidebar.Descendants(),
            element => (string?)element.Attribute(xamlNamespace + "Name") == "SidebarNavigation");
        var dashboardContentRegion = Assert.Single(
            navigationRegion.Descendants(),
            element => (string?)element.Attribute(xamlNamespace + "Name") == "DashboardContentRegion");
        var dashboardButton = Assert.Single(
            navigationItems.Descendants(),
            element => (string?)element.Attribute(xamlNamespace + "Name") == "DashboardNavigationButton");
        var dashboardTemplate = Assert.Single(
            root.Descendants(),
            element => element.Name.LocalName == "DataTemplate"
                && (string?)element.Attribute("DataType") == "{x:Type viewModels:DashboardViewModel}");

        var sidebarLabels = sidebar.Descendants()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => (string?)element.Attribute("Text"))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        var contentLabels = navigationRegion.Descendants()
            .Where(element => element.Name.LocalName == "TextBlock")
            .Select(element => (string?)element.Attribute("Text"))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();

        Assert.Contains("NAVIGATION", sidebarLabels);
        Assert.Contains("Dashboard", sidebarLabels);
        Assert.Contains("Artists", sidebarLabels);
        Assert.Contains("Collections", sidebarLabels);
        Assert.Contains("Tags", sidebarLabels);
        Assert.Contains("Settings", sidebarLabels);
        Assert.Contains("Workspace", contentLabels);
        Assert.Equal("{Binding ShowDashboardCommand}", (string?)dashboardButton.Attribute("Command"));
        Assert.Equal("{Binding CurrentViewModel}", (string?)Assert.Single(
            dashboardContentRegion.Descendants(),
            element => element.Name.LocalName == "ContentControl").Attribute("Content"));
        Assert.Contains(
            dashboardTemplate.Descendants(),
            element => element.Name.LocalName == "DashboardView");

        var dashboardViewPath = Path.Combine(TestRepository.Root, "src", "DPV.UI", "Views", "DashboardView.axaml");
        var dashboardView = XDocument.Load(dashboardViewPath);
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Title}");
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && (string?)element.Attribute("Text") == "{Binding Description}");
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Command") == "{Binding ImportCommand}");
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Command") == "{Binding CancelCommand}");
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "ProgressBar"
                && (string?)element.Attribute("Value") == "{Binding ProgressPercentage}");
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "ListBox"
                && (string?)element.Attribute("ItemsSource") == "{Binding Models}");
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "TextBox"
                && (string?)element.Attribute("Text") == "{Binding SearchQuery}");
        Assert.Contains(
            dashboardView.Descendants(),
            element => element.Name.LocalName == "ComboBox"
                && (string?)element.Attribute(xamlNamespace + "Name") == "TagFilterBox");
    }
}
