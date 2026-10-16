using System;
using System.Linq;
using DigitalWonderlab.Telemetry;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Scoping;

namespace DigitalWonderlab.CountThings;

/// <summary>
/// What Count Things reports in its anonymous daily heartbeat: the site's rough size (banded counts of
/// content, media, members, users and document types), whether Umbraco Forms is installed, and how often
/// the dashboard counted in the last day. See README "Telemetry". Reads Umbraco's core tables, whose shape
/// is the same from v13 to v17.
/// </summary>
internal sealed class CountThingsTelemetryContributor : ITelemetryContributor
{
    public const string Product = "count-things";
    public const string CountsRun = "countsRun";

    private readonly IScopeProvider _scopeProvider;
    private readonly TelemetryPayloadBuilder _builder;
    private readonly TelemetryCounters _counters;
    private readonly IConfiguration _configuration;

    public CountThingsTelemetryContributor(IScopeProvider scopeProvider, TelemetryPayloadBuilder builder,
        TelemetryCounters counters, IConfiguration configuration)
    {
        _scopeProvider = scopeProvider;
        _builder = builder;
        _counters = counters;
        _configuration = configuration;
    }

    public string ProductId => Product;

    public string? PackageVersion => TelemetryPayloadBuilder.VersionOf(typeof(CountThingsTelemetryContributor).Assembly);

    public bool IsEnabled()
        => _configuration.GetValue<bool?>("CountThings:Telemetry:Enabled") != false
           && _builder.IsProductToggleOn(Product);

    public void Contribute(TelemetryReport report)
    {
        using (var scope = _scopeProvider.CreateScope(autoComplete: true))
        {
            var db = scope.Database;
            long Nodes(Guid objectType) => db.ExecuteScalar<long>(
                "SELECT COUNT(*) FROM umbracoNode WHERE nodeObjectType = @0 AND trashed = 0", objectType);

            report.SizeBand["content"] = TelemetryBand.Of(Nodes(Constants.ObjectTypes.Document));
            report.SizeBand["media"] = TelemetryBand.Of(Nodes(Constants.ObjectTypes.Media));
            report.SizeBand["members"] = TelemetryBand.Of(Nodes(Constants.ObjectTypes.Member));
            report.SizeBand["doctypes"] = TelemetryBand.Of(Nodes(Constants.ObjectTypes.DocumentType));
            report.SizeBand["users"] = TelemetryBand.Of(db.ExecuteScalar<long>("SELECT COUNT(*) FROM umbracoUser"));
        }

        report.Features["forms"] = AppDomain.CurrentDomain.GetAssemblies()
            .Any(a => a.GetName().Name == "Umbraco.Forms.Core");

        _counters.AddTo(report, CountsRun);
    }
}
