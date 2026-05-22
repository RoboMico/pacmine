using Xunit;
using Pacmine.Environment.Indexing;

namespace Pacmine.Environment.Tests;

public class IndexManagerTests : IndexHandlerTestBase
{
    [Fact]
    public void AddHandler_HandlerIsRegistered()
    {
        var mgr = new IndexManager(RegistryDirectory);
        var handler = new PackageListHandler(IndexDirectory);

        mgr.AddHandler(handler);

        Assert.Single(mgr.Handlers);
        Assert.Same(handler, mgr.Handlers[0]);
    }

    [Fact]
    public void GetHandler_ReturnsCorrectHandlerType()
    {
        var mgr = new IndexManager(RegistryDirectory);
        mgr.AddHandler(new PackageListHandler(IndexDirectory));
        mgr.AddHandler(new VirtualPackagesHandler(IndexDirectory));

        var pkgList = mgr.GetHandler<PackageListHandler>();
        var virtPkg = mgr.GetHandler<VirtualPackagesHandler>();

        Assert.NotNull(pkgList);
        Assert.NotNull(virtPkg);
        Assert.IsType<PackageListHandler>(pkgList);
        Assert.IsType<VirtualPackagesHandler>(virtPkg);
    }

    [Fact]
    public void GetHandler_TypeNotFound_ReturnsNull()
    {
        var mgr = new IndexManager(RegistryDirectory);
        Assert.Null(mgr.GetHandler<PackageListHandler>());
    }

    [Fact]
    public void RemoveHandler_RemovesMatchingHandler()
    {
        var mgr = new IndexManager(RegistryDirectory);
        var handler = new PackageListHandler(IndexDirectory);
        mgr.AddHandler(handler);

        bool removed = mgr.RemoveHandler(h => h is PackageListHandler);

        Assert.True(removed);
        Assert.Empty(mgr.Handlers);
    }

    [Fact]
    public void RemoveHandler_NoMatch_ReturnsFalse()
    {
        var mgr = new IndexManager(RegistryDirectory);
        mgr.AddHandler(new PackageListHandler(IndexDirectory));

        bool removed = mgr.RemoveHandler(h => h is VirtualPackagesHandler);

        Assert.False(removed);
        Assert.Single(mgr.Handlers);
    }

    [Fact]
    public void Load_DoesNotThrow()
    {
        var mgr = new IndexManager(RegistryDirectory);
        mgr.AddHandler(new PackageListHandler(IndexDirectory));
        mgr.AddHandler(new VirtualPackagesHandler(IndexDirectory));

        var exception = Record.Exception(() => mgr.Load());
        Assert.Null(exception);
    }

    [Fact]
    public void Initialize_CallsInitializeOnAllHandlers()
    {
        var mgr = new IndexManager(RegistryDirectory);
        var handler = new PackageListHandler(IndexDirectory);
        mgr.AddHandler(handler);

        mgr.Initialize();

        // Initialize should have written an empty dictionary to disk
        Assert.Empty(handler.Content);
    }

    [Fact]
    public void Rebuild_NoRegistries_HandlersStayEmpty()
    {
        var mgr = new IndexManager(RegistryDirectory);
        var pkgList = new PackageListHandler(IndexDirectory);
        mgr.AddHandler(pkgList);

        mgr.TryRebuild();

        Assert.Empty(pkgList.Content);
    }

    [Fact]
    public void OnWriteRegistry_NotifiesAllHandlers()
    {
        var mgr = new IndexManager(RegistryDirectory);
        var pkgList = new PackageListHandler(IndexDirectory);
        mgr.AddHandler(pkgList);

        var registry = CreateRegistry("new-pkg", "1.0.0");
        mgr.OnWriteRegistry(registry);

        Assert.Single(pkgList.Content);
        Assert.Equal("1.0.0", pkgList.Content["new-pkg"].RawString);
    }

    [Fact]
    public void OnRemoveRegistry_NotifiesAllHandlers()
    {
        var mgr = new IndexManager(RegistryDirectory);
        var pkgList = new PackageListHandler(IndexDirectory);
        mgr.AddHandler(pkgList);

        mgr.OnWriteRegistry(CreateRegistry("pkg-a", "1.0.0"));
        mgr.OnRemoveRegistry(CreateRegistry("pkg-a", "1.0.0"));

        Assert.Empty(pkgList.Content);
    }

    [Fact]
    public void MultipleHandlers_AllReceiveEvents()
    {
        var mgr = new IndexManager(RegistryDirectory);
        var pkgList = new PackageListHandler(IndexDirectory);
        var virtPkg = new VirtualPackagesHandler(IndexDirectory);
        mgr.AddHandler(pkgList);
        mgr.AddHandler(virtPkg);

        var registry = CreateRegistry("provider", "1.0.0",
            provides: new() { { "virtual-foo", new Core.VersionIdentifier("1.0.0") } });
        mgr.OnWriteRegistry(registry);

        Assert.Single(pkgList.Content);
        Assert.True(virtPkg.Content.ContainsKey("virtual-foo"));
    }
}
