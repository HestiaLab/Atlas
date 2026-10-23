using Atlas.Blazor.Components;
using Atlas.Blazor.Content;
using Microsoft.AspNetCore.Components;
using Xunit;

namespace Atlas.Blazor.Tests;

public sealed class AtlasContentSourceCoordinatorTests
{
    [Fact]
    public void Route_generation_reuses_revision_for_same_kind()
    {
        var renderer = CreateRenderer();
        var previous = new[]
        {
            new AtlasContentHostLocalRegistration(
                "document",
                AtlasContentRegistrationSource.Route,
                renderer,
                Revision: 7,
                StructuralSignature: "Route"),
        };
        var routes = new[]
        {
            new AtlasContentRouteDescriptor("document", renderer),
        };
        var nextRevision = 10;

        var generation = AtlasContentSourceCoordinator.BuildGeneration(
            previous,
            routes,
            Array.Empty<AtlasContentMapEntry>(),
            ref nextRevision);

        var registration = Assert.Single(generation);
        Assert.Equal(7, registration.Revision);
        Assert.Equal(10, nextRevision);
    }

    [Fact]
    public void Map_generation_continues_revision_only_for_same_structure()
    {
        var entry = new AtlasContentMapEntry(
            "document",
            typeof(ComponentBase),
            InjectsContentContext: false,
            Array.Empty<AtlasContentBinding>());
        var previous = new[]
        {
            new AtlasContentHostLocalRegistration(
                "document",
                AtlasContentRegistrationSource.Map,
                entry.CreateRenderer(),
                Revision: 3,
                entry.StructuralSignature),
        };
        var nextRevision = 10;

        var same = AtlasContentSourceCoordinator.BuildGeneration(
            previous,
            Array.Empty<AtlasContentRouteDescriptor>(),
            new[] { entry },
            ref nextRevision);
        Assert.Equal(3, Assert.Single(same).Revision);
        Assert.Equal(10, nextRevision);

        var changedEntry = new AtlasContentMapEntry(
            "document",
            typeof(object),
            InjectsContentContext: false,
            Array.Empty<AtlasContentBinding>());
        var changed = AtlasContentSourceCoordinator.BuildGeneration(
            previous,
            Array.Empty<AtlasContentRouteDescriptor>(),
            new[] { changedEntry },
            ref nextRevision);
        Assert.Equal(10, Assert.Single(changed).Revision);
        Assert.Equal(11, nextRevision);
    }

    private static RenderFragment<AtlasContentContext> CreateRenderer()
    {
        return _ => _ => { };
    }
}
