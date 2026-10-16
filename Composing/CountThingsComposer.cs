using DigitalWonderlab.Telemetry;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace DigitalWonderlab.CountThings;

/// <summary>Registers the anonymous daily telemetry heartbeat (see README "Telemetry").</summary>
public class CountThingsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddDwlTelemetryProduct<CountThingsTelemetryContributor>();
}
