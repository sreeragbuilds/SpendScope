using Microsoft.AspNetCore.Routing;

namespace SpendScope.Features;

/// <summary>
/// Implemented by each slice's <c>*Endpoint</c> class to map its route.
/// The Api discovers implementations by reflection (spec appendix A.2).
/// Lives in Features rather than Api because ARCH-08 requires implementations to live in
/// Features, and Features may not reference Api.
/// </summary>
public interface IFeatureEndpoint
{
    void Map(IEndpointRouteBuilder app);
}
