using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ZeroTrustMigrationAddin.Services.Pipeline
{
    /// <summary>
    /// Generates actionable recommendations from an assessment.
    /// Recommendations are scoped, named, bounded actions — not generic advice.
    /// </summary>
    public interface IRecommendationProvider<TAssessment> where TAssessment : class
    {
        /// <summary>Display name for logging and diagnostics.</summary>
        string Name { get; }

        /// <summary>Generates recommendations from the assessment.</summary>
        Task<List<PipelineRecommendation>> GetRecommendationsAsync(TAssessment assessment, CancellationToken ct = default);
    }
}
