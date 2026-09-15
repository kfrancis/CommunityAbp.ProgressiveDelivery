using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using CommunityAbp.ProgressiveDelivery.Subjects;
using Volo.Abp.DependencyInjection;

namespace CommunityAbp.ProgressiveDelivery.Rollouts;

/// <summary>
/// SHA-256 based bucketing: <c>hash(subjectType|subjectId|track|level) mod 10000</c>. Platform independent
/// and stable across releases, which is what makes cohort growth monotonic.
/// </summary>
public class StableHashRolloutCohortAllocator : IRolloutCohortAllocator, ISingletonDependency
{
    public int BucketCount => ProgressiveDeliveryConsts.RolloutBasisPointsMax;

    public virtual int GetBucket(FeatureSubject subject, string trackName, int candidateLevel)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(trackName);

        var input = string.Concat(
            subject.Type.ToUpperInvariant(), "|",
            subject.Id, "|",
            trackName.Trim().ToUpperInvariant(), "|",
            candidateLevel.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(input), hash);
        var value = BinaryPrimitives.ReadUInt64LittleEndian(hash);
        return (int)(value % (ulong)BucketCount);
    }

    public virtual bool IsInCohort(FeatureSubject subject, string trackName, int candidateLevel, int percentageBasisPoints)
    {
        if (percentageBasisPoints <= 0)
        {
            return false;
        }

        if (percentageBasisPoints >= BucketCount)
        {
            return true;
        }

        return GetBucket(subject, trackName, candidateLevel) < percentageBasisPoints;
    }
}
