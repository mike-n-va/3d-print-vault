using DPV.Domain.Enums;
using DPV.Infrastructure.Import.Discovery;

namespace DPV.Tests.Unit.Infrastructure.Discovery;

public class AssetRoleClassifierTests
{
    [Theory]
    [InlineData("dragon.stl", AssetCategory.Geometry, FileRole.PrimaryGeometry)]
    [InlineData("model.obj", AssetCategory.Geometry, FileRole.PrimaryGeometry)]
    [InlineData("dragon_supported.stl", AssetCategory.Geometry, FileRole.SupportedVariant)]
    [InlineData("dragon_presupported.stl", AssetCategory.Geometry, FileRole.SupportedVariant)]
    [InlineData("dragon_supp.stl", AssetCategory.Geometry, FileRole.SupportedVariant)]
    [InlineData("dragon_pre.stl", AssetCategory.Geometry, FileRole.SupportedVariant)]
    [InlineData("dragon_part1.stl", AssetCategory.Geometry, FileRole.AssemblyComponent)]
    [InlineData("dragon_head.stl", AssetCategory.Geometry, FileRole.AssemblyComponent)]
    [InlineData("dragon_wings.stl", AssetCategory.Geometry, FileRole.AssemblyComponent)]
    [InlineData("plate.3mf", AssetCategory.SlicingPayload, FileRole.SlicedPlate)]
    [InlineData("model.lychee", AssetCategory.SlicingPayload, FileRole.SlicedPlate)]
    [InlineData("render.png", AssetCategory.VisualMedia, FileRole.HeroRender)]
    [InlineData("hero_preview.jpg", AssetCategory.VisualMedia, FileRole.HeroRender)]
    [InlineData("cover.webp", AssetCategory.VisualMedia, FileRole.HeroRender)]
    [InlineData("thumb.png", AssetCategory.VisualMedia, FileRole.Thumbnail)]
    [InlineData("icon.jpg", AssetCategory.VisualMedia, FileRole.Thumbnail)]
    [InlineData("assembly_guide.pdf", AssetCategory.Documentation, FileRole.AssemblyGuide)]
    [InlineData("instructions.txt", AssetCategory.Documentation, FileRole.AssemblyGuide)]
    [InlineData("license.txt", AssetCategory.Documentation, FileRole.License)]
    [InlineData("terms.pdf", AssetCategory.Documentation, FileRole.License)]
    [InlineData("model.dpv.json", AssetCategory.MetadataSidecar, FileRole.SidecarManifest)]
    [InlineData("archive.zip", AssetCategory.Geometry, FileRole.PrimaryGeometry)]
    public void Classify_AssignsCorrectCategoryAndRole(string fileName, AssetCategory expectedCategory, FileRole expectedRole)
    {
        var (category, role) = AssetRoleClassifier.Classify(fileName);

        Assert.Equal(expectedCategory, category);
        Assert.Equal(expectedRole, role);
    }

    [Theory]
    [InlineData("model.stl", true)]
    [InlineData("model.3mf", true)]
    [InlineData("preview.png", true)]
    [InlineData("guide.pdf", true)]
    [InlineData("model.dpv.json", true)]
    [InlineData("archive.zip", true)]
    [InlineData("program.exe", false)]
    [InlineData("data.bin", false)]
    public void IsSupportedAssetExtension_RecognizesSupportedTypes(string fileName, bool expected)
    {
        var actual = AssetRoleClassifier.IsSupportedAssetExtension(fileName);
        Assert.Equal(expected, actual);
    }
}
