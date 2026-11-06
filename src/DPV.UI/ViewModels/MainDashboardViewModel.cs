using System;
using System.Threading;
using System.Threading.Tasks;
using DPV.Domain.Contracts.Catalog;
using DPV.Domain.Contracts.Import;
using CatalogQuery = DPV.Domain.Contracts.Catalog.IModelCatalogQuery;

namespace DPV.UI.ViewModels;

public class MainDashboardViewModel : DashboardViewModel
{
    public MainDashboardViewModel(
        IModelImportService importService,
        CatalogQuery catalogQuery,
        long maxFileSizeBytes = 2L * 1024 * 1024 * 1024,
        int batchLimit = 500,
        string? rootPath = null,
        IModelDetailRepository? detailRepository = null,
        IThumbnailService? thumbnails = null,
        IUserPreferencesService? preferencesService = null,
        IModelGroupingQuery? groupingQuery = null,
        IReleaseCatalogQuery? releaseCatalogQuery = null,
        IReleaseDetailRepository? releaseDetailRepository = null)
        : base(
            importService,
            catalogQuery,
            maxFileSizeBytes,
            batchLimit,
            rootPath,
            detailRepository,
            thumbnails,
            preferencesService,
            groupingQuery,
            releaseCatalogQuery,
            releaseDetailRepository)
    {
    }
}
